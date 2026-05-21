using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationEvens : MonoBehaviour
{
    private PlayerWeaponVisual visualController;

    private void Start()
    {
        visualController = GetComponentInParent<PlayerWeaponVisual>();
    }

    private void ReloadIsOver()
    {
        visualController.MaximizeRigWeight();

    }

    public void ReturnRig()
    {
        visualController.MaximizeRigWeight();
        visualController.MaximizeLeftHandWeight();
    }

    public void WeaponGrapIsOver()
    {
        visualController.SetBusyGrabbingWeapon(false);
    }
}
