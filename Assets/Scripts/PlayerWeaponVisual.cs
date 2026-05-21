using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerWeaponVisual : MonoBehaviour
{
    private Animator anim;
    private bool isGrabbingWeapon;

    #region Gun tranforms 
    [SerializeField] private Transform[] gunTranforms;

    [SerializeField] private Transform pistol;
    [SerializeField] private Transform revolver;
    [SerializeField] private Transform autoRifle;
    [SerializeField] private Transform shotGun;
    [SerializeField] private Transform rifle;
    private Transform currentGun;
    #endregion

    [Header("Rig")]
    [SerializeField] private float rigWeightIncreaseRate = 2.75f; 
    private bool shouldIncreaseRigWieght = true;
    private Rig rig;

    [Header("Left hand IK")]
    [SerializeField] private float leftHandIKWeightIncreaseRate;
    [SerializeField] private TwoBoneIKConstraint leftHandIK;
    [SerializeField] private Transform leftHandIK_Target;
    private bool shouldIncrease_LeftHandIKWieght;



    private void Start()
    {
        SwitchOnGun(pistol);

        anim = GetComponentInChildren<Animator>();
        rig =GetComponentInChildren<Rig>();
    }

    private void Update()
    {
        CheckWeaponSwitch();


        if (Input.GetKeyDown(KeyCode.R) && isGrabbingWeapon == false)
        {
            anim.SetTrigger("Reload");
            ReduceRigWeight();
        }

        UpdateRigWight();

        UpdateLeftHandIKWeight();
    }

    private void UpdateLeftHandIKWeight()
    {
        if (shouldIncrease_LeftHandIKWieght)
        {
            leftHandIK.weight += leftHandIKWeightIncreaseRate * Time.deltaTime;
            if (leftHandIK.weight >= 1)
                shouldIncrease_LeftHandIKWieght = false;
        }
    }

    private void UpdateRigWight()
    {
        if (shouldIncreaseRigWieght)
        {
            rig.weight += rigWeightIncreaseRate * Time.deltaTime;
            if (rig.weight >= 1)
                shouldIncreaseRigWieght = false;
        }
    }

    private void ReduceRigWeight()
    {
        rig.weight = .15f;
    }

    private void PlayerWeaponGrapAnimation( GrapType grapType)
    {
        leftHandIK.weight = 0;
        ReduceRigWeight();
        anim.SetFloat("WeaponGrapType", (float)grapType);
        anim.SetTrigger("Grap");

        SetBusyGrabbingWeapon(true);
    }

    public void SetBusyGrabbingWeapon(bool busy)
    {
        isGrabbingWeapon = busy;
        anim.SetBool("IsWeaponGaping", isGrabbingWeapon);
    }

    public void MaximizeRigWeight() => shouldIncreaseRigWieght = true;
    public void MaximizeLeftHandWeight() => shouldIncrease_LeftHandIKWieght = true;
    private void SwitchOnGun(Transform gunTranform)
    {
        SwitchOffGun();
        gunTranform.gameObject.SetActive(true);
        currentGun = gunTranform;

        AttachLeftHand();
    }

    private void SwitchOffGun()
    {
        for(int i = 0; i< gunTranforms.Length; i++)
        {
            gunTranforms[i].gameObject.SetActive(false);
        }
    }

    private void AttachLeftHand()
    {
        Transform targetTransform = currentGun.GetComponentInChildren<LeftHandTargetTransform>().transform;

        leftHandIK_Target.localPosition = targetTransform.localPosition;
        leftHandIK_Target.localRotation = targetTransform.localRotation;
    }

    private void SwitchAnimationLayer(int layerMask)
    {
        for(int i = 1; i< anim.layerCount; i++)
        {
            anim.SetLayerWeight(i, 0);
        }
        anim.SetLayerWeight(layerMask, 1);
    }



    private void CheckWeaponSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SwitchOnGun(pistol);
            SwitchAnimationLayer(1);
            PlayerWeaponGrapAnimation(GrapType.SideGrab);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SwitchOnGun(revolver);
            SwitchAnimationLayer(1);
            PlayerWeaponGrapAnimation(GrapType.SideGrab);

        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SwitchOnGun(autoRifle);
            SwitchAnimationLayer(1);
            PlayerWeaponGrapAnimation(GrapType.SideGrab);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SwitchOnGun(shotGun);
            SwitchAnimationLayer(2);
            PlayerWeaponGrapAnimation(GrapType.BackGrap);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SwitchOnGun(rifle);
            SwitchAnimationLayer(3);
            PlayerWeaponGrapAnimation(GrapType.BackGrap);
        }
    }
}

public enum GrapType { SideGrab, BackGrap };