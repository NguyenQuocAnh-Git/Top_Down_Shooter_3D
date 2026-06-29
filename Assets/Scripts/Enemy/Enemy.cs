using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public enum EnemyType { Melee, Range, Boss ,Random}

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    public EnemyType enemyType;
    public LayerMask whatIsAlly;
    public LayerMask whatIsPlayer;
    
    [Header("Idle data")]
    public float idleTime;
    public float aggresionRange;

    [Header("Move data")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 3;
    public float turnSpeed;
    private bool manualMovement;
    private bool manualRotation;

    [SerializeField] private Transform[] patrolPoints;
    private Vector3[] patrolPointsPosition;
    private int currentPatrolIndex;

    public bool inBattleMode { get; private set; }
    protected bool isMeleeAttackReady;

    public Transform player {  get; private set; }
    public Animator anim { get; private set; }
    public NavMeshAgent agent { get; private set; }
    public EnemyStateMachine stateMachine { get; private set; }
    public Enemy_Visuals visuals { get; private set; }

    public Enemy_Health health { get; private set; }

    public Ragdoll ragdoll { get; private set; }

    public Enemy_DropController dropController { get; private set; }
    public AudioManager audioManager { get; private set; }
    public bool CanProcessAnimationEvents => stateMachine != null && stateMachine.currentState != null;
    private float nextCoopTargetRefresh;

    protected virtual void Awake()
    {
        stateMachine = new EnemyStateMachine();

        health = GetComponent<Enemy_Health>();
        ragdoll = GetComponent<Ragdoll>();
        visuals = GetComponent<Enemy_Visuals>();
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        dropController = GetComponent<Enemy_DropController>();
        if (GameSessionData.IsCoopSession)
            RefreshCoopPlayerTarget();
        else
        {
            GameObject scenePlayer = GameObject.Find("Player");
            player = scenePlayer != null ? scenePlayer.transform : null;
        }
    }

    protected virtual void Start()
    {
        InitializePatrolPoints();
        audioManager = AudioManager.instance;
    }

  

    protected virtual void Update()
    {
        if (GameSessionData.IsCoopSession && Time.time >= nextCoopTargetRefresh)
        {
            nextCoopTargetRefresh = Time.time + 0.5f;
            RefreshCoopPlayerTarget();
        }

        if (ShouldEnterBattleMode())
            EnterBattleMode();
    }

    protected virtual void InitializePerk()
    {

    }

    public virtual void MakeEnemyVIP()
    {
        int additionalHealth = Mathf.RoundToInt(health.currentHealth * 1.5f);

        health.currentHealth += additionalHealth;

        transform.localScale = transform.localScale * 1.15f;
    }

    protected bool ShouldEnterBattleMode()
    {
        if (IsPlayerInAgrresionRange() && !inBattleMode)
        {
            EnterBattleMode();
            return true;
        }

        return false;
    }

    public virtual void EnterBattleMode()
    {
        inBattleMode = true;
    }

    public virtual void GetHit(int damage)
    {
        EnterBattleMode();
        health.ReduceHealth(damage);

        if (health.ShouldDie())
            Die();
    }

    public virtual void Die()
    {
        dropController.DropItems();


        anim.enabled = false;
        agent.isStopped = true;
        agent.enabled = false;

        ragdoll.RagdollActive(true);

        MissionObject_HuntTarget huntTarget = GetComponent<MissionObject_HuntTarget>();
        huntTarget?.InvokeOnTargetKilled();
    }

    public virtual void MeleeAttackCheck(Transform[] damagePoints, float attackCheckRadius,GameObject fx,int damage)
    {
        if (isMeleeAttackReady == false)
            return;

        foreach (Transform attackPoint in damagePoints)
        {
            if (TryApplyMeleeDamage(attackPoint.position, attackCheckRadius, damage, fx, attackPoint))
                return;
        }
    }

    private bool TryApplyMeleeDamage(Vector3 attackPoint, float attackCheckRadius, int damage, GameObject fx, Transform fxAnchor)
    {
        Collider[] detectedHits = Physics.OverlapSphere(attackPoint, attackCheckRadius, whatIsPlayer);

        for (int i = 0; i < detectedHits.Length; i++)
        {
            if (TryDamageTarget(detectedHits[i], damage, fx, fxAnchor))
                return true;
        }

        if (GameSessionData.IsCoopSession == false)
            return false;

        Collider[] coopHits = Physics.OverlapSphere(attackPoint, attackCheckRadius);
        for (int i = 0; i < coopHits.Length; i++)
        {
            if (coopHits[i].GetComponentInParent<NetworkPlayerHitbox>() == null)
                continue;

            if (TryDamageTarget(coopHits[i], damage, fx, fxAnchor))
                return true;
        }

        return false;
    }

    private bool TryDamageTarget(Collider hitCollider, int damage, GameObject fx, Transform fxAnchor)
    {
        IDamagable damagable = hitCollider.GetComponentInParent<IDamagable>();
        if (damagable == null)
            return false;

        damagable.TakeDamage(damage);
        isMeleeAttackReady = false;

        if (fx != null && fxAnchor != null && ObjectPool.instance != null)
        {
            GameObject newAttackFx = ObjectPool.instance.GetObject(fx, fxAnchor);
            ObjectPool.instance.ReturnObject(newAttackFx, 1);
        }

        return true;
    }

    public void EnableMeleeAttackCheck(bool enable) => isMeleeAttackReady = enable;


    public virtual void BulletImpact( Vector3 force,Vector3 hitPoint,Rigidbody rb)
    {
        if(health.ShouldDie())
            StartCoroutine(DeathImpactCourutine(force,hitPoint,rb));
    }
    private IEnumerator DeathImpactCourutine(Vector3 force, Vector3 hitPoint, Rigidbody rb)
    {
        yield return new WaitForSeconds(.1f);

        rb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
    }

    public void FaceTarget(Vector3 target,float turnSpeed = 0)
    {
        Quaternion targetRotation = Quaternion.LookRotation(target - transform.position);

        Vector3 currentEulerAngels = transform.rotation.eulerAngles;

        if (turnSpeed == 0)
            turnSpeed = this.turnSpeed;

        float yRotation = 
            Mathf.LerpAngle(currentEulerAngels.y, targetRotation.eulerAngles.y, turnSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(currentEulerAngels.x, yRotation, currentEulerAngels.z);
    }

    

    #region Animation events
    public void ActivateManualMovement(bool manualMovement) => this.manualMovement = manualMovement;
    public bool ManualMovementActive() => manualMovement;

    public void ActivateManualRotation(bool manualRotation) => this.manualRotation = manualRotation;
    public bool ManualRotationActive() => manualRotation;
    public void AnimationTrigger()
    {
        if (CanProcessAnimationEvents)
            stateMachine.currentState.AnimationTrigger();
    }



    public virtual void AbilityTrigger()
    {
        if (CanProcessAnimationEvents)
            stateMachine.currentState.AbilityTrigger();
    }

    #endregion

    #region Patrol logic
    public Vector3 GetPatrolDestination()
    {
        Vector3 destination = patrolPointsPosition[currentPatrolIndex];

        currentPatrolIndex++;

        if (currentPatrolIndex >= patrolPoints.Length)
            currentPatrolIndex = 0;

        return destination;
    }
    private void InitializePatrolPoints()
    {
        patrolPointsPosition = new Vector3[patrolPoints.Length];

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            patrolPointsPosition[i] = patrolPoints[i].position;
            patrolPoints[i].gameObject.SetActive(false);
        }
    }

    #endregion

    public bool IsPlayerInAgrresionRange() => player != null && Vector3.Distance(transform.position, player.position) < aggresionRange;

    private void RefreshCoopPlayerTarget()
    {
        NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
        float nearestDistance = float.MaxValue;
        Transform nearest = null;

        foreach (NetworkPlayer networkPlayer in players)
        {
            if (networkPlayer.Health != null && networkPlayer.Health.IsDead)
                continue;

            float distance = (networkPlayer.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = networkPlayer.transform;
        }

        if (nearest != null)
            player = nearest;
    }
    protected virtual void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, aggresionRange);
    }
}
