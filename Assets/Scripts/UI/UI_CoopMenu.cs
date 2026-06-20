using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_CoopMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private GameObject hostLobbyPanel;
    [SerializeField] private GameObject joinRoomPanel;

    [Header("Host Lobby")]
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private Image readyButtonImage;

    [Header("Mode Selection")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("Join Room")]
    [SerializeField] private TextMeshProUGUI roomStatusText;

    private TextMeshProUGUI hostStatusText;
    private readonly TextMeshProUGUI[] roomSlotLabels = new TextMeshProUGUI[3];
    private readonly Button[] roomSlotButtons = new Button[3];
    private readonly PlayerSlotView[] playerSlotViews = new PlayerSlotView[CoopNetworkManager.MaxCoopPlayers];

    private bool localPlayerReady;

    private sealed class PlayerSlotView
    {
        public TextMeshProUGUI emptyLabel;
        public TextMeshProUGUI waitingLabel;
        public TextMeshProUGUI readyLabel;
        public TextMeshProUGUI hostLabel;
        public Image backgroundImage;
    }

    private void Awake()
    {
        CacheHostStatusText();
        EnsureNicknameInput();
        SetupPlayerSlots();
        SetupRoomSlots();
        AssignButtonActions();
        ResetPlayerSlotVisuals();
    }

    private void OnEnable()
    {
        CoopNetworkManager.Instance.OnStatusChanged += HandleNetworkStatus;
        CoopNetworkManager.Instance.OnSessionsChanged += HandleSessionsChanged;
        CoopNetworkManager.Instance.OnJoinedRoom += HandleJoinedRoom;
        CoopNetworkManager.Instance.OnLobbySlotsChanged += HandleLobbySlotsChanged;
        CoopNetworkManager.Instance.OnRoomClosed += HandleRoomClosed;
        ShowModeSelection();
    }

    private void OnDisable()
    {
        if (CoopNetworkManager.Instance == null)
            return;

        CoopNetworkManager.Instance.OnStatusChanged -= HandleNetworkStatus;
        CoopNetworkManager.Instance.OnSessionsChanged -= HandleSessionsChanged;
        CoopNetworkManager.Instance.OnJoinedRoom -= HandleJoinedRoom;
        CoopNetworkManager.Instance.OnLobbySlotsChanged -= HandleLobbySlotsChanged;
        CoopNetworkManager.Instance.OnRoomClosed -= HandleRoomClosed;
    }

    public static UI_CoopMenu EnsureCreated(UI ui)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null || ui == null)
            return null;

        Transform coopRoot = canvas.transform.Find("Coop_UI");
        if (coopRoot == null)
            return null;

        ui.RegisterRuntimeUIElement(coopRoot.gameObject);
        return coopRoot.GetComponent<UI_CoopMenu>();
    }

    public void ShowRoot()
    {
        if (UI.instance != null)
            UI.instance.SwitchTo(gameObject);
    }

    public void ShowMainMenu()
    {
        CoopNetworkManager.Instance.Disconnect();

        if (UI.instance != null && mainMenuPanel != null)
            UI.instance.SwitchTo(mainMenuPanel);
    }

    public void ShowModeSelection()
    {
        SetActivePanel(modeSelectionPanel);
        localPlayerReady = false;
        RefreshReadyVisual();
        ResetPlayerSlotVisuals();
    }

    public void ShowHostLobby()
    {
        CaptureLocalNickname();
        CoopNetworkManager.Instance.CommitLocalDisplayNameForHost();
        SetActivePanel(hostLobbyPanel);
        localPlayerReady = false;
        RefreshReadyVisual();
        UpdateHostStatus("Creating room...");
        CoopNetworkManager.Instance.HostGame();
    }

    public void EnterClientLobby(string roomName)
    {
        SetActivePanel(hostLobbyPanel);
        localPlayerReady = false;
        RefreshReadyVisual();
        UpdateHostStatus($"In room: {roomName}");
        CoopNetworkManager.Instance.RefreshLobbySlots();
    }

    public void ShowExistingLobby()
    {
        SetActivePanel(hostLobbyPanel);
        RefreshReadyVisual();

        string roomName = CoopNetworkManager.Instance != null
            ? CoopNetworkManager.Instance.CurrentRoomName
            : string.Empty;

        if (CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.IsHosting)
            UpdateHostStatus($"Hosting room: {roomName} (max {CoopNetworkManager.MaxCoopPlayers} players)");
        else
            UpdateHostStatus(string.IsNullOrEmpty(roomName) ? "In COOP room" : $"In room: {roomName}");

        CoopNetworkManager.Instance.RefreshLobbySlots();
    }

    public void ShowJoinRoom()
    {
        CaptureLocalNickname();
        SetActivePanel(joinRoomPanel);
        UpdateRoomStatus("Searching for rooms...");
        CoopNetworkManager.Instance.BrowseRooms();
    }

    public void ToggleReady()
    {
        CoopNetworkManager.Instance.ToggleLocalReady();
    }

    public void RefreshRooms()
    {
        UpdateRoomStatus("Refreshing...");
        CoopNetworkManager.Instance.BrowseRooms();
    }

    private void BackToModeSelection()
    {
        CoopNetworkManager.Instance.Disconnect();
        ShowModeSelection();
        UpdateRoomStatus("COOP room closed.");
    }

    private void SetActivePanel(GameObject activePanel)
    {
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(modeSelectionPanel == activePanel);

        if (hostLobbyPanel != null)
            hostLobbyPanel.SetActive(hostLobbyPanel == activePanel);

        if (joinRoomPanel != null)
            joinRoomPanel.SetActive(joinRoomPanel == activePanel);
    }

    private void RefreshReadyVisual()
    {
        localPlayerReady = CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.IsLocalPlayerReady();
        bool canStart = CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.CanLocalPlayerStartGame;

        if (readyButtonText != null)
            readyButtonText.text = canStart ? "START" : localPlayerReady ? "READY" : "NOT READY";

        if (readyButtonImage != null)
            readyButtonImage.color = canStart ? Color.yellow : localPlayerReady ? Color.green : Color.white;
    }

    private void UpdateRoomStatus(string message)
    {
        if (roomStatusText != null)
            roomStatusText.text = message;
    }

    private void UpdateHostStatus(string message)
    {
        if (hostStatusText != null)
            hostStatusText.text = message;
    }

    private void HandleNetworkStatus(string message)
    {
        if (hostLobbyPanel != null && hostLobbyPanel.activeSelf)
            UpdateHostStatus(message);

        if (joinRoomPanel != null && joinRoomPanel.activeSelf)
            UpdateRoomStatus(message);
    }

    private void HandleJoinedRoom(string roomName)
    {
        if (CoopNetworkManager.Instance.IsHosting)
            UpdateHostStatus($"Hosting room: {roomName} (max {CoopNetworkManager.MaxCoopPlayers} players)");
        else
            EnterClientLobby(roomName);
    }

    private void HandleRoomClosed(string message)
    {
        ShowModeSelection();
        UpdateRoomStatus(message);
        UpdateHostStatus(message);
    }

    private void HandleLobbySlotsChanged(CoopLobbySlotState[] slotStates)
    {
        if (hostLobbyPanel == null || hostLobbyPanel.activeSelf == false)
            return;

        int occupiedCount = 0;

        for (int i = 0; i < playerSlotViews.Length; i++)
        {
            CoopLobbySlotState slotState = i < slotStates.Length
                ? slotStates[i]
                : CoopLobbySlotState.Empty;

            ApplyPlayerSlotVisual(i, slotState);

            if (slotState.Presence == CoopSlotPresence.Occupied)
                occupiedCount++;
        }

        int maxPlayers = CoopNetworkManager.MaxCoopPlayers;
        if (CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.CanLocalPlayerStartGame)
            UpdateHostStatus("All players are ready. Host can start the game.");
        else
            UpdateHostStatus($"Players in lobby: {occupiedCount}/{maxPlayers}. Waiting for ready players.");

        RefreshReadyVisual();
    }

    private void ApplyPlayerSlotVisual(int slotIndex, CoopLobbySlotState slotState)
    {
        PlayerSlotView view = playerSlotViews[slotIndex];
        if (view == null)
            return;

        bool occupied = slotState.Presence == CoopSlotPresence.Occupied;

        if (view.hostLabel != null)
            view.hostLabel.gameObject.SetActive(occupied && slotState.IsHost);

        if (view.emptyLabel != null)
        {
            if (occupied && string.IsNullOrEmpty(slotState.DisplayName) == false)
            {
                view.emptyLabel.gameObject.SetActive(true);
                view.emptyLabel.text = slotState.DisplayName;
            }
            else
            {
                view.emptyLabel.gameObject.SetActive(!occupied);
                if (!occupied)
                    view.emptyLabel.text = "EMPTY";
            }
        }

        TextMeshProUGUI statusLabel = view.readyLabel != null ? view.readyLabel : view.waitingLabel;

        if (!occupied)
        {
            if (view.readyLabel != null)
                view.readyLabel.gameObject.SetActive(false);

            if (view.waitingLabel != null)
            {
                view.waitingLabel.gameObject.SetActive(true);
                view.waitingLabel.text = "WAITING";
                view.waitingLabel.color = Color.white;
            }

            if (view.backgroundImage != null)
            {
                Color color = view.backgroundImage.color;
                color.a = 0.55f;
                view.backgroundImage.color = color;
            }

            return;
        }

        if (view.waitingLabel != null && view.readyLabel != null)
            view.waitingLabel.gameObject.SetActive(false);

        if (statusLabel != null)
        {
            statusLabel.gameObject.SetActive(true);
            statusLabel.text = slotState.IsReady ? "READY" : "NOT READY";
            statusLabel.color = slotState.IsReady ? Color.green : Color.white;
        }

        if (view.backgroundImage != null)
        {
            Color color = view.backgroundImage.color;
            color.a = 0.7f;
            view.backgroundImage.color = color;
        }
    }

    private void ResetPlayerSlotVisuals()
    {
        for (int i = 0; i < playerSlotViews.Length; i++)
            ApplyPlayerSlotVisual(i, CoopLobbySlotState.Empty);
    }

    private void HandleSessionsChanged(IReadOnlyList<SessionInfo> sessionList)
    {
        for (int i = 0; i < roomSlotLabels.Length; i++)
        {
            if (roomSlotLabels[i] == null)
                continue;

            if (i < sessionList.Count)
            {
                SessionInfo session = sessionList[i];
                int maxPlayers = Mathf.Min(session.MaxPlayers, CoopNetworkManager.MaxCoopPlayers);
                roomSlotLabels[i].text = $"{session.Name} ({session.PlayerCount}/{maxPlayers})";

                if (roomSlotButtons[i] != null)
                    roomSlotButtons[i].interactable = session.PlayerCount > 0 && session.PlayerCount < maxPlayers;
            }
            else
            {
                roomSlotLabels[i].text = "-";

                if (roomSlotButtons[i] != null)
                    roomSlotButtons[i].interactable = false;
            }
        }
    }

    private void CacheHostStatusText()
    {
        if (hostLobbyPanel == null)
            return;

        hostStatusText = hostLobbyPanel.transform.Find("InfoText")?.GetComponent<TextMeshProUGUI>();
    }

    private void SetupPlayerSlots()
    {
        if (hostLobbyPanel == null)
            return;

        Transform playerSlotsRoot = hostLobbyPanel.transform.Find("PlayerSlots");
        if (playerSlotsRoot == null)
            return;

        for (int i = 0; i < playerSlotViews.Length; i++)
        {
            Transform slot = playerSlotsRoot.Find($"Slot - PLAYER {i + 1}");
            if (slot == null)
                continue;

            playerSlotViews[i] = new PlayerSlotView
            {
                emptyLabel = FindChildText(slot, "Text - EMPTY"),
                waitingLabel = FindChildText(slot, "Text - WAITING"),
                readyLabel = FindChildText(slot, "Text - READY"),
                hostLabel = FindChildText(slot, "Text - HOST"),
                backgroundImage = slot.GetComponent<Image>(),
            };
        }
    }

    private static TextMeshProUGUI FindChildText(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private void SetupRoomSlots()
    {
        if (joinRoomPanel == null)
            return;

        for (int i = 0; i < roomSlotLabels.Length; i++)
        {
            Transform slot = joinRoomPanel.transform.Find($"RoomList/Room slot {i + 1}");
            if (slot == null)
                continue;

            roomSlotLabels[i] = slot.GetComponentInChildren<TextMeshProUGUI>(true);

            Button button = slot.GetComponent<Button>();
            if (button == null)
                button = slot.gameObject.AddComponent<Button>();

            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = slot.GetComponent<Image>();
            button.interactable = false;

            int roomIndex = i;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => CoopNetworkManager.Instance.JoinRoomAt(roomIndex));

            roomSlotButtons[i] = button;
        }
    }

    private void AssignButtonActions()
    {
        AssignButton("Button - Host", ShowHostLobby);
        AssignButton("Button - Join", ShowJoinRoom);
        AssignButton("Button - Ready", ToggleReady);
        AssignButton("Button - Refresh", RefreshRooms);
        AssignBackButtonActions();
    }

    private void AssignButton(string buttonName, UnityEngine.Events.UnityAction action)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name != buttonName)
                continue;

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }
    }

    private void AssignBackButtonActions()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name != "Button - Back")
                continue;

            button.onClick = new Button.ButtonClickedEvent();

            if (modeSelectionPanel != null && button.transform.IsChildOf(modeSelectionPanel.transform))
                button.onClick.AddListener(ShowMainMenu);
            else
                button.onClick.AddListener(BackToModeSelection);
        }
    }

    private void EnsureNicknameInput()
    {
        if (nicknameInput != null || modeSelectionPanel == null)
            return;

        Transform existing = modeSelectionPanel.transform.Find("InputField - Nickname");
        if (existing != null)
        {
            nicknameInput = existing.GetComponent<TMP_InputField>();
            return;
        }

        nicknameInput = CreateNicknameInputField();
    }

    private TMP_InputField CreateNicknameInputField()
    {
        var inputRoot = new GameObject("InputField - Nickname", typeof(RectTransform));
        inputRoot.transform.SetParent(modeSelectionPanel.transform, false);

        RectTransform rootRect = inputRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -265f);
        rootRect.sizeDelta = new Vector2(420f, 56f);

        Image background = inputRoot.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);

        var textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(inputRoot.transform, false);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(12f, 8f);
        textAreaRect.offsetMax = new Vector2(-12f, -8f);

        var placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
        placeholderObject.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Nickname (optional)";
        placeholder.fontSize = 24f;
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_InputField inputField = inputRoot.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 16;
        return inputField;
    }

    private void CaptureLocalNickname()
    {
        string rawNickname = nicknameInput != null ? nicknameInput.text : string.Empty;
        CoopNetworkManager.Instance.SetPendingLocalNickname(rawNickname);
    }

    public void PrepareForNetworkConnect()
    {
        CaptureLocalNickname();
    }
}
