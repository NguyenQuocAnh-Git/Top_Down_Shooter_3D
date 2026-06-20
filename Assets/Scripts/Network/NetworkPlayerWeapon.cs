using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class NetworkPlayerWeapon : NetworkBehaviour
{
    private const float ReferenceBulletSpeed = 20f;

    [Header("Bullet")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float bulletImpactForce = 100f;
    [SerializeField] private LayerMask allyLayerMask;

    [Header("Fallback")]
    [SerializeField] private List<Weapon_Data> fallbackWeaponData;

    [Header("Inventory")]
    [SerializeField] private int maxSlots = 2;

    private NetworkPlayer networkPlayer;
    private CoopPlayerPresentation presentation;
    private Weapon currentWeapon;
    private readonly List<Weapon> weaponSlots = new List<Weapon>();
    private TickTimer fireCooldown;
    private TickTimer reloadTimer;
    private TickTimer equipTimer;
    private TickTimer burstTimer;
    private int currentWeaponSlotIndex;
    private int burstShotsRemaining;
    private bool weaponReady = true;
    private bool isReloading;
    private bool isEquipping;

    public Transform GunPoint => networkPlayer != null ? networkPlayer.GunPoint : transform;
    public Weapon CurrentWeapon => currentWeapon;
    public IReadOnlyList<Weapon> WeaponSlots => weaponSlots;

    private void Awake()
    {
        networkPlayer = GetComponent<NetworkPlayer>();
        presentation = GetComponent<CoopPlayerPresentation>();
    }

    public void InitializeAfterVisualReady()
    {
        InitializeLoadout();
        presentation?.Initialize(networkPlayer, this);
        ApplyCurrentWeaponVisual(false);
        UpdateLocalCameraDistance();

        if (Object.HasInputAuthority)
            RefreshWeaponUI();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasInputAuthority == false || networkPlayer == null)
            return;

        if (networkPlayer.Health != null && networkPlayer.Health.IsDead)
            return;

        CompleteTimedEquipOrReloadIfNeeded();
        ContinueBurstIfNeeded();

        if (GetInput(out CoopPlayerInput input) == false)
            return;

        HandleSlotInput(input);
        HandleReloadInput(input);
        HandleToggleWeaponModeInput(input);
        HandleFireInput(input);
    }

    public bool WeaponReady() => weaponReady && isReloading == false && isEquipping == false;

    public Vector3 BulletDirection()
    {
        return networkPlayer != null ? networkPlayer.BulletDirection() : transform.forward;
    }

    public void ReloadIsOver()
    {
        CompleteReload();
    }

    public void ReturnRig()
    {
        presentation?.MaximizeRigWeight();
        presentation?.MaximizeLeftHandWeight();
    }

    public void WeaponEquipingIsOver()
    {
        CompleteEquip();
    }

    public void SwitchOnWeaponModel()
    {
        ApplyCurrentWeaponVisual(false);
    }

    public void SetWeaponReady(bool ready)
    {
        weaponReady = ready;

        if (ready)
            ReturnRig();
    }

    private void HandleFireInput(CoopPlayerInput input)
    {
        if (currentWeapon == null)
            return;

        bool wantsToFire = currentWeapon.shootType == ShootType.Auto ? input.Fire : input.FirePressed;

        if (wantsToFire == false)
            return;

        Shoot();
    }

    private void Shoot()
    {
        if (WeaponReady() == false)
            return;

        if (fireCooldown.ExpiredOrNotRunning(Runner) == false)
            return;

        if (currentWeapon == null || currentWeapon.CanShoot() == false)
            return;

        presentation?.PlayFireAnimation(true);

        if (currentWeapon.BurstActivated())
        {
            StartBurst();
            return;
        }

        FireLocalBullet();
        StartFireCooldown();
    }

    public void SpawnRemoteVisualBullet()
    {
        if (Object.HasInputAuthority || bulletPrefab == null || networkPlayer == null)
            return;

        Transform gunPoint = GunPoint;
        if (gunPoint == null)
            return;

        float flyDistance = currentWeapon != null ? currentWeapon.gunDistance : 25f;
        GameObject bulletObject = ObjectPool.instance.GetObject(bulletPrefab, gunPoint);
        CoopNetworkBullet bullet = CoopNetworkBullet.EnsureOn(bulletObject);
        bullet.InitializeVisualOnly(flyDistance, networkPlayer.BulletDirection(), bulletSpeed);
    }

    public void ReportLocalHit(Collision collision)
    {
        if (Object.HasInputAuthority == false || currentWeapon == null)
            return;

        NetworkObject targetObject = ResolveHitTarget(collision.collider);
        int damage = currentWeapon.bulletDamage;
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.transform.position;
        Vector3 hitNormal = collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.up;

        RpcSubmitHit(targetObject, damage, hitPoint, hitNormal);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSubmitHit(NetworkObject targetObject, int damage, Vector3 hitPoint, Vector3 hitNormal, RpcInfo info = default)
    {
        if (ValidateHit(info.Source, targetObject, damage, hitPoint) == false)
            return;

        ApplyHostDamage(targetObject, damage, hitPoint);
    }

    private void FireLocalBullet()
    {
        currentWeapon.bulletsInMagazine--;
        RefreshWeaponUI();
        networkPlayer.MarkFireVisual();

        Transform gunPoint = GunPoint;
        GameObject bulletObject = ObjectPool.instance.GetObject(bulletPrefab, gunPoint);
        CoopNetworkBullet bullet = CoopNetworkBullet.EnsureOn(bulletObject);
        bullet.Initialize(this, allyLayerMask, currentWeapon.bulletDamage, currentWeapon.gunDistance, bulletImpactForce);

        Rigidbody bulletRigidbody = bulletObject.GetComponent<Rigidbody>();
        Vector3 direction = networkPlayer.BulletDirection();
        direction = currentWeapon.ApplySpread(direction);

        bulletRigidbody.mass = ReferenceBulletSpeed / bulletSpeed;
        bulletRigidbody.velocity = direction * bulletSpeed;
    }

    private void StartFireCooldown()
    {
        float delay = currentWeapon.fireRate > 0f ? 1f / currentWeapon.fireRate : 0.1f;
        fireCooldown = TickTimer.CreateFromSeconds(Runner, delay);
    }

    private void InitializeLoadout()
    {
        List<Weapon_Data> weaponData = GameSessionData.GetWeaponsForPlayer(Object.InputAuthority.PlayerId);

        if (weaponData.Count == 0)
            weaponData = GameSessionData.GetSelectedWeapons();

        if (weaponData.Count == 0 && fallbackWeaponData != null)
            weaponData = new List<Weapon_Data>(fallbackWeaponData);

        if (weaponData.Count == 0)
        {
            Debug.LogWarning("[COOP] NetworkPlayerWeapon has no weapon data.");
            return;
        }

        weaponSlots.Clear();
        int slotsToCreate = Mathf.Min(maxSlots, weaponData.Count);

        for (int i = 0; i < slotsToCreate; i++)
            weaponSlots.Add(new Weapon(weaponData[i]));

        currentWeaponSlotIndex = 0;
        currentWeapon = weaponSlots[0];
        weaponReady = true;
        isReloading = false;
        isEquipping = false;
        networkPlayer.SetWeaponVisualState(currentWeapon.weaponType, currentWeaponSlotIndex);
    }

    private void RefreshWeaponUI()
    {
        if (Object.HasInputAuthority == false || currentWeapon == null || UI.instance == null || UI.instance.inGameUI == null)
            return;

        UI.instance.inGameUI.UpdateWeaponUI(weaponSlots, currentWeapon);
    }

    private void HandleSlotInput(CoopPlayerInput input)
    {
        if (input.EquipSlotPressed == 0)
            return;

        EquipWeapon(input.EquipSlotPressed - 1);
    }

    private void HandleReloadInput(CoopPlayerInput input)
    {
        if (input.ReloadPressed == false || currentWeapon == null)
            return;

        if (currentWeapon.CanReload() && WeaponReady())
            Reload();
    }

    private void HandleToggleWeaponModeInput(CoopPlayerInput input)
    {
        if (input.ToggleWeaponModePressed == false || currentWeapon == null)
            return;

        currentWeapon.ToggleBurst();
        RefreshWeaponUI();
    }

    private void EquipWeapon(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Count || slotIndex == currentWeaponSlotIndex)
            return;

        currentWeaponSlotIndex = slotIndex;
        currentWeapon = weaponSlots[slotIndex];
        weaponReady = false;
        isEquipping = true;
        reloadTimer = TickTimer.None;
        networkPlayer.SetReloadingVisual(false);
        networkPlayer.SetWeaponVisualState(currentWeapon.weaponType, currentWeaponSlotIndex);

        presentation?.PlayWeaponEquipAnimation(currentWeapon);
        ApplyCurrentWeaponVisual(false);
        UpdateLocalCameraDistance();
        RefreshWeaponUI();

        float duration = currentWeapon.equipmentSpeed > 0f ? 1f / currentWeapon.equipmentSpeed : 0.25f;
        equipTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    private void Reload()
    {
        weaponReady = false;
        isReloading = true;
        reloadTimer = TickTimer.None;
        networkPlayer.SetReloadingVisual(true);
        presentation?.PlayReloadAnimation(currentWeapon);
    }

    private void CompleteTimedEquipOrReloadIfNeeded()
    {
        if (isEquipping && equipTimer.Expired(Runner))
            CompleteEquip();
    }

    private void CompleteEquip()
    {
        if (isEquipping == false)
            return;

        isEquipping = false;
        weaponReady = true;
        equipTimer = TickTimer.None;
        presentation?.RestoreLocalRigAfterReload();
    }

    private void CompleteReload()
    {
        if (isReloading == false || currentWeapon == null)
            return;

        currentWeapon.RefillBullets();
        isReloading = false;
        weaponReady = true;
        reloadTimer = TickTimer.None;
        networkPlayer.SetReloadingVisual(false);
        presentation?.RestoreLocalRigAfterReload();
        RefreshWeaponUI();
    }

    private void ApplyCurrentWeaponVisual(bool playEquipAnimation)
    {
        if (currentWeapon == null)
            return;

        if (playEquipAnimation)
            presentation?.PlayWeaponEquipAnimation(currentWeapon);

        presentation?.ApplyWeaponVisual(currentWeapon, weaponSlots);
        networkPlayer.SetWeaponVisualState(currentWeapon.weaponType, currentWeaponSlotIndex);
    }

    private void UpdateLocalCameraDistance()
    {
        if (Object.HasInputAuthority == false || currentWeapon == null || CameraManager.instance == null)
            return;

        CameraManager.instance.ChangeCameraDistance(currentWeapon.cameraDistance);
    }

    private void StartBurst()
    {
        weaponReady = false;
        burstShotsRemaining = Mathf.Max(1, currentWeapon.bulletsPerShot);
        burstTimer = TickTimer.None;
        ContinueBurstIfNeeded();
    }

    private void ContinueBurstIfNeeded()
    {
        if (burstShotsRemaining <= 0)
            return;

        if (burstTimer.ExpiredOrNotRunning(Runner) == false)
            return;

        if (currentWeapon == null || currentWeapon.bulletsInMagazine <= 0)
        {
            FinishBurst();
            return;
        }

        FireLocalBullet();
        burstShotsRemaining--;

        if (burstShotsRemaining <= 0)
        {
            FinishBurst();
            return;
        }

        float delay = Mathf.Max(0.01f, currentWeapon.burstFireDelay);
        burstTimer = TickTimer.CreateFromSeconds(Runner, delay);
    }

    private void FinishBurst()
    {
        burstShotsRemaining = 0;
        weaponReady = true;
        burstTimer = TickTimer.None;
        StartFireCooldown();
    }

    private NetworkObject ResolveHitTarget(Collider collider)
    {
        NetworkPlayerHitbox playerHitbox = collider.GetComponentInParent<NetworkPlayerHitbox>();
        if (playerHitbox != null)
            return playerHitbox.GetNetworkObject();

        NetworkBehaviour networkBehaviour = collider.GetComponentInParent<NetworkBehaviour>();
        return networkBehaviour != null ? networkBehaviour.Object : null;
    }

    private bool ValidateHit(PlayerRef shooter, NetworkObject targetObject, int damage, Vector3 hitPoint)
    {
        if (damage <= 0 || currentWeapon == null || damage > currentWeapon.bulletDamage)
            return false;

        NetworkPlayer shooterPlayer = FindPlayerForRef(shooter);
        if (shooterPlayer == null || shooterPlayer.Health == null || shooterPlayer.Health.IsDead)
            return false;

        float maxDistance = currentWeapon != null ? currentWeapon.gunDistance + 2f : 25f;
        float distance = Vector3.Distance(shooterPlayer.transform.position, hitPoint);

        if (distance > maxDistance)
            return false;

        if (targetObject == null)
            return true;

        NetworkPlayerHitbox targetHitbox = targetObject.GetComponentInChildren<NetworkPlayerHitbox>();
        if (targetHitbox != null && GameSessionData.FriendlyFire == false)
            return false;

        return HasLineOfSight(shooterPlayer.GunPoint.position, hitPoint, targetObject);
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 hitPoint, NetworkObject targetObject)
    {
        Vector3 direction = hitPoint - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
            return true;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance))
        {
            if (targetObject != null && hit.collider != null)
            {
                NetworkBehaviour hitBehaviour = hit.collider.GetComponentInParent<NetworkBehaviour>();
                if (hitBehaviour != null && hitBehaviour.Object != null && hitBehaviour.Object.Id == targetObject.Id)
                    return true;
            }

            return targetObject == null;
        }

        return true;
    }

    private void ApplyHostDamage(NetworkObject targetObject, int damage, Vector3 hitPoint)
    {
        if (targetObject != null)
        {
            NetworkPlayerHealth targetHealth = targetObject.GetComponent<NetworkPlayerHealth>();
            if (targetHealth != null)
            {
                targetHealth.ApplyDamageFromHost(damage);
                return;
            }

            IDamagable networkedDamagable = targetObject.GetComponentInChildren<IDamagable>();
            if (networkedDamagable != null)
            {
                networkedDamagable.TakeDamage(damage);
                return;
            }
        }

        Collider[] overlaps = Physics.OverlapSphere(hitPoint, 0.35f);

        foreach (Collider overlap in overlaps)
        {
            IDamagable damagable = overlap.GetComponent<IDamagable>();
            if (damagable != null)
            {
                damagable.TakeDamage(damage);
                return;
            }
        }
    }

    private static NetworkPlayer FindPlayerForRef(PlayerRef playerRef)
    {
        NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();

        foreach (NetworkPlayer player in players)
        {
            if (player.Object != null && player.Object.InputAuthority == playerRef)
                return player;
        }

        if (players.Length > 0 && players[0].Runner != null && players[0].Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject))
            return playerObject.GetComponent<NetworkPlayer>();

        return null;
    }
}
