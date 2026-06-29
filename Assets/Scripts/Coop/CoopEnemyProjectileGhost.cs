using System;
using System.Collections.Generic;
using UnityEngine;

public enum CoopEnemyProjectileKind : byte
{
    Bullet = 0,
    Grenade = 1,
    Axe = 2
}

// Visual-only projectile used on clients. Physics, collision and damage stay on
// the host; this object only reproduces the visible trajectory.
public class CoopEnemyProjectileGhost : MonoBehaviour
{
    private Rigidbody body;
    private float remainingLifetime;
    private bool spin;

    public static void ReportHostSpawn(
        CoopEnemyProjectileKind kind,
        Enemy source,
        GameObject projectile,
        Vector3 velocity,
        float lifetime)
    {
        if (GameSessionData.IsCoopSession == false
            || CoopNetworkManager.Instance.IsHosting == false
            || source == null
            || projectile == null)
            return;

        NetworkEnemy networkEnemy = source.GetComponent<NetworkEnemy>();
        if (networkEnemy == null)
            return;

        var payload = new List<byte>(49) { (byte)kind };
        AddInt(payload, networkEnemy.Id);
        AddVector3(payload, projectile.transform.position);
        AddQuaternion(payload, projectile.transform.rotation);
        AddVector3(payload, velocity);
        AddFloat(payload, Mathf.Max(0.1f, lifetime));
        CoopNetworkManager.Instance.BroadcastCoopEnemyProjectile(payload.ToArray());
    }

    public static void SpawnClientVisual(byte[] payload)
    {
        if (payload == null || payload.Length < 49 || ObjectPool.instance == null)
            return;

        int offset = 0;
        CoopEnemyProjectileKind kind = (CoopEnemyProjectileKind)payload[offset++];
        int enemyId = ReadInt(payload, ref offset);
        Vector3 position = ReadVector3(payload, ref offset);
        Quaternion rotation = ReadQuaternion(payload, ref offset);
        Vector3 velocity = ReadVector3(payload, ref offset);
        float lifetime = ReadFloat(payload, ref offset);

        NetworkEnemy source = NetworkEnemy.Find(enemyId);
        GameObject prefab = ResolvePrefab(source, kind);
        if (prefab == null)
            return;

        GameObject projectile = ObjectPool.instance.GetObject(prefab, source.transform);
        if (projectile == null)
            return;

        projectile.transform.SetPositionAndRotation(position, rotation);
        ConfigureVisualOnly(projectile, kind, velocity, lifetime);
    }

    private static GameObject ResolvePrefab(NetworkEnemy source, CoopEnemyProjectileKind kind)
    {
        if (source == null || source.Enemy == null)
            return null;

        if (kind == CoopEnemyProjectileKind.Axe)
            return (source.Enemy as Enemy_Melee)?.axePrefab;

        Enemy_Range range = source.Enemy as Enemy_Range;
        if (range == null)
            return null;

        return kind == CoopEnemyProjectileKind.Grenade ? range.grenadePrefab : range.bulletPrefab;
    }

    private static void ConfigureVisualOnly(
        GameObject projectile,
        CoopEnemyProjectileKind kind,
        Vector3 velocity,
        float lifetime)
    {
        foreach (Collider collider in projectile.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        Enemy_Grenade grenade = projectile.GetComponent<Enemy_Grenade>();
        if (grenade != null)
            grenade.enabled = false;

        Enemy_Axe axe = projectile.GetComponent<Enemy_Axe>();
        if (axe != null)
            axe.enabled = false;

        Bullet bullet = projectile.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.BulletSetup(0, 0, Mathf.Max(1f, velocity.magnitude * lifetime), 0f, false);
            bullet.enabled = false;
        }

        CoopEnemyProjectileGhost ghost = projectile.GetComponent<CoopEnemyProjectileGhost>();
        if (ghost == null)
            ghost = projectile.AddComponent<CoopEnemyProjectileGhost>();

        ghost.Begin(kind, velocity, lifetime);
    }

    private void Begin(CoopEnemyProjectileKind kind, Vector3 velocity, float lifetime)
    {
        body = GetComponent<Rigidbody>();
        remainingLifetime = lifetime;
        spin = kind == CoopEnemyProjectileKind.Axe;

        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = kind == CoopEnemyProjectileKind.Grenade;
            body.velocity = velocity;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        remainingLifetime -= Time.deltaTime;

        if (spin)
            transform.Rotate(Vector3.right, 1600f * Time.deltaTime, Space.Self);

        if (remainingLifetime > 0f)
            return;

        if (ObjectPool.instance != null)
            ObjectPool.instance.ReturnObject(gameObject);
        else
            gameObject.SetActive(false);
    }

    private static void AddInt(List<byte> bytes, int value) => bytes.AddRange(BitConverter.GetBytes(value));
    private static void AddFloat(List<byte> bytes, float value) => bytes.AddRange(BitConverter.GetBytes(value));
    private static void AddVector3(List<byte> bytes, Vector3 value)
    {
        AddFloat(bytes, value.x); AddFloat(bytes, value.y); AddFloat(bytes, value.z);
    }
    private static void AddQuaternion(List<byte> bytes, Quaternion value)
    {
        AddFloat(bytes, value.x); AddFloat(bytes, value.y); AddFloat(bytes, value.z); AddFloat(bytes, value.w);
    }
    private static int ReadInt(byte[] bytes, ref int offset)
    {
        int value = BitConverter.ToInt32(bytes, offset); offset += 4; return value;
    }
    private static float ReadFloat(byte[] bytes, ref int offset)
    {
        float value = BitConverter.ToSingle(bytes, offset); offset += 4; return value;
    }
    private static Vector3 ReadVector3(byte[] bytes, ref int offset) =>
        new Vector3(ReadFloat(bytes, ref offset), ReadFloat(bytes, ref offset), ReadFloat(bytes, ref offset));
    private static Quaternion ReadQuaternion(byte[] bytes, ref int offset) =>
        new Quaternion(ReadFloat(bytes, ref offset), ReadFloat(bytes, ref offset), ReadFloat(bytes, ref offset), ReadFloat(bytes, ref offset));
}
