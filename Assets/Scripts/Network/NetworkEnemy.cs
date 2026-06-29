using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public struct NetworkEnemyFusionState : INetworkStruct
{
    public int id;
    public Vector3 position;
    public Quaternion rotation;
    public int health;
    public NetworkBool isDead;
    public int animationState;
    public int animationLayer;
    public float animationTime;
    public float animationLayerWeight;
    public uint animationBoolMask;
    public float idleAnimIndex;
    public float moveAnimSpeed;
    public float moveAnimIndex;
    public float attackAnimIndex;
    public float chaseIndex;
    public float recoveryIndex;
    public float attackAnimationSpeed;
    public float attackIndex;
    public float slashAttackIndex;
    public float advanceAnimIndex;
}

public struct NetworkEnemyState
{
    public int id;
    public Vector3 position;
    public Quaternion rotation;
    public int health;
    public bool isDead;
    public int animationState;
    public int animationLayer;
    public float animationTime;
    public float animationLayerWeight;
    public uint animationBoolMask;
    public float idleAnimIndex;
    public float moveAnimSpeed;
    public float moveAnimIndex;
    public float attackAnimIndex;
    public float chaseIndex;
    public float recoveryIndex;
    public float attackAnimationSpeed;
    public float attackIndex;
    public float slashAttackIndex;
    public float advanceAnimIndex;
}

// Enemy prefabs live inside generated level parts, so Phase 2 uses a stable
// host-assigned id and batched snapshots rather than making every level-part
// child a separately registered Fusion prefab.
public class NetworkEnemy : MonoBehaviour
{
    private const float InterpolationBackTime = 0.1f;

    private struct BufferedState
    {
        public NetworkEnemyState state;
        public float receivedAt;
    }

    private static readonly Dictionary<int, NetworkEnemy> instances = new Dictionary<int, NetworkEnemy>();

    private Enemy enemy;
    private Enemy_Health health;
    private Animator animator;
    private NavMeshAgent agent;
    private bool isReplica;
    private bool replicaDead;
    private int lastAnimationState;
    private int lastAnimationLayer = -1;
    private int lastSnapshotSequence;
    private readonly List<BufferedState> stateBuffer = new List<BufferedState>(8);
    private readonly HashSet<int> animatorFloatParameters = new HashSet<int>();
    private readonly List<int> animatorBoolParameters = new List<int>(16);

    private static readonly int IdleAnimIndexHash = Animator.StringToHash("IdleAnimIndex");
    private static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeedMultiplier");
    private static readonly int MoveAnimIndexHash = Animator.StringToHash("MoveAnimIndex");
    private static readonly int AttackAnimIndexHash = Animator.StringToHash("AttackAnimIndex");
    private static readonly int ChaseIndexHash = Animator.StringToHash("ChaseIndex");
    private static readonly int RecoveryIndexHash = Animator.StringToHash("RecoveryIndex");
    private static readonly int AttackAnimationSpeedHash = Animator.StringToHash("AttackAnimationSpeed");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int SlashAttackIndexHash = Animator.StringToHash("SlashAttackIndex");
    private static readonly int AdvanceAnimIndexHash = Animator.StringToHash("AdvanceAnimIndex");

    public int Id { get; private set; }
    public Enemy Enemy => enemy;
    public bool IsReplica => isReplica;

    public static NetworkEnemy Find(int id)
    {
        instances.TryGetValue(id, out NetworkEnemy result);
        return result;
    }

    public static int GetFusionStateCount(int capacity)
    {
        int highestId = -1;
        foreach (KeyValuePair<int, NetworkEnemy> pair in instances)
        {
            if (pair.Value != null && pair.Value.isReplica == false)
                highestId = Mathf.Max(highestId, pair.Key);
        }

        return Mathf.Clamp(highestId + 1, 0, capacity);
    }

    public static bool TryCaptureFusionState(int id, out NetworkEnemyFusionState state)
    {
        state = default;
        NetworkEnemy networkEnemy = Find(id);
        if (networkEnemy == null || networkEnemy.isReplica)
            return false;

        NetworkEnemyState source = networkEnemy.CaptureState();
        state = new NetworkEnemyFusionState
        {
            id = source.id,
            position = source.position,
            rotation = source.rotation,
            health = source.health,
            isDead = source.isDead,
            animationState = source.animationState,
            animationLayer = source.animationLayer,
            animationTime = source.animationTime,
            animationLayerWeight = source.animationLayerWeight,
            animationBoolMask = source.animationBoolMask,
            idleAnimIndex = source.idleAnimIndex,
            moveAnimSpeed = source.moveAnimSpeed,
            moveAnimIndex = source.moveAnimIndex,
            attackAnimIndex = source.attackAnimIndex,
            chaseIndex = source.chaseIndex,
            recoveryIndex = source.recoveryIndex,
            attackAnimationSpeed = source.attackAnimationSpeed,
            attackIndex = source.attackIndex,
            slashAttackIndex = source.slashAttackIndex,
            advanceAnimIndex = source.advanceAnimIndex
        };
        return true;
    }

