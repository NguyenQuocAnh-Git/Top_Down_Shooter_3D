using System;
using System.Text;
using Fusion;
using UnityEngine;

public class CoopMissionSync : MonoBehaviour
{
    private const float MissionBroadcastInterval = 0.2f;
    private const float KeyPickupRange = 2.5f;
    private const float CarDeliveryTolerance = 2f;

    public static CoopMissionSync Instance { get; private set; }

    private NetworkRunner runner;
    private bool matchFinished;
    private float nextMissionBroadcastTime;
    private string pendingMissionTitle = string.Empty;
    private string pendingMissionDetails = string.Empty;

    public bool IsHost => runner != null && runner.IsServer;

    public void Initialize(NetworkRunner activeRunner)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        runner = activeRunner;

        CoopNetworkManager manager = CoopNetworkManager.Instance;
        manager.OnCoopMissionStateReceived += ApplyMissionState;
        manager.OnCoopTeamWipeReported += HandleTeamWipeReport;
        manager.OnCoopExtractionReached += HandleExtractionReached;
        manager.OnCoopKeyPickupRequested += HandleKeyPickupRequest;
        manager.OnCoopKeyRemoved += HandleKeyRemoved;
        manager.OnCoopCarDelivered += HandleCarDelivered;
    }

    public void StartHostMission()
    {
        if (IsHost == false || MissionManager.instance == null || MissionManager.instance.currentMission == null)
            return;

        MissionManager.instance.StartMission();
    }

    public void PublishMissionInfo(string title, string details)
    {
        if (IsHost == false || matchFinished)
            return;

        pendingMissionTitle = title ?? string.Empty;
        pendingMissionDetails = details ?? string.Empty;

        if (Time.unscaledTime < nextMissionBroadcastTime)
            return;

        nextMissionBroadcastTime = Time.unscaledTime + MissionBroadcastInterval;
        CoopNetworkManager.Instance.BroadcastCoopMissionState(EncodeStrings(pendingMissionTitle, pendingMissionDetails));
    }

    public void TryCompleteAtExtraction(NetworkPlayer player)
    {
        if (IsHost == false || matchFinished || player == null || player.Health == null || player.Health.IsDead)
            return;

        if (MissionManager.instance != null && MissionManager.instance.MissionCompleted())
            ConfirmMatchResult(true);
    }

    public void ConfirmTeamWipe()
    {
        if (IsHost)
            ConfirmMatchResult(false);
    }

    public void ConfirmMissionFailure()
    {
        if (IsHost)
            ConfirmMatchResult(false);
    }

    private void HandleTeamWipeReport(int reporterPlayerId)
    {
        if (IsHost == false || matchFinished)
            return;

        if (AllNetworkPlayersDead())
            ConfirmMatchResult(false);
    }

    private void HandleExtractionReached(int playerId)
    {
        if (IsHost == false || matchFinished)
            return;

        TryCompleteAtExtraction(FindNetworkPlayer(playerId));
    }

    private void HandleKeyPickupRequest(int playerId, Vector3 keyPosition)
    {
        if (IsHost == false || matchFinished)
            return;

        NetworkPlayer player = FindNetworkPlayer(playerId);
        if (player == null || player.Health == null || player.Health.IsDead)
            return;

        MissionObject_Key key = FindNearestKey(keyPosition, KeyPickupRange);
        if (key == null)
            return;

        if (Vector3.Distance(player.transform.position, key.transform.position) > KeyPickupRange)
            return;

        key.CompletePickupFromHost();
    }

    private void HandleKeyRemoved(Vector3 position)
    {
        MissionObject_Key key = FindNearestKey(position, KeyPickupRange);
        if (key != null)
            key.HideWithoutMissionEvent();
    }

    private void HandleCarDelivered(Vector3 carPosition)
    {
        if (IsHost == false || matchFinished)
            return;

        MissionObject_CarToDeliver carMission = FindNearestCarMission(carPosition, CarDeliveryTolerance);
        carMission?.InvokeOnCarDelivery();
    }

    private void ConfirmMatchResult(bool victory)
    {
        if (matchFinished)
            return;

        matchFinished = true;
        CoopNetworkManager.Instance.BroadcastCoopMatchResult(victory);
    }

    private void ApplyMissionState(byte[] payload)
    {
        if (IsHost || TryDecodeStrings(payload, out string title, out string details) == false)
            return;

        UI.instance?.inGameUI?.SetMissionInfoFromNetwork(title, details);
    }

    private static NetworkPlayer FindNetworkPlayer(int playerId)
    {
        foreach (NetworkPlayer player in FindObjectsOfType<NetworkPlayer>())
        {
            if (player.Object != null && player.Object.InputAuthority.PlayerId == playerId)
                return player;
        }

        return null;
    }

    private static bool AllNetworkPlayersDead()
    {
        NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
        if (players.Length == 0)
            return false;

        foreach (NetworkPlayer player in players)
        {
            if (player.Health == null || player.Health.IsDead == false)
                return false;
        }

        return true;
    }

    private static MissionObject_Key FindNearestKey(Vector3 position, float maxDistance)
    {
        MissionObject_Key nearest = null;
        float nearestDistance = maxDistance;

        foreach (MissionObject_Key key in FindObjectsOfType<MissionObject_Key>())
        {
            float distance = Vector3.Distance(position, key.transform.position);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = key;
            }
        }

        return nearest;
    }

    private static MissionObject_CarToDeliver FindNearestCarMission(Vector3 position, float maxDistance)
    {
        MissionObject_CarToDeliver nearest = null;
        float nearestDistance = maxDistance;

        foreach (MissionObject_CarToDeliver carMission in FindObjectsOfType<MissionObject_CarToDeliver>())
        {
            float distance = Vector3.Distance(position, carMission.transform.position);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = carMission;
            }
        }

        return nearest;
    }

    private static byte[] EncodeStrings(string title, string details)
    {
        byte[] titleBytes = Encoding.UTF8.GetBytes(title ?? string.Empty);
        byte[] detailBytes = Encoding.UTF8.GetBytes(details ?? string.Empty);
        int titleLength = Mathf.Min(titleBytes.Length, ushort.MaxValue);
        int detailLength = Mathf.Min(detailBytes.Length, ushort.MaxValue);
        byte[] payload = new byte[4 + titleLength + detailLength];
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)titleLength), 0, payload, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)detailLength), 0, payload, 2, 2);
        Buffer.BlockCopy(titleBytes, 0, payload, 4, titleLength);
        Buffer.BlockCopy(detailBytes, 0, payload, 4 + titleLength, detailLength);
        return payload;
    }

    private static bool TryDecodeStrings(byte[] payload, out string title, out string details)
    {
        title = string.Empty;
        details = string.Empty;
        if (payload == null || payload.Length < 4)
            return false;

        int titleLength = BitConverter.ToUInt16(payload, 0);
        int detailLength = BitConverter.ToUInt16(payload, 2);
        if (4 + titleLength + detailLength > payload.Length)
            return false;

        title = Encoding.UTF8.GetString(payload, 4, titleLength);
        details = Encoding.UTF8.GetString(payload, 4 + titleLength, detailLength);
        return true;
    }

    private void OnDestroy()
    {
        CoopNetworkManager manager = CoopNetworkManager.Instance;
        if (manager != null)
        {
            manager.OnCoopMissionStateReceived -= ApplyMissionState;
            manager.OnCoopTeamWipeReported -= HandleTeamWipeReport;
            manager.OnCoopExtractionReached -= HandleExtractionReached;
            manager.OnCoopKeyPickupRequested -= HandleKeyPickupRequest;
            manager.OnCoopKeyRemoved -= HandleKeyRemoved;
            manager.OnCoopCarDelivered -= HandleCarDelivered;
        }

        if (Instance == this)
            Instance = null;
    }
}
