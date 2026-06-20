using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class CoopPlayerSpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject networkPlayerPrefab;

    public void SetNetworkPlayerPrefab(NetworkObject prefab)
    {
        networkPlayerPrefab = prefab;
    }

    public void SpawnPlayers(NetworkRunner runner)
    {
        if (runner == null || runner.IsServer == false)
            return;

        if (networkPlayerPrefab == null)
        {
            Debug.LogError("[COOP] NetworkPlayer prefab is not assigned on CoopPlayerSpawner.");
            return;
        }

        CoopGameplayBootstrap bootstrap = CoopGameplayBootstrap.Instance;
        if (bootstrap == null)
        {
            Debug.LogError("[COOP] CoopGameplayBootstrap instance missing during spawn.");
            return;
        }

        List<PlayerRef> players = CoopNetworkManager.GetSortedActivePlayers(runner);

        for (int i = 0; i < players.Count; i++)
        {
            Transform spawnPoint = bootstrap.GetSpawnPoint(i);

            if (spawnPoint == null)
            {
                Debug.LogError($"[COOP] Missing spawn point index {i}.");
                continue;
            }

            NetworkObject spawnedPlayer = runner.Spawn(
                networkPlayerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                players[i]);

            runner.SetPlayerObject(players[i], spawnedPlayer);

            Debug.Log($"[COOP] Spawned NetworkPlayer for {players[i]} at index {i}.");
        }
    }
}
