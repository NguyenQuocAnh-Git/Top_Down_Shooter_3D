using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CoopSlotPresence
{
    Empty,
    Occupied
}

public readonly struct CoopLobbySlotState
{
    public CoopSlotPresence Presence { get; }
    public bool IsHost { get; }
    public bool IsReady { get; }
    public string DisplayName { get; }

    public CoopLobbySlotState(CoopSlotPresence presence, bool isHost, bool isReady, string displayName = "")
    {
        Presence = presence;
        IsHost = isHost;
        IsReady = isReady;
        DisplayName = displayName ?? string.Empty;
    }

    public static CoopLobbySlotState Empty => new CoopLobbySlotState(CoopSlotPresence.Empty, false, false);

    public static CoopLobbySlotState Occupied(bool isHost, bool isReady, string displayName = "") =>
        new CoopLobbySlotState(CoopSlotPresence.Occupied, isHost, isReady, displayName);
}

public class CoopNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public const int MaxCoopPlayers = 4;
    public const int MinCoopPlayersToStart = 2;

    public static CoopNetworkManager Instance
    {
        get
        {
            if (applicationIsQuitting || Application.isPlaying == false)
                return instance;

            if (instance == null)
            {
                var go = new GameObject(nameof(CoopNetworkManager));
                instance = go.AddComponent<CoopNetworkManager>();
            }

            return instance;
        }
    }

    private static CoopNetworkManager instance;
    private static bool applicationIsQuitting;

    public event Action<string> OnStatusChanged;
    public event Action<IReadOnlyList<SessionInfo>> OnSessionsChanged;
    public event Action<string> OnJoinedRoom;
    public event Action<CoopLobbySlotState[]> OnLobbySlotsChanged;
    public event Action<string> OnRoomClosed;
    public event Action OnLobbyReturned;
    public event Action OnMissionSelectionStarted;
    public event Action<string> OnMissionPreviewed;
    public event Action<string> OnMissionSelected;
    public event Action OnWeaponSelectionStarted;
    public event Action OnComicStarted;
    public event Action OnCoopPlayGame;

    public bool IsHosting => runner != null && runner.IsRunning && runner.IsServer;
    public bool IsInRoom => runner != null && runner.IsRunning && (runner.IsServer || runner.IsClient);
    public bool CanLocalPlayerStartGame => IsHosting && IsLobbyReadyToStart();
    public bool CanLocalPlayerSelectMission => IsHosting && currentSetupStep == CoopSetupStep.MissionSelection;
    public bool CanLocalPlayerSelectWeapons => IsInRoom && currentSetupStep == CoopSetupStep.WeaponSelection && IsLocalPlayerWeaponReady() == false;
    public bool CanLocalPlayerPressCoopPlay => IsHosting && currentSetupStep == CoopSetupStep.Comic;
    public string CurrentRoomName { get; private set; }

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private NetworkObjectProviderDefault objectProvider;
    private readonly List<SessionInfo> sessions = new List<SessionInfo>();
    private readonly Dictionary<int, bool> playerReady = new Dictionary<int, bool>();
    private readonly HashSet<int> weaponReadyPlayers = new HashSet<int>();
    private int hostPlayerId = -1;
    private bool isJoiningRoom;
    private bool isStartingGame;
    private bool isLeavingIntentionally;
    private bool remoteRoomClosedHandled;
    private byte setupSequence;
    private CoopSetupStep currentSetupStep = CoopSetupStep.Lobby;

    private static readonly ReliableKey ReadyStateKey = ReliableKey.FromInts(42, 1, 0, 0);
    private static readonly ReliableKey ReadyToggleKey = ReliableKey.FromInts(42, 1, 0, 1);
    private static readonly ReliableKey RoomClosedKey = ReliableKey.FromInts(42, 1, 0, 2);
    private static readonly ReliableKey SetupStepKey = ReliableKey.FromInts(42, 1, 0, 3);
    private static readonly ReliableKey MissionSelectedKey = ReliableKey.FromInts(42, 1, 0, 4);
    private static readonly ReliableKey WeaponReadyKey = ReliableKey.FromInts(42, 1, 0, 5);
    private static readonly ReliableKey PlayGameKey = ReliableKey.FromInts(42, 1, 0, 6);
    private static readonly ReliableKey MissionPreviewKey = ReliableKey.FromInts(42, 1, 0, 7);
    private static readonly ReliableKey DisplayNameAnnounceKey = ReliableKey.FromInts(42, 1, 0, 8);
    private static readonly ReliableKey DisplayNamesStateKey = ReliableKey.FromInts(42, 1, 0, 9);

    private readonly Dictionary<int, string> playerDisplayNames = new Dictionary<int, string>();
    private string pendingLocalNickname = string.Empty;

    private enum CoopSetupStep : byte
    {
        Lobby = 0,
        MissionSelection = 1,
        WeaponSelection = 2,
        Comic = 3
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        ShutdownNetworkSync();
        instance = null;
    }

    private void OnApplicationQuit()
    {
        applicationIsQuitting = true;

        if (instance == this)
            ShutdownNetworkSync();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        applicationIsQuitting = false;
    }

    public void HostGame() => _ = HostGameAsync();

    public void BrowseRooms() => _ = BrowseRoomsAsync();

    public void JoinRoomAt(int index)
    {
        if (isJoiningRoom || index < 0 || index >= sessions.Count)
            return;

        UI_CoopMenu menu = FindObjectOfType<UI_CoopMenu>(true);
        menu?.PrepareForNetworkConnect();

        _ = JoinRoomAsync(sessions[index].Name);
    }

    public void SetPendingLocalNickname(string nickname)
    {
        pendingLocalNickname = nickname ?? string.Empty;
    }

    public void CommitLocalDisplayNameForHost()
    {
        string displayName = ResolveDisplayName(pendingLocalNickname, 1);
        GameSessionData.SetLocalDisplayName(displayName);
    }

    public static string ResolveDisplayName(string rawInput, int lobbySlotOneBased)
    {
        string trimmed = rawInput?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed) == false)
            return trimmed;

        int slot = Mathf.Clamp(lobbySlotOneBased, 1, MaxCoopPlayers);
        return $"Player{slot}";
    }

    public void Disconnect() => _ = DisconnectAsync();

    public void ToggleLocalReady()
    {
        if (runner == null || IsInRoom == false)
            return;

        if (currentSetupStep != CoopSetupStep.Lobby)
        {
            CoopLog($"Ready ignored. Current step is {currentSetupStep}.");
            return;
        }

        int playerId = runner.LocalPlayer.PlayerId;
        if (playerId < 0)
            return;

        bool isReady = IsPlayerReady(playerId);

        if (IsHosting && isReady && IsLobbyReadyToStart())
        {
            _ = StartCoopGameAsync();
            return;
        }

        SetPlayerReady(playerId, !isReady);
    }

    public bool IsLocalPlayerReady()
    {
        if (runner == null || IsInRoom == false)
            return false;

        int playerId = runner.LocalPlayer.PlayerId;
        if (playerId < 0)
            return false;

        return IsPlayerReady(playerId);
    }

    public bool IsLocalPlayerWeaponReady()
    {
        if (runner == null || IsInRoom == false)
            return false;

        int playerId = runner.LocalPlayer.PlayerId;
        return playerId >= 0 && weaponReadyPlayers.Contains(playerId);
    }

    public void SelectCoopMission(Mission mission)
    {
        if (mission == null)
            return;

        if (CanLocalPlayerSelectMission == false)
        {
            CoopLog($"Mission select ignored. Host={IsHosting}, Step={currentSetupStep}.");
            return;
        }

        GameSessionData.SetSelectedMission(mission);
        BroadcastString(MissionPreviewKey, string.Empty);
        OnMissionPreviewed?.Invoke(string.Empty);
        BroadcastSequencedString(MissionSelectedKey, mission.missionName);
        ApplyMissionSelected(mission.missionName);
    }

    public void PreviewCoopMission(Mission mission)
    {
        if (mission == null || CanLocalPlayerSelectMission == false)
            return;

        BroadcastString(MissionPreviewKey, mission.missionName);
        OnMissionPreviewed?.Invoke(mission.missionName);
    }

    public void ClearCoopMissionPreview()
    {
        if (CanLocalPlayerSelectMission == false)
            return;

        BroadcastString(MissionPreviewKey, string.Empty);
        OnMissionPreviewed?.Invoke(string.Empty);
    }

    public void NotifyLocalWeaponSelectionReady()
    {
        if (runner == null || IsInRoom == false)
            return;

        if (currentSetupStep != CoopSetupStep.WeaponSelection)
        {
            CoopLog($"Weapon ready ignored. Current step is {currentSetupStep}.");
            return;
        }

        int playerId = runner.LocalPlayer.PlayerId;
        if (playerId < 0)
            return;

        if (weaponReadyPlayers.Contains(playerId))
        {
            CoopLog($"Weapon ready ignored. Player {playerId} is already locked.");
            return;
        }

        if (runner.IsServer)
        {
            MarkWeaponReady(playerId);
            return;
        }

        GameSessionData.SetWeaponsForPlayer(playerId, GameSessionData.GetSelectedWeapons());
        weaponReadyPlayers.Add(playerId);
        CoopLog($"Weapon ready sent to host by player {playerId}.");
        byte[] payload = { (byte)Mathf.Clamp(playerId, 0, 255) };
        runner.SendReliableDataToServer(WeaponReadyKey, payload);
    }

    public void RequestCoopPlayGame()
    {
        if (IsHosting == false)
        {
            RaiseStatus("Only the host can press Play in COOP.");
            CoopLog("Play ignored from client.");
            return;
        }

        if (currentSetupStep != CoopSetupStep.Comic)
        {
            RaiseStatus("Play is only available after the COOP comic.");
            CoopLog($"Play ignored. Current step is {currentSetupStep}.");
            return;
        }

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        GameSessionData.BeginCoopGameplaySession(seed);
        CoopLog($"Host pressed Play. Loading {GameSessionData.GameplaySceneName} with seed {seed}.");
        BroadcastToPlayers(PlayGameKey, EncodePlayGamePayload(seed));
        StartCoopGameplaySceneLoad();
        LogCoopPlayGame();
    }

    public void ReturnCoopSetupToLobby()
    {
        if (IsHosting == false)
        {
            RaiseStatus("Only the host can go back in COOP setup.");
            return;
        }

        AdvanceSetupSequence("Host returned setup to lobby.");
        EnterCoopStep(CoopSetupStep.Lobby, true, "Host returned setup to lobby.");
    }

    private async Task HostGameAsync()
    {
        try
        {
            await DisconnectAsync();

            var activeRunner = EnsureRunner();
            remoteRoomClosedHandled = false;
            string roomName = $"Coop_{UnityEngine.Random.Range(1000, 9999)}";
            CurrentRoomName = roomName;

            var result = await activeRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = roomName,
                PlayerCount = MaxCoopPlayers,
                IsOpen = true,
                IsVisible = true,
                SceneManager = sceneManager,
                ObjectProvider = objectProvider,
            });

            if (!result.Ok)
            {
                RaiseStatus($"Host failed: {result.ShutdownReason}");
                return;
            }

            hostPlayerId = activeRunner.LocalPlayer.PlayerId;
            isStartingGame = false;
            setupSequence = 0;
            currentSetupStep = CoopSetupStep.Lobby;
            RegisterDisplayName(activeRunner.LocalPlayer.PlayerId, pendingLocalNickname, 1);
            RaiseStatus($"Hosting room: {roomName} (max {MaxCoopPlayers} players)");
            CoopLog($"Hosted room {roomName}.");
            RefreshLobbySlots();
            OnJoinedRoom?.Invoke(roomName);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RaiseStatus("Host failed. Check console.");
        }
    }

    private async Task BrowseRoomsAsync()
    {
        try
        {
            await LeaveNetworkAsync(clearSessions: true);
            remoteRoomClosedHandled = false;
            RaiseStatus("Searching for rooms...");

            var activeRunner = EnsureRunner();
            await activeRunner.JoinSessionLobby(SessionLobby.ClientServer);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RaiseStatus("Room search failed. Check console.");
        }
    }

    private async Task JoinRoomAsync(string sessionName)
    {
        if (isJoiningRoom)
            return;

        isJoiningRoom = true;

        try
        {
            RaiseStatus($"Joining {sessionName}...");

            await LeaveNetworkAsync(clearSessions: false);
            remoteRoomClosedHandled = false;

            var activeRunner = EnsureRunner();
            var result = await activeRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                SceneManager = sceneManager,
                ObjectProvider = objectProvider,
            });

            if (!result.Ok)
            {
                RaiseStatus($"Join failed: {result.ShutdownReason}");
                await BrowseRoomsAsync();
                return;
            }

            RaiseStatus($"Joined room: {sessionName}");
            CurrentRoomName = sessionName;
            isStartingGame = false;
            setupSequence = 0;
            currentSetupStep = CoopSetupStep.Lobby;
            CoopLog($"Joined room {sessionName}.");
            RefreshLobbySlots();
            OnJoinedRoom?.Invoke(sessionName);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RaiseStatus("Join failed. Check console.");
            await BrowseRoomsAsync();
        }
        finally
        {
            isJoiningRoom = false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (IsHosting)
        {
            BroadcastRoomClosed();
            await Task.Delay(100);
        }

        await LeaveNetworkAsync(clearSessions: true);
        RaiseEmptyLobbySlots();
    }

    public void RefreshLobbySlots()
    {
        if (runner == null || runner.IsRunning == false || IsInRoom == false)
        {
            RaiseEmptyLobbySlots();
            return;
        }

        List<PlayerRef> players = CollectLobbyPlayers();
        var slots = new CoopLobbySlotState[MaxCoopPlayers];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = CoopLobbySlotState.Empty;

        for (int slotIndex = 0; slotIndex < players.Count && slotIndex < MaxCoopPlayers; slotIndex++)
        {
            PlayerRef player = players[slotIndex];
            slots[slotIndex] = CoopLobbySlotState.Occupied(
                IsHostPlayer(player),
                IsPlayerReady(player.PlayerId),
                GetDisplayNameForPlayer(player.PlayerId));
        }

        OnLobbySlotsChanged?.Invoke(slots);
    }

    private List<PlayerRef> CollectLobbyPlayers()
    {
        if (runner == null)
            return new List<PlayerRef>();

        var players = new List<PlayerRef>();

        foreach (PlayerRef player in runner.ActivePlayers)
            players.Add(player);

        players.Sort(ComparePlayersForLobby);
        return players;
    }

    private int ComparePlayersForLobby(PlayerRef a, PlayerRef b)
    {
        bool aIsHost = IsHostPlayer(a);
        bool bIsHost = IsHostPlayer(b);

        if (aIsHost != bIsHost)
            return aIsHost ? -1 : 1;

        return a.PlayerId.CompareTo(b.PlayerId);
    }

    private void RaiseEmptyLobbySlots()
    {
        ClearReadyStates();

        var slots = new CoopLobbySlotState[MaxCoopPlayers];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = CoopLobbySlotState.Empty;

        OnLobbySlotsChanged?.Invoke(slots);
    }

    private void ClearReadyStates()
    {
        playerReady.Clear();
        weaponReadyPlayers.Clear();
        playerDisplayNames.Clear();
        hostPlayerId = -1;
        isStartingGame = false;
        setupSequence = 0;
        currentSetupStep = CoopSetupStep.Lobby;
        CurrentRoomName = string.Empty;
        pendingLocalNickname = string.Empty;
    }

    private void ResetSetupState()
    {
        weaponReadyPlayers.Clear();
        isStartingGame = false;
        currentSetupStep = CoopSetupStep.Lobby;
        GameSessionData.ClearGameplaySession();
    }

    private void SetPlayerReady(int playerId, bool ready)
    {
        if (playerId < 0)
            return;

        if (runner == null || runner.IsRunning == false)
            return;

        if (runner.IsServer)
        {
            ApplyPlayerReady(playerId, ready);
            BroadcastReadyState();
            return;
        }

        byte[] payload = { (byte)playerId, (byte)(ready ? 1 : 0) };
        runner.SendReliableDataToServer(ReadyToggleKey, payload);
    }

    private void ApplyPlayerReady(int playerId, bool ready)
    {
        if (playerId < 0)
            return;

        playerReady[playerId] = ready;
        CoopLog($"Lobby ready state: player {playerId} => {ready}.");
        RefreshLobbySlots();
    }

    private void BroadcastReadyState()
    {
        if (runner == null || runner.IsServer == false)
            return;

        byte[] payload = EncodeReadyState();

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
                continue;

            runner.SendReliableDataToPlayer(player, ReadyStateKey, payload);
        }
    }

    private void BroadcastRoomClosed()
    {
        if (runner == null || runner.IsServer == false)
            return;

        byte[] payload = { 1 };

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
                continue;

            runner.SendReliableDataToPlayer(player, RoomClosedKey, payload);
        }
    }

    private void BroadcastSetupStep(CoopSetupStep step)
    {
        if (runner == null || runner.IsServer == false)
            return;

        byte[] payload = { setupSequence, (byte)step };

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
                continue;

            runner.SendReliableDataToPlayer(player, SetupStepKey, payload);
        }
    }

    private void BroadcastString(ReliableKey key, string value)
    {
        if (runner == null || runner.IsServer == false)
            return;

        BroadcastToPlayers(key, EncodeStringPayload(value));
    }

    private void BroadcastSequencedString(ReliableKey key, string value)
    {
        if (runner == null || runner.IsServer == false)
            return;

        byte[] textPayload = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var payload = new byte[textPayload.Length + 1];
        payload[0] = setupSequence;
        Buffer.BlockCopy(textPayload, 0, payload, 1, textPayload.Length);
        BroadcastToPlayers(key, payload);
    }

    private byte[] EncodeStringPayload(string value)
    {
        byte[] textPayload = Encoding.UTF8.GetBytes(value ?? string.Empty);
        int textLength = Mathf.Min(textPayload.Length, 255);
        var payload = new byte[textLength + 1];
        payload[0] = (byte)textLength;

        if (textLength > 0)
            Buffer.BlockCopy(textPayload, 0, payload, 1, textLength);

        return payload;
    }

    private void BroadcastToPlayers(ReliableKey key, byte[] payload)
    {
        if (runner == null || runner.IsServer == false)
            return;

        if (payload == null || payload.Length == 0)
        {
            CoopLog($"Reliable broadcast skipped because payload is empty. Key={key}.");
            return;
        }

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
                continue;

            runner.SendReliableDataToPlayer(player, key, payload);
        }
    }

    private void SendReadyStateToPlayer(PlayerRef player)
    {
        if (runner == null || runner.IsServer == false)
            return;

        runner.SendReliableDataToPlayer(player, ReadyStateKey, EncodeReadyState());
    }

    private byte[] EncodeReadyState()
    {
        List<PlayerRef> players = CollectLobbyPlayers();
        int count = Mathf.Min(players.Count, MaxCoopPlayers);
        var payload = new byte[2 + count * 2];

        payload[0] = (byte)Mathf.Clamp(hostPlayerId, 0, 255);
        payload[1] = (byte)count;

        for (int i = 0; i < count; i++)
        {
            int offset = 2 + i * 2;
            int playerId = players[i].PlayerId;
            payload[offset] = (byte)Mathf.Clamp(playerId, 0, 255);
            payload[offset + 1] = (byte)(IsPlayerReady(playerId) ? 1 : 0);
        }

        return payload;
    }

    private void ApplyReadyStatePayload(ArraySegment<byte> data)
    {
        if (data.Count < 2)
            return;

        hostPlayerId = data.Array[data.Offset];
        playerReady.Clear();

        int count = Mathf.Min(data.Array[data.Offset + 1], MaxCoopPlayers);

        for (int i = 0; i < count; i++)
        {
            int offset = data.Offset + 2 + i * 2;
            if (offset + 1 >= data.Offset + data.Count)
                break;

            int playerId = data.Array[offset];
            playerReady[playerId] = data.Array[offset + 1] == 1;
        }

        RefreshLobbySlots();
    }

    private bool IsPlayerReady(int playerId)
    {
        return playerReady.TryGetValue(playerId, out bool ready) && ready;
    }

    private bool IsHostPlayer(PlayerRef player)
    {
        if (hostPlayerId >= 0)
            return player.PlayerId == hostPlayerId;

        return runner != null && runner.IsServer && player == runner.LocalPlayer;
    }

    private bool IsLobbyReadyToStart()
    {
        if (runner == null || runner.IsRunning == false || isStartingGame)
            return false;

        List<PlayerRef> players = CollectLobbyPlayers();
        if (players.Count < MinCoopPlayersToStart)
            return false;

        foreach (PlayerRef player in players)
        {
            if (IsPlayerReady(player.PlayerId) == false)
                return false;
        }

        return true;
    }

    private async Task StartCoopGameAsync()
    {
        if (runner == null || runner.IsRunning == false || runner.IsServer == false)
        {
            RaiseStatus("Only the host can start the COOP game.");
            CoopLog("Start setup ignored because local player is not host.");
            return;
        }

        if (currentSetupStep != CoopSetupStep.Lobby)
        {
            RaiseStatus($"COOP setup already moved to {currentSetupStep}.");
            CoopLog($"Start setup ignored. Current step is {currentSetupStep}.");
            return;
        }

        if (IsLobbyReadyToStart() == false)
        {
            RaiseStatus($"Waiting for at least {MinCoopPlayersToStart} ready players.");
            CoopLog("Start setup ignored because lobby is not ready.");
            return;
        }

        isStartingGame = true;
        weaponReadyPlayers.Clear();
        RaiseStatus("COOP setup started. Host selects the mission.");
        AdvanceSetupSequence("Host started COOP setup.");

        await Task.Yield();
        EnterCoopStep(CoopSetupStep.MissionSelection, true, "Host started COOP setup.");
    }

    private void MarkWeaponReady(int playerId)
    {
        if (playerId < 0)
            return;

        if (weaponReadyPlayers.Contains(playerId))
            return;

        weaponReadyPlayers.Add(playerId);
        GameSessionData.SetWeaponsForPlayer(playerId, GameSessionData.GetSelectedWeapons());
        RaiseStatus($"Weapon ready: {weaponReadyPlayers.Count}/{CollectLobbyPlayers().Count}");
        CoopLog($"Weapon locked: player {playerId}. Ready {weaponReadyPlayers.Count}/{CollectLobbyPlayers().Count}.");

        if (runner == null || runner.IsServer == false)
            return;

        if (AllPlayersWeaponReady() == false)
            return;

        EnterCoopStep(CoopSetupStep.Comic, true, "All players locked weapons.");
    }

    private bool AllPlayersWeaponReady()
    {
        List<PlayerRef> players = CollectLobbyPlayers();
        if (players.Count == 0)
            return false;

        foreach (PlayerRef player in players)
        {
            if (weaponReadyPlayers.Contains(player.PlayerId) == false)
                return false;
        }

        return true;
    }

    private void LogCoopPlayGame()
    {
        Debug.Log($"COOP PLAY GAME - player {runner?.LocalPlayer.PlayerId ?? -1}");
        OnCoopPlayGame?.Invoke();
    }

    private void StartCoopGameplaySceneLoad()
    {
        if (runner == null || runner.IsServer == false)
            return;

        runner.LoadScene(GameSessionData.GameplaySceneName, LoadSceneMode.Single);
    }

    private byte[] EncodePlayGamePayload(int seed)
    {
        byte[] seedBytes = System.BitConverter.GetBytes(seed);
        var payload = new byte[1 + seedBytes.Length];
        payload[0] = 1;
        Buffer.BlockCopy(seedBytes, 0, payload, 1, seedBytes.Length);
        return payload;
    }

    private bool TryDecodePlayGamePayload(ArraySegment<byte> data, out int seed)
    {
        seed = 0;

        if (data.Count < 1)
            return false;

        if (data.Count >= 5)
        {
            seed = System.BitConverter.ToInt32(data.Array, data.Offset + 1);
            return true;
        }

        return data.Array[data.Offset] == 1;
    }

    private void RegisterDisplayName(int playerId, string rawNickname, int lobbySlotOneBased)
    {
        if (playerId < 0)
            return;

        string displayName = ResolveDisplayName(rawNickname, lobbySlotOneBased);
        playerDisplayNames[playerId] = displayName;
        GameSessionData.SetDisplayNameForPlayer(playerId, displayName);

        if (runner != null && runner.LocalPlayer.PlayerId == playerId)
            GameSessionData.SetLocalDisplayName(displayName);

        if (runner != null && runner.IsServer)
            BroadcastDisplayNamesState();
    }

    private void ApplyLocalDisplayName(string displayName)
    {
        if (runner == null || runner.IsRunning == false)
            return;

        int slotOneBased = GetLobbySlotOneBased(runner.LocalPlayer.PlayerId);
        RegisterDisplayName(runner.LocalPlayer.PlayerId, displayName, slotOneBased);
    }

    private int GetLobbySlotOneBased(int playerId)
    {
        List<PlayerRef> players = CollectLobbyPlayers();

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].PlayerId == playerId)
                return i + 1;
        }

        return 1;
    }

    private string GetDisplayNameForPlayer(int playerId)
    {
        if (playerId < 0)
            return string.Empty;

        if (playerDisplayNames.TryGetValue(playerId, out string displayName))
            return displayName;

        return GameSessionData.GetDisplayNameForPlayer(playerId);
    }

    private void SendDisplayNameAnnouncement()
    {
        if (runner == null || runner.IsRunning == false)
            return;

        byte[] payload = EncodeStringPayload(pendingLocalNickname);

        if (runner.IsServer)
            HandleDisplayNameAnnounce(runner.LocalPlayer, payload);
        else
            runner.SendReliableDataToServer(DisplayNameAnnounceKey, payload);
    }

    private void HandleDisplayNameAnnounce(PlayerRef player, byte[] payload)
    {
        if (runner == null || runner.IsServer == false)
            return;

        string rawNickname = DecodeStringPayload(new ArraySegment<byte>(payload));
        int slotOneBased = GetLobbySlotOneBased(player.PlayerId);
        RegisterDisplayName(player.PlayerId, rawNickname, slotOneBased);
    }

    private void BroadcastDisplayNamesState()
    {
        if (runner == null || runner.IsServer == false)
            return;

        byte[] payload = EncodeDisplayNamesState();
        BroadcastToPlayers(DisplayNamesStateKey, payload);
        ApplyDisplayNamesState(new ArraySegment<byte>(payload));
    }

    private void SendDisplayNamesStateToPlayer(PlayerRef player)
    {
        if (runner == null || runner.IsServer == false)
            return;

        runner.SendReliableDataToPlayer(player, DisplayNamesStateKey, EncodeDisplayNamesState());
    }

    private byte[] EncodeDisplayNamesState()
    {
        List<PlayerRef> players = CollectLobbyPlayers();
        var payload = new List<byte> { (byte)Mathf.Min(players.Count, MaxCoopPlayers) };

        for (int i = 0; i < players.Count && i < MaxCoopPlayers; i++)
        {
            int playerId = players[i].PlayerId;
            string displayName = GetDisplayNameForPlayer(playerId);
            byte[] nameBytes = Encoding.UTF8.GetBytes(displayName ?? string.Empty);
            int nameLength = Mathf.Min(nameBytes.Length, 255);

            payload.Add((byte)Mathf.Clamp(playerId, 0, 255));
            payload.Add((byte)nameLength);

            if (nameLength > 0)
            {
                for (int j = 0; j < nameLength; j++)
                    payload.Add(nameBytes[j]);
            }
        }

        return payload.ToArray();
    }

    private void ApplyDisplayNamesState(ArraySegment<byte> data)
    {
        if (data.Count < 1)
            return;

        int count = Mathf.Min(data.Array[data.Offset], MaxCoopPlayers);
        int offset = data.Offset + 1;

        for (int i = 0; i < count; i++)
        {
            if (offset + 1 >= data.Offset + data.Count)
                break;

            int playerId = data.Array[offset];
            int nameLength = data.Array[offset + 1];
            offset += 2;

            if (offset + nameLength > data.Offset + data.Count)
                break;

            string displayName = nameLength > 0
                ? Encoding.UTF8.GetString(data.Array, offset, nameLength)
                : string.Empty;

            offset += nameLength;
            playerDisplayNames[playerId] = displayName;
            GameSessionData.SetDisplayNameForPlayer(playerId, displayName);

            if (runner != null && runner.LocalPlayer.PlayerId == playerId)
                GameSessionData.SetLocalDisplayName(displayName);
        }

        RefreshLobbySlots();
    }

    private void ApplyMissionSelected(string missionName)
    {
        if (string.IsNullOrEmpty(missionName))
            return;

        CoopLog($"Mission selected: {missionName}.");
        OnMissionSelected?.Invoke(missionName);
        EnterCoopStep(CoopSetupStep.WeaponSelection, false, $"Mission selected: {missionName}.");
    }

    private void EnterCoopStep(CoopSetupStep step, bool broadcast, string reason)
    {
        if (IsStaleStep(step))
        {
            CoopLog($"Stale step {step} ignored while current step is {currentSetupStep}. Reason: {reason}");
            return;
        }

        if (currentSetupStep == step)
        {
            CoopLog($"Step {step} ignored because it is already active. Reason: {reason}");
            return;
        }

        CoopSetupStep previousStep = currentSetupStep;
        currentSetupStep = step;
        CoopLog($"Step {previousStep} -> {step}. {reason}");

        if (runner != null && runner.IsServer)
            SetSessionJoinable(step == CoopSetupStep.Lobby);

        if (step == CoopSetupStep.Lobby)
        {
            ResetSetupState();
            if (runner != null && runner.IsServer)
                BroadcastReadyState();
        }

        if (broadcast)
            BroadcastSetupStep(step);

        switch (step)
        {
            case CoopSetupStep.Lobby:
                OnLobbyReturned?.Invoke();
                RefreshLobbySlots();
                break;
            case CoopSetupStep.MissionSelection:
                OnMissionSelectionStarted?.Invoke();
                break;
            case CoopSetupStep.WeaponSelection:
                weaponReadyPlayers.Clear();
                OnWeaponSelectionStarted?.Invoke();
                break;
            case CoopSetupStep.Comic:
                OnComicStarted?.Invoke();
                break;
        }
    }

    private void SetSessionJoinable(bool joinable)
    {
        if (runner == null || runner.SessionInfo == null)
            return;

        runner.SessionInfo.IsOpen = joinable;
        runner.SessionInfo.IsVisible = joinable;
        CoopLog($"Session joinable set to {joinable}.");
    }

    private void CoopLog(string message)
    {
        Debug.Log($"[COOP FLOW] {message}");
    }

    private bool IsValidStepPayload(byte value)
    {
        return Enum.IsDefined(typeof(CoopSetupStep), value);
    }

    private void AdvanceSetupSequence(string reason)
    {
        setupSequence = setupSequence == byte.MaxValue ? (byte)1 : (byte)(setupSequence + 1);
        CoopLog($"Setup sequence advanced to {setupSequence}. {reason}");
    }

    private bool TryAcceptSetupSequence(byte incomingSequence, string reason)
    {
        if (incomingSequence == 0)
            return true;

        if (setupSequence != 0 && incomingSequence < setupSequence)
        {
            CoopLog($"Stale sequence {incomingSequence} ignored. Current sequence is {setupSequence}. {reason}");
            return false;
        }

        setupSequence = incomingSequence;
        return true;
    }

    private string DecodeSequencedString(ArraySegment<byte> data, out byte incomingSequence)
    {
        incomingSequence = 0;

        if (data.Count <= 0)
            return string.Empty;

        incomingSequence = data.Array[data.Offset];
        int textOffset = data.Offset + 1;
        int textCount = data.Count - 1;
        return textCount > 0
            ? Encoding.UTF8.GetString(data.Array, textOffset, textCount)
            : string.Empty;
    }

    private string DecodeStringPayload(ArraySegment<byte> data)
    {
        if (data.Count <= 0)
            return string.Empty;

        int textCount = Mathf.Min(data.Array[data.Offset], data.Count - 1);
        if (textCount <= 0)
            return string.Empty;

        return Encoding.UTF8.GetString(data.Array, data.Offset + 1, textCount);
    }

    private bool IsStaleStep(CoopSetupStep step)
    {
        if (step == CoopSetupStep.Lobby || currentSetupStep == CoopSetupStep.Lobby)
            return false;

        return (byte)step < (byte)currentSetupStep;
    }

    private void ShutdownNetworkSync()
    {
        if (runner == null)
            return;

        isLeavingIntentionally = true;

        try
        {
            if (runner.IsRunning)
                runner.Shutdown();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            isLeavingIntentionally = false;
            runner = null;
        }
    }

    private async Task LeaveNetworkAsync(bool clearSessions)
    {
        if (clearSessions)
        {
            sessions.Clear();
            NotifySessionsChanged();
        }

        if (runner == null || runner.IsRunning == false)
            return;

        isLeavingIntentionally = true;

        try
        {
            await runner.Shutdown();
        }
        finally
        {
            isLeavingIntentionally = false;
            runner = null;
        }
    }

    private void HandleRemoteRoomClosed(string message)
    {
        if (remoteRoomClosedHandled)
            return;

        remoteRoomClosedHandled = true;
        ClearReadyStates();
        sessions.Clear();
        NotifySessionsChanged();
        RaiseEmptyLobbySlots();
        RaiseStatus(message);
        OnRoomClosed?.Invoke(message);
    }

    private NetworkRunner EnsureRunner()
    {
        if (runner == null)
        {
            runner = gameObject.AddComponent<NetworkRunner>();
            runner.AddCallbacks(this);
        }

        if (sceneManager == null)
            sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>() ?? gameObject.AddComponent<NetworkSceneManagerDefault>();

        if (objectProvider == null)
            objectProvider = gameObject.GetComponent<NetworkObjectProviderDefault>() ?? gameObject.AddComponent<NetworkObjectProviderDefault>();

        return runner;
    }

    private static bool IsJoinable(SessionInfo session)
    {
        if (!session.IsValid || !session.IsOpen || !session.IsVisible)
            return false;

        int maxPlayers = Mathf.Min(session.MaxPlayers, MaxCoopPlayers);
        return session.PlayerCount > 0 && session.PlayerCount < maxPlayers;
    }

    private void RaiseStatus(string message) => OnStatusChanged?.Invoke(message);

    private void NotifySessionsChanged() => OnSessionsChanged?.Invoke(sessions);

    public void OnSessionListUpdated(NetworkRunner activeRunner, List<SessionInfo> sessionList)
    {
        sessions.Clear();

        if (sessionList != null)
        {
            foreach (SessionInfo session in sessionList)
            {
                if (IsJoinable(session))
                    sessions.Add(session);
            }
        }

        NotifySessionsChanged();
        RaiseStatus(sessions.Count > 0
            ? $"Found {sessions.Count} joinable room(s). Tap a slot to join."
            : "No joinable rooms found.");
    }

    public void OnPlayerJoined(NetworkRunner activeRunner, PlayerRef player)
    {
        if (activeRunner.IsServer)
        {
            if (hostPlayerId < 0)
                hostPlayerId = activeRunner.LocalPlayer.PlayerId;

            if (currentSetupStep != CoopSetupStep.Lobby)
            {
                activeRunner.SendReliableDataToPlayer(player, RoomClosedKey, new byte[] { 1 });
                CoopLog($"Late join rejected for player {player.PlayerId}. Current step is {currentSetupStep}.");
                return;
            }

            ApplyPlayerReady(player.PlayerId, false);
            SendReadyStateToPlayer(player);
            SendDisplayNamesStateToPlayer(player);
            BroadcastReadyState();
        }

        RefreshLobbySlots();
    }

    public void OnPlayerLeft(NetworkRunner activeRunner, PlayerRef player)
    {
        if (activeRunner.IsServer)
        {
            playerReady.Remove(player.PlayerId);
            weaponReadyPlayers.Remove(player.PlayerId);
            BroadcastReadyState();

            if (currentSetupStep == CoopSetupStep.WeaponSelection && AllPlayersWeaponReady())
                EnterCoopStep(CoopSetupStep.Comic, true, "Remaining players already locked weapons after a player left.");
        }

        RefreshLobbySlots();
    }

    public void OnShutdown(NetworkRunner activeRunner, ShutdownReason shutdownReason)
    {
        RaiseEmptyLobbySlots();

        if (isLeavingIntentionally)
            return;

        if (activeRunner != null && activeRunner.IsServer)
            return;

        HandleRemoteRoomClosed("Host closed the COOP room.");
    }

    public void OnConnectedToServer(NetworkRunner activeRunner)
    {
        SendDisplayNameAnnouncement();
        RefreshLobbySlots();
    }

    public void OnConnectFailed(NetworkRunner activeRunner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        RaiseStatus($"Connection failed: {reason}");

        if (isJoiningRoom == false)
            _ = BrowseRoomsAsync();
    }

    public void OnDisconnectedFromServer(NetworkRunner activeRunner, NetDisconnectReason reason)
    {
        if (isLeavingIntentionally)
            return;

        HandleRemoteRoomClosed("Host disconnected. Returning to COOP menu.");
    }

    public void OnObjectExitAOI(NetworkRunner activeRunner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectEnterAOI(NetworkRunner activeRunner, NetworkObject obj, PlayerRef player) { }

    public static List<PlayerRef> GetSortedActivePlayers(NetworkRunner activeRunner)
    {
        if (activeRunner == null)
            return new List<PlayerRef>();

        if (Instance != null)
            return Instance.CollectLobbyPlayers();

        var players = new List<PlayerRef>();

        foreach (PlayerRef player in activeRunner.ActivePlayers)
            players.Add(player);

        players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return players;
    }

    public void OnInput(NetworkRunner activeRunner, NetworkInput input)
    {
        if (GameSessionData.IsCoopSession == false)
            return;

        if (SceneManager.GetActiveScene().name != GameSessionData.GameplaySceneName)
            return;

        if (ControlsManager.instance == null || ControlsManager.instance.controls == null)
            return;

        PlayerControls controls = ControlsManager.instance.controls;
        Vector2 aimScreenPosition = controls.Character.Aim.ReadValue<Vector2>();
        Vector3 aimWorldPoint = ResolveLocalAimWorldPoint(activeRunner, aimScreenPosition);
        var coopInput = new CoopPlayerInput
        {
            Movement = controls.Character.Movement.ReadValue<Vector2>(),
            AimScreenPosition = aimScreenPosition,
            AimWorldPoint = aimWorldPoint,
            Fire = controls.Character.Fire.IsPressed(),
            FirePressed = controls.Character.Fire.WasPressedThisFrame(),
            Run = controls.Character.Run.IsPressed(),
            ReloadPressed = controls.Character.Reload.WasPressedThisFrame(),
            ToggleWeaponModePressed = controls.Character.ToogleWeaponMode.WasPressedThisFrame(),
            EquipSlotPressed = ResolveCoopEquipSlotPressed(controls)
        };

        input.Set(coopInput);
    }

    private static Vector3 ResolveLocalAimWorldPoint(NetworkRunner activeRunner, Vector2 aimScreenPosition)
    {
        if (activeRunner == null)
            return Vector3.zero;

        if (activeRunner.TryGetPlayerObject(activeRunner.LocalPlayer, out NetworkObject localPlayerObject) == false)
            return Vector3.zero;

        NetworkPlayer localPlayer = localPlayerObject.GetComponent<NetworkPlayer>();
        if (localPlayer == null)
            return Vector3.zero;

        return localPlayer.ResolveAimWorldPointForInput(aimScreenPosition);
    }

    private static byte ResolveCoopEquipSlotPressed(PlayerControls controls)
    {
        if (controls.Character.EquipSlot1.WasPressedThisFrame())
            return 1;

        if (controls.Character.EquipSlot2.WasPressedThisFrame())
            return 2;

        if (controls.Character.EquipSlot3.WasPressedThisFrame())
            return 3;

        if (controls.Character.EquipSlot4.WasPressedThisFrame())
            return 4;

        if (controls.Character.EquipSlot5.WasPressedThisFrame())
            return 5;

        return 0;
    }

    public void OnInputMissing(NetworkRunner activeRunner, PlayerRef player, NetworkInput input) { }

    public void OnConnectRequest(NetworkRunner activeRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnUserSimulationMessage(NetworkRunner activeRunner, SimulationMessagePtr message) { }

    public void OnCustomAuthenticationResponse(NetworkRunner activeRunner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner activeRunner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner activeRunner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        if (key == SetupStepKey)
        {
            if (data.Count < 1)
                return;

            byte incomingSequence = data.Count >= 2 ? data.Array[data.Offset] : (byte)0;
            byte stepValue = data.Count >= 2 ? data.Array[data.Offset + 1] : data.Array[data.Offset];

            if (IsValidStepPayload(stepValue) == false)
            {
                CoopLog($"Invalid setup step payload ignored: {stepValue}.");
                return;
            }

            if (TryAcceptSetupSequence(incomingSequence, $"Setup step {stepValue}.") == false)
                return;

            EnterCoopStep((CoopSetupStep)stepValue, false, $"Remote step received: {(CoopSetupStep)stepValue}.");
            return;
        }

        if (key == MissionSelectedKey)
        {
            string missionName = DecodeSequencedString(data, out byte incomingSequence);
            if (TryAcceptSetupSequence(incomingSequence, $"Mission selected {missionName}.") == false)
                return;

            ApplyMissionSelected(missionName);
            return;
        }

        if (key == MissionPreviewKey)
        {
            string missionName = DecodeStringPayload(data);
            OnMissionPreviewed?.Invoke(missionName);
            return;
        }

        if (key == WeaponReadyKey)
        {
            if (activeRunner.IsServer == false || data.Count < 1)
                return;

            int playerId = data.Array[data.Offset];
            if (player.PlayerId != playerId)
                return;

            MarkWeaponReady(playerId);
            return;
        }

        if (key == PlayGameKey)
        {
            if (TryDecodePlayGamePayload(data, out int seed))
                GameSessionData.BeginCoopGameplaySession(seed);

            LogCoopPlayGame();
            return;
        }

        if (key == DisplayNameAnnounceKey)
        {
            if (activeRunner.IsServer == false || data.Count < 1)
                return;

            var payload = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, payload, 0, data.Count);
            HandleDisplayNameAnnounce(player, payload);
            return;
        }

        if (key == DisplayNamesStateKey)
        {
            if (activeRunner.IsServer)
                return;

            ApplyDisplayNamesState(data);
            return;
        }

        if (key == RoomClosedKey)
        {
            if (activeRunner.IsServer)
                return;

            HandleRemoteRoomClosed("Host closed the COOP room.");
            _ = DisconnectAsync();
            return;
        }

        if (key == ReadyToggleKey)
        {
            if (activeRunner.IsServer == false || data.Count < 2)
                return;

            int playerId = data.Array[data.Offset];
            bool ready = data.Array[data.Offset + 1] == 1;

            if (player.PlayerId != playerId)
                return;

            ApplyPlayerReady(playerId, ready);
            BroadcastReadyState();
            return;
        }

        if (key == ReadyStateKey)
        {
            if (activeRunner.IsServer)
                return;

            ApplyReadyStatePayload(data);
        }
    }

    public void OnReliableDataProgress(NetworkRunner activeRunner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner activeRunner)
    {
        activeRunner.ProvideInput = true;
        RaiseStatus("COOP gameplay scene loaded.");
        CoopGameplayBridge.HandleSceneLoadDone(activeRunner);
    }

    public void OnSceneLoadStart(NetworkRunner activeRunner) { }
}
