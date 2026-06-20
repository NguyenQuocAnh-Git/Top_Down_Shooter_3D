using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class CoopPlayerPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private LineRenderer aimLaser;
    [SerializeField] private Player_SoundFX sound;
    [SerializeField] private WeaponModel[] weaponModels;
    [SerializeField] private BackupWeaponModel[] backupWeaponModels;

    [Header("Rig")]
    [SerializeField] private float rigWeightIncreaseRate = 3f;
    [SerializeField] private float leftHandIkWeightIncreaseRate = 3f;
    [SerializeField] private Rig rig;
    [SerializeField] private TwoBoneIKConstraint leftHandIK;
    [SerializeField] private Transform leftHandIKTarget;
    [SerializeField] private MultiAimConstraint[] aimConstraints;
    [SerializeField] private float visualRotationLerpRate = 25f;
    [SerializeField] private float localClientPositionLerpRate = 30f;

    private NetworkPlayer networkPlayer;
    private NetworkPlayerWeapon weaponController;
    private bool shouldIncreaseRigWeight;
    private bool shouldIncreaseLeftHandIKWeight;
    private int lastRenderedFireTick;
    private WeaponType lastRenderedWeaponType;
    private int lastRenderedWeaponSlot = -1;
    private WeaponType currentVisualWeaponType;
    private bool hasRenderedWeaponState;
    private bool lastRenderedReloading;
    private int forceFullRigFramesRemaining;

    public Transform CurrentGunPoint
    {
        get
        {
            WeaponModel weaponModel = CurrentWeaponModel();
            return weaponModel != null && weaponModel.gunPoint != null ? weaponModel.gunPoint : null;
        }
    }

    private bool IsLocalOwner => networkPlayer != null
        && networkPlayer.Object != null
        && networkPlayer.Object.HasInputAuthority;

    public void Initialize(NetworkPlayer player, NetworkPlayerWeapon weapon)
    {
        networkPlayer = player;
        weaponController = weapon;
        AutoCacheReferences();
        ConfigureAudioSources();
        ConfigureRemoteOnlyVisuals();

        if (networkPlayer != null)
            lastRenderedFireTick = networkPlayer.NetFireTick;
    }

    public void ConfigureFromPlayerBody(
        Transform body,
        Animator bodyAnimator,
        Transform bodyAimTarget,
        LineRenderer bodyAimLaser,
        Player_SoundFX bodySound,
        Rig bodyRig,
        TwoBoneIKConstraint bodyLeftHandIk,
        Transform bodyLeftHandIkTarget)
    {
        if (body != null)
            playerBody = body;

        if (bodyAnimator != null)
            animator = bodyAnimator;

        if (bodyAimTarget != null)
            aimTarget = bodyAimTarget;

        if (bodyAimLaser != null)
            aimLaser = bodyAimLaser;

        if (bodySound != null)
            sound = bodySound;

        if (bodyRig != null)
            rig = bodyRig;

        if (bodyLeftHandIk != null)
            leftHandIK = bodyLeftHandIk;

        if (bodyLeftHandIkTarget != null)
            leftHandIKTarget = bodyLeftHandIkTarget;

        weaponModels = playerBody != null
            ? playerBody.GetComponentsInChildren<WeaponModel>(true)
            : GetComponentsInChildren<WeaponModel>(true);
        backupWeaponModels = playerBody != null
            ? playerBody.GetComponentsInChildren<BackupWeaponModel>(true)
            : GetComponentsInChildren<BackupWeaponModel>(true);

        if (playerBody != null)
            aimConstraints = playerBody.GetComponentsInChildren<MultiAimConstraint>(true);

        ConfigureAudioSources();
        ConfigureRemoteOnlyVisuals();

        if (IsLocalOwner && rig != null)
            rig.weight = 1f;
    }

    private void Awake()
    {
        AutoCacheReferences();
    }

    private void Update()
    {
        UpdateFootstepSfx();
        UpdateAimTargetForRig();
    }

    private void LateUpdate()
    {
        UpdateRigWeight();
        UpdateLeftHandIKWeight();
        EnforceFullRigIfNeeded();
        EnforceCombatRigForLocalOwner();
        UpdateWeaponAimVisuals();
    }

    public void RenderPresentation()
    {
        if (networkPlayer == null)
            return;

        UpdateVisualBodyTransform();
        UpdateAimTargetForRig();
        UpdateAnimator();
        RenderRemoteFireIfNeeded();
        RenderRemoteWeaponStateIfNeeded();
        RenderRemoteReloadIfNeeded();
    }

    public void PlayFireAnimation(bool playSfx)
    {
        animator?.SetTrigger("Fire");

        if (playSfx)
            CurrentWeaponModel()?.fireSFX?.Play();
    }

    public void PlayReloadAnimation(Weapon weapon)
    {
        if (weapon == null)
            return;

        animator?.SetFloat("ReloadSpeed", weapon.reloadSpeed);
        animator?.SetTrigger("Reload");
        ReduceLocalRigWeight();

        if (leftHandIK != null)
        {
            shouldIncreaseLeftHandIKWeight = false;
            leftHandIK.weight = 0f;
        }

        CurrentWeaponModel()?.realodSfx?.Play();
    }

    public void StopReloadSfx()
    {
        CurrentWeaponModel()?.realodSfx?.Stop();
    }

    public void PlayWeaponEquipAnimation(Weapon weapon)
    {
        if (weapon == null)
            return;

        WeaponModel weaponModel = FindWeaponModel(weapon.weaponType);
        EquipType equipType = weaponModel != null ? weaponModel.equipAnimationType : EquipType.SideEquipAnimation;

        if (leftHandIK != null)
        {
            shouldIncreaseLeftHandIKWeight = false;
            leftHandIK.weight = 0f;
        }

        ReduceLocalRigWeight();
        animator?.SetTrigger("EquipWeapon");
        animator?.SetFloat("EquipType", (float)equipType);
        animator?.SetFloat("EquipSpeed", weapon.equipmentSpeed);
    }

    public void ApplyWeaponVisual(Weapon currentWeapon, IReadOnlyList<Weapon> weaponSlots)
    {
        if (currentWeapon == null)
            return;

        ApplyWeaponVisual(currentWeapon.weaponType, weaponSlots);
    }

    public void ApplyWeaponVisual(WeaponType weaponType, IReadOnlyList<Weapon> weaponSlots)
    {
        currentVisualWeaponType = weaponType;
        SwitchOffWeaponModels();
        SwitchOffBackupWeaponModels();

        WeaponModel weaponModel = FindWeaponModel(weaponType);
        if (weaponModel != null)
        {
            weaponModel.gameObject.SetActive(true);
            SwitchAnimationLayer((int)weaponModel.holdType);
            AttachLeftHand(weaponModel);
        }

        ApplyBackupWeaponVisuals(weaponType, weaponSlots);
    }

    public void MaximizeRigWeight()
    {
        if (IsLocalOwner)
            shouldIncreaseRigWeight = true;
    }

    public void MaximizeLeftHandWeight()
    {
        if (IsLocalOwner == false)
            return;

        AttachLeftHand(CurrentWeaponModel());
        shouldIncreaseLeftHandIKWeight = true;
    }

    public void RestoreLocalRigAfterReload()
    {
        if (IsLocalOwner == false)
            return;

        shouldIncreaseRigWeight = false;
        shouldIncreaseLeftHandIKWeight = false;
        forceFullRigFramesRemaining = 30;

        ApplyFullLocalRigState();
    }

    public void RestoreLocalRigFromAnimationEvent()
    {
        if (IsLocalOwner == false)
            return;

        shouldIncreaseRigWeight = true;
        shouldIncreaseLeftHandIKWeight = true;
    }

    private void AutoCacheReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (playerBody == null && animator != null)
            playerBody = animator.transform;

        if (aimTarget == null)
        {
            aimTarget = FindChildByName(transform, "Aim_Target");
            if (aimTarget == null)
                aimTarget = FindChildByName(transform, "AimTarget");
        }

        if (aimLaser == null)
            aimLaser = GetComponentInChildren<LineRenderer>(true);

        if (sound == null)
            sound = GetComponentInChildren<Player_SoundFX>(true);

        if (rig == null)
            rig = GetComponentInChildren<Rig>(true);

        if (leftHandIK == null)
            leftHandIK = GetComponentInChildren<TwoBoneIKConstraint>(true);

        if (leftHandIKTarget == null)
            leftHandIKTarget = FindChildByName(transform, "LeftHandIK_Target");

        if (weaponModels == null || weaponModels.Length == 0)
            weaponModels = GetComponentsInChildren<WeaponModel>(true);

        if (backupWeaponModels == null || backupWeaponModels.Length == 0)
            backupWeaponModels = GetComponentsInChildren<BackupWeaponModel>(true);
    }

    private void ConfigureAudioSources()
    {
        AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
        foreach (AudioSource audioSource in audioSources)
            audioSource.spatialBlend = 1f;
    }

    private void ConfigureRemoteOnlyVisuals()
    {
        if (IsLocalOwner)
            return;

        if (aimLaser != null)
            aimLaser.enabled = false;

        if (rig != null)
            rig.weight = 0f;

        if (leftHandIK != null)
            leftHandIK.weight = 0f;
    }

    private void UpdateAnimator()
    {
        if (animator == null || networkPlayer == null)
            return;

        Transform visualReference = playerBody != null ? playerBody : transform;
        Vector3 movementDirection = new Vector3(networkPlayer.MoveInput.x, 0f, networkPlayer.MoveInput.y);
        float xVelocity = movementDirection.sqrMagnitude > 0.001f
            ? Vector3.Dot(movementDirection.normalized, visualReference.right)
            : 0f;
        float zVelocity = movementDirection.sqrMagnitude > 0.001f
            ? Vector3.Dot(movementDirection.normalized, visualReference.forward)
            : 0f;

        animator.SetFloat("xVelocity", xVelocity, 0.1f, Time.deltaTime);
        animator.SetFloat("zVelocity", zVelocity, 0.1f, Time.deltaTime);
        animator.SetBool("isRunning", networkPlayer.IsRunning && movementDirection.sqrMagnitude > 0.001f);
    }

    private void UpdateVisualBodyTransform()
    {
        if (playerBody == null || networkPlayer == null)
            return;

        Quaternion targetRotation = networkPlayer.VisualAimRotation;
        playerBody.rotation = Quaternion.Slerp(
            playerBody.rotation,
            targetRotation,
            visualRotationLerpRate * Time.deltaTime);

        Vector3 targetPosition = networkPlayer.transform.position;

        if (ShouldSmoothLocalClientVisual())
        {
            playerBody.position = Vector3.Lerp(
                playerBody.position,
                targetPosition,
                localClientPositionLerpRate * Time.deltaTime);
        }
        else
        {
            playerBody.position = targetPosition;
        }
    }

    private bool ShouldSmoothLocalClientVisual()
    {
        return IsLocalOwner
            && networkPlayer.Object != null
            && networkPlayer.Object.HasStateAuthority == false;
    }

    private void UpdateAimTargetForRig()
    {
        if (networkPlayer == null || aimTarget == null)
            return;

        aimTarget.position = networkPlayer.GetVisualAimPoint();
    }

    private void UpdateWeaponAimVisuals()
    {
        if (networkPlayer == null)
            return;

        UpdateRemoteAimVisual(networkPlayer.GetVisualAimPoint());
    }

    private void UpdateRemoteAimVisual(Vector3 aimPoint)
    {
        WeaponModel weaponModel = CurrentWeaponModel();
        bool canAimWeapon = weaponController != null && weaponController.WeaponReady() && weaponModel != null;

        if (canAimWeapon)
        {
            weaponModel.transform.LookAt(aimPoint);

            if (weaponModel.gunPoint != null)
                weaponModel.gunPoint.LookAt(aimPoint);

            AttachLeftHand(weaponModel);
        }

        UpdateLocalAimLaser(weaponModel, aimPoint);
    }

    private void UpdateLocalAimLaser(WeaponModel weaponModel, Vector3 aimPoint)
    {
        if (aimLaser == null)
            return;

        if (IsLocalOwner == false || weaponController == null || weaponController.WeaponReady() == false || weaponModel == null)
        {
            aimLaser.enabled = false;
            return;
        }

        Transform gunPoint = weaponController.GunPoint;
        if (gunPoint == null)
        {
            aimLaser.enabled = false;
            return;
        }

        aimLaser.enabled = true;
        Vector3 laserDirection = weaponController.BulletDirection();
        float laserTipLength = 0.5f;
        float gunDistance = weaponController.CurrentWeapon != null ? weaponController.CurrentWeapon.gunDistance : 4f;
        Vector3 endPoint = gunPoint.position + laserDirection * gunDistance;

        if (Physics.Raycast(gunPoint.position, laserDirection, out RaycastHit hit, gunDistance))
        {
            endPoint = hit.point;
            laserTipLength = 0f;
        }

        aimLaser.SetPosition(0, gunPoint.position);
        aimLaser.SetPosition(1, endPoint);
        aimLaser.SetPosition(2, endPoint + laserDirection * laserTipLength);
    }

    private void RenderRemoteFireIfNeeded()
    {
        if (IsLocalOwner || networkPlayer == null)
            return;

        int currentFireTick = networkPlayer.NetFireTick;
        if (currentFireTick == lastRenderedFireTick)
            return;

        int shotsToVisualize = currentFireTick - lastRenderedFireTick;
        if (shotsToVisualize < 0 || shotsToVisualize > 8)
            shotsToVisualize = 1;

        lastRenderedFireTick = currentFireTick;
        PlayFireAnimation(true);

        for (int i = 0; i < shotsToVisualize; i++)
            weaponController?.SpawnRemoteVisualBullet();
    }

    private void RenderRemoteWeaponStateIfNeeded()
    {
        if (IsLocalOwner)
            return;

        if (hasRenderedWeaponState
            && lastRenderedWeaponType == networkPlayer.NetEquippedWeaponType
            && lastRenderedWeaponSlot == networkPlayer.NetWeaponSlotIndex)
        {
            return;
        }

        hasRenderedWeaponState = true;
        lastRenderedWeaponType = networkPlayer.NetEquippedWeaponType;
        lastRenderedWeaponSlot = networkPlayer.NetWeaponSlotIndex;

        if (weaponController != null)
            ApplyWeaponVisual(networkPlayer.NetEquippedWeaponType, weaponController.WeaponSlots);
        else
            ApplyWeaponVisual(networkPlayer.NetEquippedWeaponType, null);
    }

    private void RenderRemoteReloadIfNeeded()
    {
        if (IsLocalOwner)
            return;

        bool isReloading = networkPlayer.NetReloading;

        if (isReloading && lastRenderedReloading == false)
            animator?.SetTrigger("Reload");

        lastRenderedReloading = isReloading;
    }

    private void UpdateFootstepSfx()
    {
        if (networkPlayer == null || sound == null)
            return;

        if (networkPlayer.Health != null && networkPlayer.Health.IsDead)
        {
            sound.walkSFX?.Stop();
            sound.runSFX?.Stop();
            return;
        }

        bool moving = networkPlayer.MoveInput.sqrMagnitude > 0.001f;
        AudioSource activeSource = networkPlayer.IsRunning ? sound.runSFX : sound.walkSFX;
        AudioSource inactiveSource = networkPlayer.IsRunning ? sound.walkSFX : sound.runSFX;

        if (moving == false)
        {
            sound.walkSFX?.Stop();
            sound.runSFX?.Stop();
            return;
        }

        if (inactiveSource != null && inactiveSource.isPlaying)
            inactiveSource.Stop();

        if (activeSource != null && activeSource.isPlaying == false)
            activeSource.Play();
    }

    private void ApplyBackupWeaponVisuals(WeaponType currentWeaponType, IReadOnlyList<Weapon> weaponSlots)
    {
        if (weaponSlots == null || weaponSlots.Count <= 1)
            return;

        BackupWeaponModel lowHangWeapon = null;
        BackupWeaponModel backHangWeapon = null;
        BackupWeaponModel sideHangWeapon = null;

        foreach (BackupWeaponModel backupModel in backupWeaponModels)
        {
            if (backupModel.weaponType == currentWeaponType || WeaponTypeInSlots(backupModel.weaponType, weaponSlots) == false)
                continue;

            if (backupModel.HangTypeIs(HangType.LowBackHang))
                lowHangWeapon = backupModel;

            if (backupModel.HangTypeIs(HangType.BackHang))
                backHangWeapon = backupModel;

            if (backupModel.HangTypeIs(HangType.SideHang))
                sideHangWeapon = backupModel;
        }

        lowHangWeapon?.Activate(true);
        backHangWeapon?.Activate(true);
        sideHangWeapon?.Activate(true);
    }

    private static bool WeaponTypeInSlots(WeaponType weaponType, IReadOnlyList<Weapon> weaponSlots)
    {
        foreach (Weapon weapon in weaponSlots)
        {
            if (weapon != null && weapon.weaponType == weaponType)
                return true;
        }

        return false;
    }

    private WeaponModel CurrentWeaponModel()
    {
        if (weaponController == null || weaponController.CurrentWeapon == null)
            return FindWeaponModel(currentVisualWeaponType);

        return FindWeaponModel(IsLocalOwner ? weaponController.CurrentWeapon.weaponType : currentVisualWeaponType);
    }

    private WeaponModel FindWeaponModel(WeaponType weaponType)
    {
        foreach (WeaponModel weaponModel in weaponModels)
        {
            if (weaponModel != null && weaponModel.weaponType == weaponType)
                return weaponModel;
        }

        return null;
    }

    private void SwitchOffWeaponModels()
    {
        foreach (WeaponModel weaponModel in weaponModels)
            weaponModel?.gameObject.SetActive(false);
    }

    private void SwitchOffBackupWeaponModels()
    {
        foreach (BackupWeaponModel backupModel in backupWeaponModels)
            backupModel?.Activate(false);
    }

    private void SwitchAnimationLayer(int layerIndex)
    {
        if (animator == null)
            return;

        for (int i = 1; i < animator.layerCount; i++)
            animator.SetLayerWeight(i, 0f);

        if (layerIndex > 0 && layerIndex < animator.layerCount)
            animator.SetLayerWeight(layerIndex, 1f);
    }

    private void AttachLeftHand(WeaponModel weaponModel)
    {
        if (IsLocalOwner == false || leftHandIKTarget == null || weaponModel == null || weaponModel.holdPoint == null)
            return;

        leftHandIKTarget.SetPositionAndRotation(weaponModel.holdPoint.position, weaponModel.holdPoint.rotation);
    }

    private void UpdateLeftHandIKWeight()
    {
        if (shouldIncreaseLeftHandIKWeight == false || leftHandIK == null)
            return;

        leftHandIK.weight += leftHandIkWeightIncreaseRate * Time.deltaTime;

        if (leftHandIK.weight >= 1f)
        {
            leftHandIK.weight = 1f;
            shouldIncreaseLeftHandIKWeight = false;
        }
    }

    private void UpdateRigWeight()
    {
        if (shouldIncreaseRigWeight == false || rig == null)
            return;

        rig.weight += rigWeightIncreaseRate * Time.deltaTime;

        if (rig.weight >= 1f)
        {
            rig.weight = 1f;
            shouldIncreaseRigWeight = false;
        }
    }

    private void ApplyFullLocalRigState()
    {
        if (rig != null)
            rig.weight = 1f;

        if (leftHandIK != null)
            leftHandIK.weight = 1f;

        RestoreAimConstraintWeights();

        WeaponModel weaponModel = CurrentWeaponModel();
        if (weaponModel == null)
            return;

        SwitchAnimationLayer((int)weaponModel.holdType);
        AttachLeftHand(weaponModel);
    }

    private void RestoreAimConstraintWeights()
    {
        if (aimConstraints == null)
            return;

        foreach (MultiAimConstraint aimConstraint in aimConstraints)
        {
            if (aimConstraint != null)
                aimConstraint.weight = 1f;
        }
    }

    private void EnforceCombatRigForLocalOwner()
    {
        if (IsLocalOwner == false || weaponController == null || networkPlayer == null)
            return;

        if (weaponController.WeaponReady() == false || networkPlayer.NetReloading)
            return;

        if (rig != null && rig.weight < 1f)
            rig.weight = 1f;

        if (leftHandIK != null && leftHandIK.weight < 1f)
            leftHandIK.weight = 1f;

        RestoreAimConstraintWeights();
    }

    private void EnforceFullRigIfNeeded()
    {
        if (IsLocalOwner == false || forceFullRigFramesRemaining <= 0)
            return;

        ApplyFullLocalRigState();
        forceFullRigFramesRemaining--;
    }

    private void ReduceLocalRigWeight()
    {
        if (IsLocalOwner == false || rig == null)
            return;

        forceFullRigFramesRemaining = 0;
        rig.weight = 0.15f;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
