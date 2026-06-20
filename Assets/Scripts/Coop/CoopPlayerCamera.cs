using UnityEngine;

public static class CoopPlayerCamera
{
    public static void BindLocalPlayer(NetworkPlayer player)
    {
        if (player == null || player.Object == null || player.Object.HasInputAuthority == false)
            return;

        Transform cameraTarget = player.CameraTarget != null ? player.CameraTarget : player.transform;

        if (CameraManager.instance != null)
            CameraManager.instance.ChangeCameraTarget(cameraTarget);
    }
}
