using UnityEngine;

public class Player_AimController : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0025f;

    private Player player;
    private PlayerControls controls;

    [Header("Aim Viusal - Laser")]
    [SerializeField] private LineRenderer aimLaser; // this component is on the waepon holder(child of a player)

    [Header("Aim control")]
    [SerializeField] private Transform aim;
    [SerializeField] private float minAimDistance = 2f;
    [SerializeField] private float visualAimDistance = 8f;

    [SerializeField] private bool isAimingPrecisly;
    [SerializeField] private bool isLockingToTarget;

    [Header("Camera control")]
    [SerializeField] private Transform cameraTarget;
    [Range(.5f, 1)]
    [SerializeField] private float minCameraDistance = 1.5f;
    [Range(1, 3f)]
    [SerializeField] private float maxCameraDistance = 4;
    [Range(3f, 5f)]
    [SerializeField] private float cameraSensetivity = 5f;

    [Space]

    [SerializeField] private LayerMask aimLayerMask;

    private Vector2 mouseInput;
    private RaycastHit lastKnownMouseHit;
    private Vector3 lastFlatAimDirection;

    private void Start()
    {
        player = GetComponent<Player>();
        lastFlatAimDirection = transform.forward;
        AssignInputEvents();
    }
    private void Update()
    {
        if (player.health.isDead)
            return;

        if (player.controlsEnabled == false)
            return;

        if(Input.GetKeyDown(KeyCode.P))
            isAimingPrecisly = !isAimingPrecisly;

        if(Input.GetKeyDown(KeyCode.L))
            isLockingToTarget = !isLockingToTarget;

        UpdateAimPosition();
        UpdateAimVisuals();
        UpdateCameraPosition();
    }

    public Transform GetAimCameraTarget()
    {
        cameraTarget.position = player.transform.position;
        return cameraTarget;
    }
    public void EnableAimLaer(bool enable) => aimLaser.enabled = enable;
    private void UpdateAimVisuals()
    {
        aimLaser.enabled = player.weapon.WeaponReady();

        if (aimLaser.enabled == false)
            return;


        WeaponModel weaponModel = player.weaponVisuals.CurrentWeaponModel();

        weaponModel.transform.LookAt(aim);
        weaponModel.gunPoint.LookAt(aim);
        player.weaponVisuals.RefreshLeftHandTarget();


        Transform gunPoint = player.weapon.GunPoint();
        Vector3 laserDirection = player.weapon.BulletDirection();

        float laserTipLenght = .5f;
        float gunDistance = player.weapon.CurrentWeapon().gunDistance;

        Vector3 endPoint = gunPoint.position + laserDirection * gunDistance;

        if (Physics.Raycast(gunPoint.position, laserDirection, out RaycastHit hit, gunDistance))
        {
            endPoint = hit.point;
            laserTipLenght = 0;
        }

        aimLaser.SetPosition(0, gunPoint.position);
        aimLaser.SetPosition(1, endPoint);
        aimLaser.SetPosition(2, endPoint + laserDirection * laserTipLenght);
    }
    private void UpdateAimPosition()
    {
        Transform target = Target();

        if (target != null && isLockingToTarget)
        {
            if(target.GetComponent<Renderer>() != null)
                aim.position = target.GetComponent<Renderer>().bounds.center;
            else
                aim.position = target.position;


            return;
        }   

        Vector3 mouseAimPosition = GetMouseAimWorldPosition();
        Vector3 aimDirection = ResolveFlatAimDirection(mouseAimPosition, true);
        Vector3 visualAimPosition = transform.position + aimDirection * Mathf.Max(minAimDistance, visualAimDistance);
        Vector3 rawFlatDirection = mouseAimPosition - transform.position;
        rawFlatDirection.y = 0f;
        bool aimPointIsTooClose = rawFlatDirection.sqrMagnitude < minAimDistance * minAimDistance;

        visualAimPosition.y = isAimingPrecisly && aimPointIsTooClose == false
            ? mouseAimPosition.y
            : transform.position.y + 1;
        aim.position = visualAimPosition;
    }

    private Vector3 ResolveFlatAimDirection(Vector3 aimPosition, bool updateLastDirection)
    {
        Vector3 flatDirection = aimPosition - transform.position;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude >= MinDirectionSqrMagnitude)
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

    public Vector3 StableAimDirection() => ResolveFlatAimDirection(GetMouseAimWorldPosition(), false);

    private Vector3 GetMouseAimWorldPosition()
    {
        Vector3 raycastAimPosition = GetMouseHitInfo().point;
        Vector3 flatOffset = raycastAimPosition - transform.position;
        flatOffset.y = 0f;

        if (flatOffset.sqrMagnitude >= minAimDistance * minAimDistance)
            return raycastAimPosition;

        return GetMouseGroundPlaneIntersection();
    }

    private Vector3 GetMouseGroundPlaneIntersection()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return transform.position + lastFlatAimDirection * minAimDistance;

        Ray ray = mainCamera.ScreenPointToRay(mouseInput);
        float aimHeight = transform.position.y + 1f;
        Plane aimPlane = new Plane(Vector3.up, new Vector3(0f, aimHeight, 0f));

        if (aimPlane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return transform.position + lastFlatAimDirection * minAimDistance;
    }




    public Transform Target()
    {
        Transform target = null;
        Transform hitTransform = GetMouseHitInfo().transform;

        if (hitTransform != null && hitTransform.GetComponent<Target>() != null)
        {
            target = hitTransform;
        }

        return target;
    }
    public Transform Aim() => aim;
    public bool CanAimPrecisly() => isAimingPrecisly;
    public RaycastHit GetMouseHitInfo()
    {
        Ray ray = Camera.main.ScreenPointToRay(mouseInput);

        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, aimLayerMask))
        {
            lastKnownMouseHit = hitInfo;
            return hitInfo;
        }

        return lastKnownMouseHit;
    }

    #region Camera Region

    private void UpdateCameraPosition()
    {
        cameraTarget.position =
                    Vector3.Lerp(cameraTarget.position, DesieredCameraPosition(), cameraSensetivity * Time.deltaTime);
    }

    private Vector3 DesieredCameraPosition()
    {
        float actualMaxCameraDistance = player.movement.moveInput.y < -.5f ? minCameraDistance : maxCameraDistance;

        Vector3 mouseAimPosition = GetMouseAimWorldPosition();
        Vector3 aimDirection = ResolveFlatAimDirection(mouseAimPosition, false);

        float distanceToDesierdPosition = Vector3.Distance(transform.position, mouseAimPosition);
        float clampedDistance = Mathf.Clamp(distanceToDesierdPosition, minCameraDistance, actualMaxCameraDistance);

        Vector3 desiredCameraPosition = transform.position + aimDirection * clampedDistance;
        desiredCameraPosition.y = transform.position.y + 1;

        return desiredCameraPosition;
    }

    #endregion

    private void AssignInputEvents()
    {
        controls = player.controls;

        controls.Character.Aim.performed += context => mouseInput = context.ReadValue<Vector2>();
        controls.Character.Aim.canceled += context => mouseInput = Vector2.zero;
    }

}
