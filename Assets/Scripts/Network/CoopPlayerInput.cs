using Fusion;
using UnityEngine;

public struct CoopPlayerInput : INetworkInput
{
    public Vector2 Movement;
    public Vector2 AimScreenPosition;
    public Vector3 AimWorldPoint;
    public NetworkBool Fire;
    public NetworkBool FirePressed;
    public NetworkBool Run;
    public NetworkBool ReloadPressed;
    public NetworkBool ToggleWeaponModePressed;
    public byte EquipSlotPressed;
}
