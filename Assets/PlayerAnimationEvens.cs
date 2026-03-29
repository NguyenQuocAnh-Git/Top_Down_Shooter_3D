using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationEvens : MonoBehaviour
{
    private WeaponVisualController visualController;

    private void Start()
    {
        visualController = GetComponentInParent<WeaponVisualController>();
    }

    private void ReloadIsOver()
    {
        visualController.ReturnRigWeightOne();

    }

    public void ReturnRig()
    {
        visualController.ReturnRigWeightOne();
        visualController.ReturnWeightToLeftHandIK();
    }

    public void WeaponGrapIsOver()
    {
        visualController.SetBusyGrabbingWeapon(false);
    }
}
