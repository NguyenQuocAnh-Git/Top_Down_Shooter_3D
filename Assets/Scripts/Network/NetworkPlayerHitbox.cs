using Fusion;
using UnityEngine;

public class NetworkPlayerHitbox : HitBox, IDamagable
{
    private NetworkPlayerHealth health;

    protected override void Awake()
    {
        base.Awake();
        health = GetComponentInParent<NetworkPlayerHealth>();

        if (GameSessionData.IsCoopSession)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                gameObject.layer = playerLayer;
        }
    }

    public override void TakeDamage(int damage)
    {
        if (GameSessionData.IsCoopSession == false)
            return;

        int scaledDamage = Mathf.RoundToInt(damage * damageMultiplier);
        if (scaledDamage <= 0 || health == null)
            return;

        health.ApplyDamageFromHost(scaledDamage);
    }

    public NetworkObject GetNetworkObject()
    {
        return health != null ? health.Object : null;
    }
}
