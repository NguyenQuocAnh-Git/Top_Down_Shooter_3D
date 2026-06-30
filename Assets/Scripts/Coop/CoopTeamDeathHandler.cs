using Fusion;
using UnityEngine;

public class CoopTeamDeathHandler : MonoBehaviour
{
    private NetworkRunner runner;
    private NetworkPlayer localPlayer;
    private bool cameraHandedOff;
    private bool wipeReported;

    public void Initialize(NetworkRunner activeRunner)
    {
        runner = activeRunner;
    }

    private void Update()
    {
        if (runner == null || runner.IsRunning == false)
            return;

        NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
        if (players.Length == 0)
            return;

        if (localPlayer == null)
        {
            foreach (NetworkPlayer player in players)
            {
                if (player.Object != null && player.Object.HasInputAuthority)
                {
                    localPlayer = player;
                    break;
                }
            }
        }

        if (localPlayer != null && localPlayer.Health != null && localPlayer.Health.IsDead && cameraHandedOff == false)
        {
            NetworkPlayer teammate = FirstAlivePlayer(players, localPlayer);
            if (teammate != null && CameraManager.instance != null)
            {
                CameraManager.instance.ChangeCameraTarget(teammate.transform);
                cameraHandedOff = true;
            }
        }

        if (wipeReported == false && AllActivePlayersDead())
        {
            if (runner.IsServer)
            {
                if (CoopMissionSync.Instance == null)
                    return;

                CoopMissionSync.Instance.ConfirmTeamWipe();
            }
            else
                CoopNetworkManager.Instance.SendCoopTeamWipeReport();

            wipeReported = true;
        }
    }

    private static NetworkPlayer FirstAlivePlayer(NetworkPlayer[] players, NetworkPlayer excluded)
    {
        foreach (NetworkPlayer player in players)
        {
            if (player != excluded && player.Health != null && player.Health.IsDead == false)
                return player;
        }

        return null;
    }

    private bool AllActivePlayersDead()
    {
        int activePlayerCount = 0;

        foreach (PlayerRef playerRef in runner.ActivePlayers)
        {
            activePlayerCount++;

            if (runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) == false
                || playerObject == null)
                return false;

            NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
            if (health == null || health.IsDead == false)
                return false;
        }

        return activePlayerCount > 0;
    }
}
