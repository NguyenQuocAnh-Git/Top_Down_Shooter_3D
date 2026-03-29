using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class WeaponVisualController : MonoBehaviour
{
    private Animator anim;

    [SerializeField] private Transform[] gunTranforms;

    [SerializeField] private Transform pistol;
    [SerializeField] private Transform revolver;
    [SerializeField] private Transform autoRifle;
    [SerializeField] private Transform shotGun;
    [SerializeField] private Transform rifle;

    private Transform currentGun;

    [Header("Rig")]
    [SerializeField] private float rigIncreaseStep = 2.75f; 
    private bool rigShouldBeIncrease = true;

    [Header("Left hand IK")]
    [SerializeField] private TwoBoneIKConstraint leftHandIK;
    [SerializeField] private Transform leftHandIK_Target;
    [SerializeField] private float leftHandIK_IncreaseStep;
    private bool shouldIncreaseLeftHandIKWieght;
    private Rig rig;


    private bool busyGrabbingWeapon;

    private void Start()
    {
        SwitchOnGun(pistol);

        anim = GetComponentInChildren<Animator>();
        rig =GetComponentInChildren<Rig>();
    }

    private void Update()
    {
        CheckWeaponSwitch();


        if (Input.GetKeyDown(KeyCode.R) && busyGrabbingWeapon == false)
        {
            anim.SetTrigger("Reload");
            PauseRig();
        }

        UpdateRigWight();

        UpdateLeftHandIKWeight();
    }

    private void UpdateLeftHandIKWeight()
    {
        if (shouldIncreaseLeftHandIKWieght)
        {
            leftHandIK.weight += leftHandIK_IncreaseStep * Time.deltaTime;
            if (leftHandIK.weight >= 1)
                shouldIncreaseLeftHandIKWieght = false;
        }
    }

    private void UpdateRigWight()
    {
        if (rigShouldBeIncrease)
        {
            rig.weight += rigIncreaseStep * Time.deltaTime;
            if (rig.weight >= 1)
                rigShouldBeIncrease = false;
        }
    }

    private void PauseRig()
    {
        rig.weight = .15f;
    }

    private void PlayerWeaponGrapAnimation( GrapType grapType)
    {
        leftHandIK.weight = 0;
        PauseRig();
        anim.SetFloat("WeaponGrapType", (float)grapType);
        anim.SetTrigger("Grap");

        SetBusyGrabbingWeapon(true);
    }

    public void SetBusyGrabbingWeapon(bool busy)
    {
        busyGrabbingWeapon = busy;
        anim.SetBool("IsWeaponGaping", busyGrabbingWeapon);
    }

    public void ReturnRigWeightOne() => rigShouldBeIncrease = true;
    public void ReturnWeightToLeftHandIK() => shouldIncreaseLeftHandIKWieght = true;
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