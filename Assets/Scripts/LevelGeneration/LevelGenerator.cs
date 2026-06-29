using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

[System.Serializable]
public struct CoopLevelPartState
{
    public int prefabIndex;
    public Vector3 position;
    public Quaternion rotation;

    public CoopLevelPartState(int prefabIndex, Vector3 position, Quaternion rotation)
    {
        this.prefabIndex = prefabIndex;
        this.position = position;
        this.rotation = rotation;
    }
}

public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator instance;

    // Enemies
    private List<Enemy> enemyList;

    // NavMesh
    [SerializeField] private NavMeshSurface navMeshSurface;
    [Space]

    // Level parts
    [SerializeField] private Transform lastLevelPart;
    [SerializeField] private List<Transform> levelParts;
    private List<Transform> currentLevelParts;
    private List<Transform> generatedLevelParts = new List<Transform>();
    private readonly List<CoopLevelPartState> coopLayout = new List<CoopLevelPartState>();
    private bool coopHostGeneration;

    public event System.Action<IReadOnlyList<CoopLevelPartState>, IReadOnlyList<Enemy>> CoopHostGenerationFinished;

    // Snap points
    [SerializeField] private SnapPoint nextSnapPoint;
    private SnapPoint defaultSnapPoint;

    // Cooldown
    [Space]
    [SerializeField] private float generationCooldown;
    private float cooldownTimer;
    private bool generationOver = true;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        enemyList = new List<Enemy>();
        defaultSnapPoint = nextSnapPoint;
    }


    private void Update()
    {
        if (generationOver)
            return;

        cooldownTimer -= Time.deltaTime;

        if (cooldownTimer < 0)
        {
            if (currentLevelParts.Count > 0)
            {
                cooldownTimer = generationCooldown;
                GenerateNextLevelPart();
            }
            else if (generationOver == false)
            {
                FinishGeneration();
            }
        }
    }

    [ContextMenu("Restart generation")]
    public void InitializeGeneration()
    {
        nextSnapPoint = defaultSnapPoint;
        generationOver = false;
        currentLevelParts = new List<Transform>(levelParts);
        coopLayout.Clear();

        DestroyOldLevelPartsAndEnemies();
    }

    public void InitializeCoopHostGeneration(int seed)
    {
        coopHostGeneration = true;
        Random.InitState(seed);
        InitializeGeneration();
    }

    public void ApplyCoopLayout(IReadOnlyList<CoopLevelPartState> layout)
    {
        coopHostGeneration = false;
        generationOver = true;
        DestroyOldLevelPartsAndEnemies();

        if (layout == null)
            return;

        foreach (CoopLevelPartState state in layout)
        {
            Transform prefab = state.prefabIndex < 0
                ? lastLevelPart
                : state.prefabIndex < levelParts.Count ? levelParts[state.prefabIndex] : null;

            if (prefab == null)
            {
                Debug.LogError($"[COOP] Invalid level part index {state.prefabIndex} received from host.");
                continue;
            }

            Transform newPart = Instantiate(prefab, state.position, state.rotation);
            generatedLevelParts.Add(newPart);
            enemyList.AddRange(newPart.GetComponent<LevelPart>().MyEnemies());
        }

        navMeshSurface.BuildNavMesh();
    }

    private void DestroyOldLevelPartsAndEnemies()
    {
        foreach (Enemy enemy in enemyList)
        {
            Destroy(enemy.gameObject);
        }

        foreach (Transform t in generatedLevelParts)
        {
            Destroy(t.gameObject);
        }

        generatedLevelParts = new List<Transform>();
        enemyList = new List<Enemy>();
    }

    private void FinishGeneration()
    {
        generationOver = true;
        GenerateNextLevelPart();

        navMeshSurface.BuildNavMesh();

        foreach (Enemy enemy in enemyList)
        {
            enemy.transform.parent = null;
            enemy.gameObject.SetActive(true);
        }

        if (coopHostGeneration)
        {
            CoopHostGenerationFinished?.Invoke(coopLayout, enemyList);
            return;
        }

        MissionManager.instance.StartMission();
    }

    [ContextMenu("Create next level part")]
    private void GenerateNextLevelPart()
    {
        Transform newPart = null;
        int prefabIndex = -1;

        if (generationOver)
            newPart = Instantiate(lastLevelPart);
        else
        {
            Transform selectedPart = ChooseRandomPart();
            prefabIndex = levelParts.IndexOf(selectedPart);
            newPart = Instantiate(selectedPart);
        }

        generatedLevelParts.Add(newPart);

        LevelPart levelPartScript = newPart.GetComponent<LevelPart>();
        levelPartScript.SnapAndAlignPartTo(nextSnapPoint);

        if (levelPartScript.IntersectionDetected())
        {
            InitializeGeneration();
            return;
        }

        nextSnapPoint = levelPartScript.GetExitPoint();
        enemyList.AddRange(levelPartScript.MyEnemies());

        if (coopHostGeneration)
            coopLayout.Add(new CoopLevelPartState(prefabIndex, newPart.position, newPart.rotation));
    }

    private Transform ChooseRandomPart()
    {
        int randomIndex = Random.Range(0, currentLevelParts.Count);

        Transform choosenPart = currentLevelParts[randomIndex];

        currentLevelParts.RemoveAt(randomIndex);

        return choosenPart;
    }

    public Enemy GetRandomEnemy()
    {
        int randomIndex = Random.Range(0,enemyList.Count);

        return enemyList[randomIndex];
    }

    public List<Enemy> GetEnemyList() => enemyList;
}
