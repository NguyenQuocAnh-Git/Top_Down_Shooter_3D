using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class CoopLevelGenerationHost : MonoBehaviour
{
    private readonly List<NetworkEnemy> networkEnemies = new List<NetworkEnemy>();
    private NetworkRunner runner;
    private LevelGenerator generator;
    private bool initialized;

    public void Initialize(NetworkRunner activeRunner)
    {
        if (initialized)
            return;

        initialized = true;
        runner = activeRunner;
        generator = LevelGenerator.instance != null
            ? LevelGenerator.instance
            : FindObjectOfType<LevelGenerator>();

        if (generator == null)
        {
            Debug.LogError("[COOP] LevelGenerator not found in gameplay scene.");
            return;
        }

        CoopNetworkManager.Instance.OnCoopLevelLayoutReceived += HandleLevelLayout;
        CoopNetworkManager.Instance.OnCoopEnemyProjectileReceived += HandleEnemyProjectile;

        if (runner != null && runner.IsServer)
        {
            generator.CoopHostGenerationFinished += HandleHostGenerationFinished;
            generator.InitializeCoopHostGeneration(GameSessionData.LevelGenerationSeed);
            Debug.Log($"[COOP] Host started seeded level generation ({GameSessionData.LevelGenerationSeed}).");
        }
        else if (CoopNetworkManager.Instance.TryConsumePendingLevelLayout(out byte[] pendingLayout))
        {
            HandleLevelLayout(pendingLayout);
        }
    }

    private void HandleHostGenerationFinished(
        IReadOnlyList<CoopLevelPartState> layout,
        IReadOnlyList<Enemy> enemies)
    {
        RegisterEnemies(enemies, false);
        CoopNetworkManager.Instance.BroadcastCoopLevelLayout(EncodeLevelLayout(layout));
        CoopMissionSync.Instance?.StartHostMission();
        Debug.Log($"[COOP] Level ready: {layout.Count} parts, {networkEnemies.Count} host-authoritative enemies.");
    }

    private void HandleLevelLayout(byte[] payload)
    {
        if (runner != null && runner.IsServer)
            return;

        List<CoopLevelPartState> layout = DecodeLevelLayout(payload);
        generator.ApplyCoopLayout(layout);
        RegisterEnemies(generator.GetEnemyList(), true);

        foreach (Enemy enemy in generator.GetEnemyList())
            enemy.gameObject.SetActive(true);

        Debug.Log($"[COOP] Client applied host layout: {layout.Count} parts, {networkEnemies.Count} replicated enemies.");
    }

    private void RegisterEnemies(IReadOnlyList<Enemy> enemies, bool replica)
    {
        networkEnemies.Clear();
        if (enemies == null)
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null)
                continue;

            NetworkEnemy networkEnemy = enemy.GetComponent<NetworkEnemy>();
            if (networkEnemy == null)
                networkEnemy = enemy.gameObject.AddComponent<NetworkEnemy>();

            networkEnemy.Configure(i, replica);
            networkEnemies.Add(networkEnemy);
        }
    }

    private void HandleEnemyProjectile(byte[] payload)
    {
        if (runner != null && runner.IsServer == false)
            CoopEnemyProjectileGhost.SpawnClientVisual(payload);
    }

    private byte[] EncodeLevelLayout(IReadOnlyList<CoopLevelPartState> layout)
    {
        var bytes = new List<byte>();
        AddInt(bytes, GameSessionData.LevelGenerationSeed);
        AddInt(bytes, layout.Count);

        foreach (CoopLevelPartState state in layout)
        {
            AddInt(bytes, state.prefabIndex);
            AddVector3(bytes, state.position);
            AddQuaternion(bytes, state.rotation);
        }

        return bytes.ToArray();
    }

    private static List<CoopLevelPartState> DecodeLevelLayout(byte[] payload)
    {
        var result = new List<CoopLevelPartState>();
        if (payload == null || payload.Length < 8)
            return result;

        int offset = 0;
        int seed = ReadInt(payload, ref offset);
        int count = Mathf.Clamp(ReadInt(payload, ref offset), 0, 128);

        if (seed != GameSessionData.LevelGenerationSeed)
            Debug.LogWarning($"[COOP] Layout seed {seed} differs from session seed {GameSessionData.LevelGenerationSeed}.");

        for (int i = 0; i < count && offset + 32 <= payload.Length; i++)
        {
            int index = ReadInt(payload, ref offset);
            Vector3 position = ReadVector3(payload, ref offset);
            Quaternion rotation = ReadQuaternion(payload, ref offset);
            result.Add(new CoopLevelPartState(index, position, rotation));
        }

        return result;
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

    private void OnDestroy()
    {
        if (CoopNetworkManager.Instance != null)
        {
            CoopNetworkManager.Instance.OnCoopLevelLayoutReceived -= HandleLevelLayout;
            CoopNetworkManager.Instance.OnCoopEnemyProjectileReceived -= HandleEnemyProjectile;
        }

        if (generator != null)
            generator.CoopHostGenerationFinished -= HandleHostGenerationFinished;
    }
}
