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
    public UI_MissionSelection missionSelection { get; private set; }
    public UI_ComicPanel comicPanel { get; private set; }
    public UI_GameOver gameOverUI { get; private set; }
    public UI_Settings settingsUI { get; private set; }
    public GameObject victoryScreenUI;
    public GameObject pauseUI;


    [SerializeField] private GameObject[] UIElements;
    private readonly List<GameObject> runtimeUIElements = new List<GameObject>();
    private UI_CoopMenu coopMenu;

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
        missionSelection = GetComponentInChildren<UI_MissionSelection>(true);
        comicPanel = GetComponentInChildren<UI_ComicPanel>(true);
        gameOverUI = GetComponentInChildren<UI_GameOver>(true);
        settingsUI = GetComponentInChildren<UI_Settings>(true);
    }
    private void Start()
    {
        AssignInputsUI();
        coopMenu = UI_CoopMenu.EnsureCreated(this);

        StartCoroutine(ChangeImageAlpha(0, 1.5f, null));

        if (ShouldAutoStartGameplay())
        {
            if (GameSessionData.IsCoopSession)
                CoopGameplayBridge.HandleCoopGameplaySceneStart(this);
            else
                StartTheGame();
        }
    }

    private void OnEnable()
    {
        CoopNetworkManager.Instance.OnMissionSelectionStarted += HandleCoopMissionSelectionStarted;
        CoopNetworkManager.Instance.OnMissionSelected += HandleCoopMissionSelected;
        CoopNetworkManager.Instance.OnWeaponSelectionStarted += HandleCoopWeaponSelectionStarted;
        CoopNetworkManager.Instance.OnComicStarted += HandleCoopComicStarted;
        CoopNetworkManager.Instance.OnCoopPlayGame += HandleCoopPlayGame;
        CoopNetworkManager.Instance.OnLobbyReturned += HandleCoopLobbyReturned;
        CoopNetworkManager.Instance.OnMissionPreviewed += HandleCoopMissionPreviewed;
    }

    private void OnDisable()
    {
        if (CoopNetworkManager.Instance == null)
            return;

        CoopNetworkManager.Instance.OnMissionSelectionStarted -= HandleCoopMissionSelectionStarted;
        CoopNetworkManager.Instance.OnMissionSelected -= HandleCoopMissionSelected;
        CoopNetworkManager.Instance.OnWeaponSelectionStarted -= HandleCoopWeaponSelectionStarted;
        CoopNetworkManager.Instance.OnComicStarted -= HandleCoopComicStarted;
        CoopNetworkManager.Instance.OnCoopPlayGame -= HandleCoopPlayGame;
        CoopNetworkManager.Instance.OnLobbyReturned -= HandleCoopLobbyReturned;
        CoopNetworkManager.Instance.OnMissionPreviewed -= HandleCoopMissionPreviewed;
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

        if (missionSelection != null
            && uiToSwitchOn == missionSelection.gameObject
            && (CoopNetworkManager.Instance == null || CoopNetworkManager.Instance.IsInRoom == false))
        {
            missionSelection.ConfigureForSinglePlayer();
        }

        if (weaponSelection != null
            && uiToSwitchOn == weaponSelection.gameObject
            && (CoopNetworkManager.Instance == null || CoopNetworkManager.Instance.IsInRoom == false))
        {
            weaponSelection.ConfigureForSinglePlayer();
        }

        if (comicPanel != null
            && uiToSwitchOn == comicPanel.gameObject
            && (CoopNetworkManager.Instance == null || CoopNetworkManager.Instance.IsInRoom == false))
        {
            comicPanel.ConfigureForSinglePlayer();
            comicPanel.SetPointerAdvanceEnabled(true);
            comicPanel.SetPlayButtonInteractable(true);
        }
    }

    public void StartTheGame()
    {
        if (CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.IsInRoom)
        {
            CoopNetworkManager.Instance.RequestCoopPlayGame();
            return;
        }

        if (ShouldLoadGameplayScene())
        {
            GameSessionData.MarkGameplayRequestedFromMenu();
            StartCoroutine(LoadSceneSequence(gameplaySceneName));
            return;
        }

        StartCoroutine(StartGameSequence());
    }

    private void HandleCoopMissionSelectionStarted()
    {
        if (missionSelection == null)
            return;

        GameSessionData.ClearGameplaySession();
        SwitchTo(missionSelection.gameObject);
        missionSelection.ConfigureForCoop(CoopNetworkManager.Instance.CanLocalPlayerSelectMission, HandleCoopMissionBack);
    }

    private void HandleCoopMissionSelected(string missionName)
    {
        missionSelection?.SelectMissionByName(missionName);
    }

    private void HandleCoopMissionPreviewed(string missionName)
    {
        if (missionSelection == null)
            return;

        if (string.IsNullOrEmpty(missionName))
            missionSelection.ClearRemotePreview();
        else
            missionSelection.PreviewMissionByName(missionName);
    }

    private void HandleCoopWeaponSelectionStarted()
    {
        if (weaponSelection != null)
        {
            SwitchTo(weaponSelection.gameObject);
            weaponSelection.ConfigureForCoop(CoopNetworkManager.Instance.IsHosting, HandleCoopReturnToLobby);
        }
    }

    private void HandleCoopComicStarted()
    {
        if (comicPanel == null)
            return;

        SwitchTo(comicPanel.gameObject);
        comicPanel.ConfigureForCoop();
        comicPanel.SetPointerAdvanceEnabled(false);
        comicPanel.ConfigurePlayButton(CoopNetworkManager.Instance.CanLocalPlayerPressCoopPlay, StartTheGame);
    }

    private void HandleCoopPlayGame()
    {
        GameSessionData.MarkGameplayRequestedFromMenu();
        Debug.Log("COOP PLAY GAME received by UI. Fusion is loading GameplayScene.");
    }

    private void HandleCoopMissionBack() => HandleCoopReturnToLobby();

    private void HandleCoopReturnToLobby()
    {
        CoopNetworkManager.Instance.ReturnCoopSetupToLobby();
    }

    private void HandleCoopLobbyReturned()
    {
        if (coopMenu == null)
            coopMenu = UI_CoopMenu.EnsureCreated(this);

        if (coopMenu == null)
            return;

        SwitchTo(coopMenu.gameObject);
        coopMenu.ShowExistingLobby();
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
        if (GameSessionData.IsCoopSession)
        {
            if (ControlsManager.instance != null)
                ControlsManager.instance.controls.Character.UIPause.performed += ctx => PauseSwitch();

            return;
        }

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
