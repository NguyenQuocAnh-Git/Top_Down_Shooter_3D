using Fusion;
using UnityEngine;

public class CoopNetworkBullet : Bullet
{
    private const float ReferenceBulletSpeed = 20f;

    private NetworkPlayerWeapon shooterWeapon;
    private bool visualOnly;

    public static CoopNetworkBullet EnsureOn(GameObject bulletObject)
    {
        CoopNetworkBullet bullet = bulletObject.GetComponent<CoopNetworkBullet>();
        if (bullet != null)
            return bullet;

        Bullet legacyBullet = bulletObject.GetComponent<Bullet>();
        GameObject impactFxPrefab = null;
        if (legacyBullet != null)
        {
            impactFxPrefab = legacyBullet.GetImpactFxPrefab();
            Object.Destroy(legacyBullet);
        }

        bullet = bulletObject.AddComponent<CoopNetworkBullet>();
        bullet.SetImpactFxPrefab(impactFxPrefab);
        return bullet;
    }

    public void Initialize(NetworkPlayerWeapon shooter, LayerMask allyLayerMask, int bulletDamage, float flyDistance, float impactForce)
    {
        visualOnly = false;
        shooterWeapon = shooter;

        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
            collider.enabled = true;

        BulletSetup(allyLayerMask, bulletDamage, flyDistance, impactForce);
    }

    public void InitializeVisualOnly(float flyDistance, Vector3 direction, float speed)
    {
        visualOnly = true;
        shooterWeapon = null;

        BulletSetup(default, 0, flyDistance, 0, enableCollider: false);

        Rigidbody bulletRigidbody = GetComponent<Rigidbody>();
        if (bulletRigidbody == null)
            return;

        Vector3 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        bulletRigidbody.mass = ReferenceBulletSpeed / speed;
        bulletRigidbody.velocity = normalizedDirection * speed;
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        if (visualOnly)
        {
            ReturnBulletToPool();
            return;
        }

        if (GameSessionData.IsCoopSession == false)
        {
            base.OnCollisionEnter(collision);
            return;
        }

        if (FriendlyFireBlocked(collision))
        {
            ReturnBulletToPool(10);
            return;
        }

        CreateImpactFx();
        ReturnBulletToPool();

        if (shooterWeapon != null)
            shooterWeapon.ReportLocalHit(collision);
    }

    private bool FriendlyFireBlocked(Collision collision)
    {
        if (GameSessionData.FriendlyFire)
            return false;

        return collision.gameObject.GetComponentInParent<NetworkPlayerHitbox>() != null;
    }
}
