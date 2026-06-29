using UnityEngine;

public class Enemy_AnimationEvents : MonoBehaviour
{
    private Enemy enemy;
    private Enemy_Melee enemyMelee;
    private Enemy_Boss enemyBoss;
    private NetworkEnemy networkEnemy;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        enemyMelee = GetComponentInParent<Enemy_Melee>();
        enemyBoss = GetComponentInParent<Enemy_Boss>();
        networkEnemy = GetComponentInParent<NetworkEnemy>();
    }

    private bool CanDriveGameplay => enemy != null
        && enemy.enabled
        && enemy.CanProcessAnimationEvents
        && (networkEnemy == null || networkEnemy.IsReplica == false);

    public void AnimationTrigger()
    {
        if (CanDriveGameplay)
            enemy.AnimationTrigger();
    }

    public void StartManualMovement()
    {
        if (CanDriveGameplay)
            enemy.ActivateManualMovement(true);
    }

    public void StopManualMovement()
    {
        if (CanDriveGameplay)
            enemy.ActivateManualMovement(false);
    }

    public void StartManualRotation()
    {
        if (CanDriveGameplay)
            enemy.ActivateManualRotation(true);
    }

    public void StopManualRotation()
    {
        if (CanDriveGameplay)
            enemy.ActivateManualRotation(false);
    }

    public void AbilityEvent()
    {
        if (CanDriveGameplay)
            enemy.AbilityTrigger();
    }

    public void EnableIK()
    {
        if (CanDriveGameplay && enemy.visuals != null)
            enemy.visuals.EnableIK(true, true, 1f);
    }

    public void EnableWeaponModel()
    {
        if (CanDriveGameplay == false || enemy.visuals == null)
            return;

        enemy.visuals.EnableWeaponModel(true);
        enemy.visuals.EnableSeconoderyWeaponModel(false);
    }

    public void BossJumpImpact()
    {
        if (CanDriveGameplay)
            enemyBoss?.JumpImpact();
    }

    public void BeginMeleeAttackCheck()
    {
        if (CanDriveGameplay == false)
            return;

        enemy.EnableMeleeAttackCheck(true);

        if (enemy.audioManager != null && enemyMelee?.meleeSFX != null)
            enemy.audioManager.PlaySFX(enemyMelee.meleeSFX.swoosh, true);
    }

    public void FinishMeleeAttackCheck()
    {
        if (CanDriveGameplay)
            enemy.EnableMeleeAttackCheck(false);
    }
}
