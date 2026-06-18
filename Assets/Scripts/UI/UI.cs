using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public static UI instance;

    public UI_InGame inGameUI { get; private set; }
    public UI_WeaponSelection weaponSelection { get; private set; }
    public UI_GameOver gameOverUI { get; private set; }
    public UI_Settings settingsUI { get; private set; }
    public GameObject victoryScreenUI;
    public GameObject pauseUI;


    [SerializeField] private GameObject[] UIElements;
    private readonly List<GameObject> runtimeUIElements = new List<GameObject>();

    [Header("Fade Image")]
    [SerializeField] private Image fadeImage;

    [Header("Scene Flow")]
    [SerializeField] private string menuSceneName = GameSessionData.MenuSceneName;
    [SerializeField] private string gameplaySceneName = GameSessionData.GameplaySceneName;

    private void Awake()
    {
        instance = this;
        inGameUI = GetComponentInChildren<UI_InGame>(true);
        weaponSelection = GetComponentInChildren<UI_WeaponSelection>(true);
        gameOverUI = GetComponentInChildren<UI_GameOver>(true);
        settingsUI = GetComponentInChildren<UI_Settings>(true);
    }
    private void Start()
    {
        AssignInputsUI();
        UI_CoopMenu.EnsureCreated(this);

        StartCoroutine(ChangeImageAlpha(0, 1.5f, null));

        if (ShouldAutoStartGameplay())
            StartTheGame();
    }

    public void SwitchTo(GameObject uiToSwitchOn)
    {
        if (ShouldReturnToMenuScene(uiToSwitchOn))
        {
            LoadMenuScene();
            return;
        }

        foreach (GameObject go in UIElements)
        {
            go.SetActive(false);
        }

        foreach (GameObject go in runtimeUIElements)
        {
            if (go != null)
                go.SetActive(false);
        }
         
        uiToSwitchOn.SetActive(true);

        if (settingsUI != null && uiToSwitchOn == settingsUI.gameObject)
            settingsUI.LoadSettings();
    }

    public void StartTheGame()
    {
        if (ShouldLoadGameplayScene())
        {
            GameSessionData.MarkGameplayRequestedFromMenu();
            StartCoroutine(LoadSceneSequence(gameplaySceneName));
            return;
        }

        StartCoroutine(StartGameSequence());
    }

    public void QuitTheGame() => Application.Quit();

    public void RegisterRuntimeUIElement(GameObject uiElement)
    {
        if (uiElement == null || runtimeUIElements.Contains(uiElement))
            return;

        runtimeUIElements.Add(uiElement);
    }
    public void StartLevelGeneration()
    {
        if (LevelGenerator.instance != null)
            LevelGenerator.instance.InitializeGeneration();
    }

    public void RestartTheGame()
    {
        if (GameManager.instance != null)
            StartCoroutine(ChangeImageAlpha(1, 1f, GameManager.instance.RestartScene));
    }

    public void LoadMenuScene()
    {
        PrepareForMenuSceneLoad();
        GameSessionData.ClearGameplaySession();
        StartCoroutine(LoadSceneSequence(menuSceneName));
    }

    public void PauseSwitch()
    {
        bool gamePaused = pauseUI.activeSelf;

        if (gamePaused)
        {
            SwitchTo(inGameUI.gameObject);
            ControlsManager.instance?.SwitchToCharacterControls();
            TimeManager.instance?.ResumeTime();
        }
        else
        {
            SwitchTo(pauseUI);
            ControlsManager.instance?.SwitchToUIControls();
            TimeManager.instance?.PauseTime();
        }
    }

    public void ShowGameOverUI(string message = "GAME OVER!")
    {
        SwitchTo(gameOverUI.gameObject);
        gameOverUI.ShowGameOverMessage(message);
    }

    public void ShowVictoryScreenUI()
    {
        StartCoroutine(ChangeImageAlpha(1, 1.5f, SwitchToVictoryScreenUI));
    }

    private void SwitchToVictoryScreenUI()
    {
        SwitchTo(victoryScreenUI);

        Color color = fadeImage.color;
        color.a = 0;

        fadeImage.color = color;

    }

    private void AssignInputsUI()
    {
        if (GameManager.instance == null || GameManager.instance.player == null)
            return;

        PlayerControls controls = GameManager.instance.player.controls;

        controls.UI.UIPause.performed += ctx => PauseSwitch();
    }

    private IEnumerator StartGameSequence()
    {
        if (GameManager.instance == null)
            yield break;

        bool quickStart = GameManager.instance.quickStart;

        //THIS SHOULD BE UNCOMMENTED BEFORE MAKING A BUILD
        if (quickStart == false)
        {
            fadeImage.color = Color.black;
            StartCoroutine(ChangeImageAlpha(1, 1, null));
            yield return new WaitForSeconds(1);

        }

        yield return null;
        SwitchTo(inGameUI.gameObject);
        GameManager.instance.GameStart();
        StartLevelGeneration();
        GameSessionData.ClearGameplayRequest();

        if(quickStart)
            StartCoroutine(ChangeImageAlpha(0,.1f, null));
        else
            StartCoroutine(ChangeImageAlpha(0,1f, null));
    }

    private IEnumerator LoadSceneSequence(string sceneName)
    {
        yield return ChangeImageAlpha(1, 1f, null);
        SceneManager.LoadScene(sceneName);
    }

    private void PrepareForMenuSceneLoad()
    {
        if (pauseUI != null)
            pauseUI.SetActive(false);

        TimeManager.instance?.ResumeTime();
        Time.timeScale = 1;
    }

    private bool ShouldAutoStartGameplay()
    {
        if (GameManager.instance == null)
            return false;

        return IsGameplayScene() || GameSessionData.GameplayRequestedFromMenu || GameManager.instance.quickStart;
    }

    private bool ShouldLoadGameplayScene()
    {
        return IsGameplayScene() == false && (GameManager.instance == null || LevelGenerator.instance == null);
    }

    private bool ShouldReturnToMenuScene(GameObject uiToSwitchOn)
    {
        return IsGameplayScene()
            && uiToSwitchOn != null
            && uiToSwitchOn.name == "MainMenu_UI";
    }

    private bool IsGameplayScene()
    {
        return SceneManager.GetActiveScene().name == gameplaySceneName;
    }

    private IEnumerator ChangeImageAlpha(float targetAlpha, float duration,System.Action onComplete)
    {
        float time = 0;
        Color currentColor = fadeImage.color;
        float startAlpha = currentColor.a;

        while(time < duration)
        {
            time += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);

            fadeImage.color = new Color(currentColor.r,currentColor.g, currentColor.b,alpha);
            yield return null;
        }

        fadeImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, targetAlpha);


        // Call the cimpletion method if it exists
        onComplete?.Invoke();
    }

    [ContextMenu("Assign Audio To Buttons")]
    public void AssignAudioListenesrsToButtons()
    {
        UI_Button[] buttons = FindObjectsOfType<UI_Button>(true);

        foreach (var button in buttons)
        {
            button.AssignAudioSource();
        }
    }
}
