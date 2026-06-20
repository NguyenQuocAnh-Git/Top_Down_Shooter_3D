using Fusion;
using UnityEngine;

public static class CoopGameplayBridge
{
    public static bool IsCoopGameplayActive => GameSessionData.IsCoopSession;

    public static void HandleCoopGameplaySceneStart(UI ui)
    {
        if (IsCoopGameplayActive == false)
            return;

        GameSessionData.ClearGameplayRequest();

        if (ControlsManager.instance != null)
            ControlsManager.instance.SwitchToCoopCharacterControls();

        if (ui == null || ui.inGameUI == null)
            return;

        ui.SwitchTo(ui.inGameUI.gameObject);
    }

    public static void HandleSceneLoadDone(NetworkRunner runner)
    {
        if (IsCoopGameplayActive == false)
            return;

        CoopGameplayBootstrap.Initialize(runner);
    }
}

public class CoopGameplayBootstrap : MonoBehaviour
{
    public static CoopGameplayBootstrap Instance { get; private set; }

    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private NetworkObject networkPlayerPrefab;
    [SerializeField] private GameObject networkPlayerVisualSource;

    private GameObject visualTemplate;

    private void Awake()
    {
        if (GameSessionData.IsCoopSession == false)
            gameObject.SetActive(false);
    }

    public static void Initialize(NetworkRunner runner)
    {
        if (GameSessionData.IsCoopSession == false)
            return;

        CoopGameplayBootstrap bootstrap = FindObjectOfType<CoopGameplayBootstrap>(true);
        if (bootstrap == null)
        {
            Debug.LogError("[COOP] CoopGameplayBootstrap not found in GameplayScene.");
            return;
        }

        bootstrap.ActivateCoopPath(runner);
    }

    private void ActivateCoopPath(NetworkRunner runner)
    {
        EnsureVisualTemplate();

        DisableSinglePlayerObjects();

        gameObject.SetActive(true);

        Instance = this;
        Debug.Log($"[COOP] Gameplay bootstrap active. Runner={runner?.LocalPlayer}, seed={GameSessionData.LevelGenerationSeed}.");

        CoopPlayerSpawner spawner = GetComponent<CoopPlayerSpawner>();
        if (spawner == null)
            spawner = gameObject.AddComponent<CoopPlayerSpawner>();

        NetworkObject prefab = networkPlayerPrefab != null
            ? networkPlayerPrefab
            : Resources.Load<NetworkObject>("Coop/NetworkPlayer");

        if (prefab != null)
            spawner.SetNetworkPlayerPrefab(prefab);

        spawner.SpawnPlayers(runner);
    }

    public void AssemblePlayerVisual(NetworkPlayer networkPlayer)
    {
        if (networkPlayer == null || visualTemplate == null)
            return;

        if (networkPlayer.GetComponentInChildren<Animator>(true) != null)
            return;

        CoopNetworkPlayerVisualAssembler.AttachVisual(visualTemplate, networkPlayer);
    }

    private void EnsureVisualTemplate()
    {
        if (visualTemplate != null)
            return;

        if (networkPlayerVisualSource != null)
        {
            visualTemplate = networkPlayerVisualSource;
            visualTemplate.SetActive(false);
            CoopNetworkPlayerVisualAssembler.PrepareAssignedVisualSource(visualTemplate);
            return;
        }

        Player scenePlayer = GameManager.instance != null && GameManager.instance.player != null
            ? GameManager.instance.player
            : FindObjectOfType<Player>(true);

        if (scenePlayer == null)
        {
            Debug.LogError("[COOP] Could not build NetworkPlayer visual template. Scene Player not found.");
            return;
        }

        visualTemplate = CoopNetworkPlayerVisualAssembler.CreateVisualTemplate(scenePlayer, transform);

        if (visualTemplate == null)
            Debug.LogError("[COOP] Failed to create NetworkPlayer visual template.");
    }

    private static void DisableSinglePlayerObjects()
    {
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            GameManager.instance.player.gameObject.SetActive(false);
            return;
        }

        Player scenePlayer = FindObjectOfType<Player>();
        if (scenePlayer != null)
            scenePlayer.gameObject.SetActive(false);
    }

    public Transform GetSpawnPoint(int index)
    {
        if (spawnPoints == null || index < 0 || index >= spawnPoints.Length)
            return null;

        return spawnPoints[index];
    }
}