    public static void ApplyFusionState(NetworkEnemyFusionState source, int sequence)
    {
        NetworkEnemy networkEnemy = Find(source.id);
        if (networkEnemy == null)
            return;

        networkEnemy.ApplyState(new NetworkEnemyState
        {
            id = source.id,
            position = source.position,
            rotation = source.rotation,
            health = source.health,
            isDead = source.isDead,
            animationState = source.animationState,
            animationLayer = source.animationLayer,
            animationTime = source.animationTime,
            animationLayerWeight = source.animationLayerWeight,
            animationBoolMask = source.animationBoolMask,
            idleAnimIndex = source.idleAnimIndex,
            moveAnimSpeed = source.moveAnimSpeed,
            moveAnimIndex = source.moveAnimIndex,
            attackAnimIndex = source.attackAnimIndex,
            chaseIndex = source.chaseIndex,
            recoveryIndex = source.recoveryIndex,
            attackAnimationSpeed = source.attackAnimationSpeed,
            attackIndex = source.attackIndex,
            slashAttackIndex = source.slashAttackIndex,
            advanceAnimIndex = source.advanceAnimIndex
        }, sequence);
    }

    public void Configure(int id, bool replica)
    {
        Id = id;
        isReplica = replica;
        enemy = GetComponent<Enemy>();
        health = GetComponent<Enemy_Health>();
        animator = GetComponentInChildren<Animator>(true);
        agent = GetComponent<NavMeshAgent>();
        instances[id] = this;

        if (enemy != null)
        {
            int visualSeed = unchecked(GameSessionData.LevelGenerationSeed * 397 ^ id * 7919 ^ (int)enemy.enemyType * 104729);
            Enemy_Range range = enemy as Enemy_Range;
            range?.ConfigureNetworkVisualSeed(visualSeed);
            GetComponent<Enemy_Visuals>()?.SetupLook(visualSeed);
        }

        CacheAnimatorParameters();

        if (isReplica)
        {
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1f;
            }

            if (enemy != null)
                enemy.enabled = false;

            if (agent != null)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.enabled = false;
            }
        }
    }

    public NetworkEnemyState CaptureState()
    {
        int animationLayer = ResolveActiveAnimationLayer();
        AnimatorStateInfo animation = animator != null
            ? animator.GetCurrentAnimatorStateInfo(animationLayer)
            : default;

        return new NetworkEnemyState
        {
            id = Id,
            position = transform.position,
            rotation = transform.rotation,
            health = health != null ? health.currentHealth : 0,
            isDead = health != null && health.currentHealth < 0,
            animationState = animation.fullPathHash,
            animationLayer = animationLayer,
            animationTime = animation.normalizedTime,
            animationLayerWeight = animator != null ? animator.GetLayerWeight(animationLayer) : 1f,
            animationBoolMask = CaptureAnimatorBoolMask(),
            idleAnimIndex = GetAnimatorFloat(IdleAnimIndexHash),
            moveAnimSpeed = GetAnimatorFloat(MoveAnimSpeedHash),
            moveAnimIndex = GetAnimatorFloat(MoveAnimIndexHash),
            attackAnimIndex = GetAnimatorFloat(AttackAnimIndexHash),
            chaseIndex = GetAnimatorFloat(ChaseIndexHash),
            recoveryIndex = GetAnimatorFloat(RecoveryIndexHash),
            attackAnimationSpeed = GetAnimatorFloat(AttackAnimationSpeedHash),
            attackIndex = GetAnimatorFloat(AttackIndexHash),
            slashAttackIndex = GetAnimatorFloat(SlashAttackIndexHash),
            advanceAnimIndex = GetAnimatorFloat(AdvanceAnimIndexHash)
        };
    }

    public void ApplyState(NetworkEnemyState state, int sequence)
    {
        if (isReplica == false || sequence <= lastSnapshotSequence)
            return;

        lastSnapshotSequence = sequence;

        stateBuffer.Add(new BufferedState
        {
            state = state,
            receivedAt = Time.unscaledTime
        });

        if (stateBuffer.Count > 8)
            stateBuffer.RemoveAt(0);

        if (health != null)
            health.currentHealth = state.health;

        SetAnimatorFloat(IdleAnimIndexHash, state.idleAnimIndex);
        SetAnimatorFloat(MoveAnimSpeedHash, state.moveAnimSpeed);
        SetAnimatorFloat(MoveAnimIndexHash, state.moveAnimIndex);
        SetAnimatorFloat(AttackAnimIndexHash, state.attackAnimIndex);
        SetAnimatorFloat(ChaseIndexHash, state.chaseIndex);
        SetAnimatorFloat(RecoveryIndexHash, state.recoveryIndex);
        SetAnimatorFloat(AttackAnimationSpeedHash, state.attackAnimationSpeed);
        SetAnimatorFloat(AttackIndexHash, state.attackIndex);
        SetAnimatorFloat(SlashAttackIndexHash, state.slashAttackIndex);
        SetAnimatorFloat(AdvanceAnimIndexHash, state.advanceAnimIndex);
        ApplyAnimatorBoolMask(state.animationBoolMask);

        if (animator != null && state.animationLayer >= 0 && state.animationLayer < animator.layerCount)
            animator.SetLayerWeight(state.animationLayer, state.animationLayerWeight);

        if (animator != null
            && state.animationState != 0
            && (state.animationState != lastAnimationState || state.animationLayer != lastAnimationLayer)
            && state.isDead == false)
        {
            animator.Play(state.animationState, state.animationLayer, state.animationTime);
            lastAnimationState = state.animationState;
            lastAnimationLayer = state.animationLayer;
        }

        if (state.isDead && replicaDead == false)
            ApplyReplicaDeath();
    }

    public void ApplyHostDamage(int damage)
    {
        if (isReplica || enemy == null || damage <= 0)
            return;

        if (health != null && health.currentHealth < 0)
            return;

        enemy.GetHit(damage);
    }

    private void LateUpdate()
    {
        if (isReplica == false || replicaDead)
            return;

        if (stateBuffer.Count == 0)
            return;

        float renderTime = Time.unscaledTime - InterpolationBackTime;

        while (stateBuffer.Count >= 2 && stateBuffer[1].receivedAt <= renderTime)
            stateBuffer.RemoveAt(0);

        if (stateBuffer.Count >= 2)
        {
            BufferedState from = stateBuffer[0];
            BufferedState to = stateBuffer[1];
            float duration = Mathf.Max(0.001f, to.receivedAt - from.receivedAt);
            float t = Mathf.Clamp01((renderTime - from.receivedAt) / duration);
            transform.position = Vector3.LerpUnclamped(from.state.position, to.state.position, t);
            transform.rotation = Quaternion.SlerpUnclamped(from.state.rotation, to.state.rotation, t);
            return;
        }

        NetworkEnemyState latest = stateBuffer[0].state;
        float smoothing = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, latest.position, smoothing);
        transform.rotation = Quaternion.Slerp(transform.rotation, latest.rotation, smoothing);
    }

    private void ApplyReplicaDeath()
    {
        replicaDead = true;

        if (animator != null)
            animator.enabled = false;

        Ragdoll ragdoll = GetComponent<Ragdoll>();
        ragdoll?.RagdollActive(true);
    }

    private void CacheAnimatorParameters()
    {
        animatorFloatParameters.Clear();
        animatorBoolParameters.Clear();
        if (animator == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float)
                animatorFloatParameters.Add(parameter.nameHash);
            else if (parameter.type == AnimatorControllerParameterType.Bool && animatorBoolParameters.Count < 32)
                animatorBoolParameters.Add(parameter.nameHash);
        }
    }

    private int ResolveActiveAnimationLayer()
    {
        if (animator == null || animator.layerCount <= 1)
            return 0;

        for (int layer = animator.layerCount - 1; layer >= 1; layer--)
        {
            if (animator.GetLayerWeight(layer) > 0.01f)
                return layer;
        }

        return 0;
    }

    private uint CaptureAnimatorBoolMask()
    {
        if (animator == null)
            return 0;

        uint mask = 0;
        for (int i = 0; i < animatorBoolParameters.Count; i++)
        {
            if (animator.GetBool(animatorBoolParameters[i]))
                mask |= 1u << i;
        }

        return mask;
    }

    private void ApplyAnimatorBoolMask(uint mask)
    {
        if (animator == null)
            return;

        for (int i = 0; i < animatorBoolParameters.Count; i++)
            animator.SetBool(animatorBoolParameters[i], (mask & (1u << i)) != 0);
    }

    private float GetAnimatorFloat(int parameterHash)
    {
        return animator != null && animatorFloatParameters.Contains(parameterHash)
            ? animator.GetFloat(parameterHash)
            : 0f;
    }

    private void SetAnimatorFloat(int parameterHash, float value)
    {
        if (animator != null && animatorFloatParameters.Contains(parameterHash))
            animator.SetFloat(parameterHash, value);
    }

    private void OnDestroy()
    {
        if (instances.TryGetValue(Id, out NetworkEnemy current) && current == this)
            instances.Remove(Id);
    }
}
