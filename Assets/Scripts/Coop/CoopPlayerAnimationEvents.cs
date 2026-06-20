using UnityEngine;

public class CoopPlayerAnimationEvents : MonoBehaviour
{
    private CoopPlayerPresentation presentation;
    private NetworkPlayerWeapon weaponController;

    private void Awake()
    {
        presentation = GetComponentInParent<CoopPlayerPresentation>();
        weaponController = GetComponentInParent<NetworkPlayerWeapon>();
    }

    public void ReloadIsOver()
    {
        presentation?.RestoreLocalRigAfterReload();
        presentation?.StopReloadSfx();
        weaponController?.ReloadIsOver();
    }

    public void ReturnRig()
    {
        presentation?.RestoreLocalRigFromAnimationEvent();
    }

    public void WeaponEquipingIsOver()
    {
        weaponController?.WeaponEquipingIsOver();
    }

    public void SwitchOnWeaponModel()
    {
        weaponController?.SwitchOnWeaponModel();
    }
}
