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

        if (wipeReported == false && AllPlayersDead(players))
        {
            wipeReported = true;

            if (runner.IsServer)
                CoopMissionSync.Instance?.ConfirmTeamWipe();
            else
                CoopNetworkManager.Instance.SendCoopTeamWipeReport();
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

    private static bool AllPlayersDead(NetworkPlayer[] players)
    {
        foreach (NetworkPlayer player in players)
        {
            if (player.Health == null || player.Health.IsDead == false)
                return false;
        }

        return true;
    }
}
