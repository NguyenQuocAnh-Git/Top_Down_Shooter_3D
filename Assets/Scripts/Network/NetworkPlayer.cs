using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkPlayer : NetworkBehaviour
{
    private const float MinAimDirectionSqrMagnitude = 0.0025f;
    private const int MaxReplicatedEnemies = 64;
    private const int EnemyReplicationTickStride = 3;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 3f;

    [Header("Aim")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform gunPoint;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private LayerMask aimLayerMask;
    [SerializeField] private float minCameraDistance = 1f;
    [SerializeField] private float maxCameraDistance = 3f;
    [SerializeField] private float cameraSensitivity = 5f;
    [SerializeField] private float minAimDistance = 2f;
    [SerializeField] private float visualAimDistance = 8f;

    [Networked] public Vector3 NetAimPoint { get; private set; }
    [Networked] public Vector2 NetMoveInput { get; private set; }
    [Networked] public NetworkBool NetIsRunning { get; private set; }
    [Networked] public WeaponType NetEquippedWeaponType { get; private set; }
    [Networked] public int NetWeaponSlotIndex { get; private set; }
    [Networked] public int NetFireTick { get; private set; }
    [Networked] public NetworkBool NetReloading { get; private set; }
    [Networked] public int NetEnemyCount { get; private set; }
    [Networked] public int NetEnemySequence { get; private set; }
    [Networked, Capacity(MaxReplicatedEnemies)]
    public NetworkArray<NetworkEnemyFusionState> NetEnemyStates => default;

    private NetworkCharacterController networkCharacterController;
    private NetworkPlayerHealth health;
    private CoopPlayerPresentation presentation;
    private RaycastHit lastKnownAimHit;
    private Vector3 frozenCameraPosition;
    private Vector3 lastFlatAimDirection;
    private bool cameraFrozen;
    private int enemyReplicationTick;
    private int lastRenderedEnemySequence;

    public NetworkPlayerHealth Health => health;
    public Transform CameraTarget => cameraTarget;
    public Transform GunPoint => presentation != null && presentation.CurrentGunPoint != null
        ? presentation.CurrentGunPoint
        : gunPoint != null ? gunPoint : transform;
    public Vector2 MoveInput => NetMoveInput;
    public bool IsRunning => NetIsRunning;
    public Vector3 AimPoint => GetAimPosition();
    public Quaternion VisualAimRotation
    {
        get
        {
            Vector3 lookDirection = GetVisualAimPoint() - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude < 0.001f)
                lookDirection = ResolveFlatAimDirection(GetAimPosition(), false);
            else
                lookDirection.Normalize();

            return Quaternion.LookRotation(lookDirection);
        }
    }

    public Vector3 GetVisualAimPoint()
    {
        if (Object.HasInputAuthority && ControlsManager.instance != null && ControlsManager.instance.controls != null)
        {
            Vector2 screenPosition = ControlsManager.instance.controls.Character.Aim.ReadValue<Vector2>();
            return ProjectVisualAimPoint(FlattenAimHeight(ComputeAimPoint(screenPosition)));
        }

        return ProjectVisualAimPoint(GetAimPosition());
    }

    public CoopPlayerPresentation Presentation => presentation;

    private void Awake()
    {
        networkCharacterController = GetComponent<NetworkCharacterController>();

        if (networkCharacterController != null)
            networkCharacterController.rotationSpeed = 0f;

        health = GetComponent<NetworkPlayerHealth>();
        presentation = GetComponent<CoopPlayerPresentation>();
        lastFlatAimDirection = transform.forward;
    }

    public override void Spawned()
    {
        CoopGameplayBootstrap.Instance?.AssemblePlayerVisual(this);

        NetworkPlayerWeapon weapon = GetComponent<NetworkPlayerWeapon>();
        weapon?.InitializeAfterVisualReady();

        if (Object.HasInputAuthority)
            CoopPlayerCamera.BindLocalPlayer(this);
    }

    public void ConfigureVisualReferences(Transform aim, Transform gun, Transform camera)
    {
        if (aim != null)
            aimTarget = aim;

        if (gun != null)
            gunPoint = gun;

        if (camera != null)
            cameraTarget = camera;
    }

    public override void FixedUpdateNetwork()
    {
        PublishEnemyStatesFromHost();

        if (health != null && health.IsDead)
            return;

        if (GetInput(out CoopPlayerInput input))
        {
            NetAimPoint = ResolveAimPoint(input);
            NetMoveInput = input.Movement;
            NetIsRunning = input.Run;

            ApplyMovement(input);

            if (Object.HasInputAuthority && input.InteractPressed)
                CoopPickupCoordinator.Instance?.RequestNearestPickup(this);
        }
    }

    public override void Render()
    {
        RenderReplicatedEnemyStates();

        if (health != null && health.IsDead)
        {
            return;
        }

        UpdateCameraTarget();
        presentation?.RenderPresentation();
    }

    private void PublishEnemyStatesFromHost()
    {
        if (Runner == null
            || Runner.IsServer == false
            || Object == null
            || Object.HasInputAuthority == false)
            return;

        enemyReplicationTick++;
        if (enemyReplicationTick % EnemyReplicationTickStride != 0)
            return;

        int count = NetworkEnemy.GetFusionStateCount(MaxReplicatedEnemies);
        NetEnemyCount = count;
        NetEnemySequence++;

        for (int i = 0; i < count; i++)
        {
            NetworkEnemyFusionState state = default;
            NetworkEnemy.TryCaptureFusionState(i, out state);
            NetEnemyStates.Set(i, state);
        }
    }

    private void RenderReplicatedEnemyStates()
    {
        if (Runner == null
            || Runner.IsServer
            || NetEnemyCount <= 0
            || NetEnemySequence <= lastRenderedEnemySequence)
            return;

        lastRenderedEnemySequence = NetEnemySequence;
        int count = Mathf.Min(NetEnemyCount, MaxReplicatedEnemies);

        for (int i = 0; i < count; i++)
            NetworkEnemy.ApplyFusionState(NetEnemyStates[i], NetEnemySequence);
    }

    public void SetWeaponVisualState(WeaponType weaponType, int slotIndex)
    {
        NetEquippedWeaponType = weaponType;
        NetWeaponSlotIndex = slotIndex;
    }

    public void MarkFireVisual()
    {
        if (Object == null)
            return;

        if (Object.HasStateAuthority)
        {
            NetFireTick++;
            return;
        }

        if (Object.HasInputAuthority)
            RpcMarkFireVisual();
    }

    public Vector3 ResolveAimWorldPointForInput(Vector2 screenPosition)
    {
        return ComputeAimPoint(screenPosition);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcMarkFireVisual(RpcInfo info = default)
    {
        NetFireTick++;
    }

    public void SetReloadingVisual(bool reloading)
    {
        NetReloading = reloading;
    }

    public Vector3 BulletDirection()
    {
        Vector3 direction = GetAimPosition() - GunPoint.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        return direction.normalized;
    }

    private void ApplyMovement(CoopPlayerInput input)
    {
        Vector3 moveDirection = new Vector3(input.Movement.x, 0f, input.Movement.y);
        networkCharacterController.maxSpeed = input.Run ? runSpeed : walkSpeed;
        networkCharacterController.Move(moveDirection);
    }

    private Vector3 ResolveAimPoint(CoopPlayerInput input)
    {
        if (input.AimWorldPoint != Vector3.zero)
            return input.AimWorldPoint;

        return ComputeAimPoint(input.AimScreenPosition);
    }

    private Vector3 ComputeAimPoint(Vector2 screenPosition)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return transform.position + transform.forward * 2f;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, aimLayerMask))
        {
            lastKnownAimHit = hit;
            return hit.point;
        }

        return lastKnownAimHit.collider != null ? lastKnownAimHit.point : transform.position + transform.forward * 2f;
    }

    private Vector3 GetAimPosition()
    {
        if (NetAimPoint == Vector3.zero)
            return transform.position + transform.forward * minAimDistance + Vector3.up;

        return ClampAimDistance(FlattenAimHeight(NetAimPoint));
    }

    private Vector3 FlattenAimHeight(Vector3 aimPosition)
    {
        aimPosition.y = transform.position.y + 1f;
        return aimPosition;
    }

    private Vector3 ClampAimDistance(Vector3 aimPosition)
    {
        Vector3 flatDirection = ResolveFlatAimDirection(aimPosition, true);
        Vector3 rawFlatDirection = aimPosition - transform.position;
        rawFlatDirection.y = 0f;

        if (rawFlatDirection.sqrMagnitude < minAimDistance * minAimDistance)
        {
            aimPosition = transform.position + flatDirection * minAimDistance;
            aimPosition.y = transform.position.y + 1f;
        }

        return aimPosition;
    }

    private Vector3 ProjectVisualAimPoint(Vector3 aimPosition)
    {
        Vector3 flatDirection = ResolveFlatAimDirection(aimPosition, true);

        Vector3 visualAimPosition = transform.position + flatDirection * Mathf.Max(minAimDistance, visualAimDistance);
        visualAimPosition.y = transform.position.y + 1f;
        return visualAimPosition;
    }

    private Vector3 ResolveFlatAimDirection(Vector3 aimPosition, bool updateLastDirection)
    {
        Vector3 flatDirection = aimPosition - transform.position;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude >= MinAimDirectionSqrMagnitude)
        {
            flatDirection.Normalize();

            if (updateLastDirection)
                lastFlatAimDirection = flatDirection;

            return flatDirection;
        }

        if (lastFlatAimDirection.sqrMagnitude < 0.001f)
            lastFlatAimDirection = transform.forward;

        return lastFlatAimDirection;
    }

    private void UpdateCameraTarget()
    {
        if (cameraTarget == null || Object.HasInputAuthority == false)
            return;

        cameraTarget.position = Vector3.Lerp(
            cameraTarget.position,
            DesiredCameraPosition(),
            cameraSensitivity * Time.deltaTime);
    }

    private Vector3 DesiredCameraPosition()
    {
        float actualMaxCameraDistance = NetMoveInput.y < -0.5f ? minCameraDistance : maxCameraDistance;
        Vector3 desiredCameraPosition = GetAimPosition();
        Vector3 aimDirection = (desiredCameraPosition - transform.position).normalized;
        float distanceToDesiredPosition = Vector3.Distance(transform.position, desiredCameraPosition);
        float clampedDistance = Mathf.Clamp(distanceToDesiredPosition, minCameraDistance, actualMaxCameraDistance);

        desiredCameraPosition = transform.position + aimDirection * clampedDistance;
        desiredCameraPosition.y = transform.position.y + 1f;

        return desiredCameraPosition;
    }

    private void FreezeCameraAtDeathPosition()
    {
        if (Object.HasInputAuthority == false || cameraTarget == null || cameraFrozen)
            return;

        frozenCameraPosition = cameraTarget.position;
        cameraTarget.position = frozenCameraPosition;
        cameraFrozen = true;
    }
}
