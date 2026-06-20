using Fusion;
using UnityEngine;

public class NetworkPlayerHitbox : HitBox, IDamagable
{
    private NetworkPlayerHealth health;

    protected override void Awake()
    {
        base.Awake();
        health = GetComponentInParent<NetworkPlayerHealth>();
    }

    public override void TakeDamage(int damage)
    {
        if (GameSessionData.IsCoopSession == false)
            return;

        int scaledDamage = Mathf.RoundToInt(damage * damageMultiplier);

        if (health != null && health.Object != null && health.Object.HasStateAuthority)
            health.ApplyDamageFromHost(scaledDamage);
    }

    public NetworkObject GetNetworkObject()
    {
        return health != null ? health.Object : null;
    }
}
