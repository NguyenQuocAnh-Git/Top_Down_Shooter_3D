using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MissionSelection : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI missionDesciprtion;

    private Button backButton;
    private CanvasGroup rootCanvasGroup;
    private string defaultDescription = "Choose a mission";
    private UI_MissionSelectionButton previewedMissionButton;

    private void Awake()
    {
        CacheBackButton();
        CacheRootCanvasGroup();
    }

    public void UpdateMissionDesicription(string text)
    {
        missionDesciprtion.text = text;
    }

    public void SetSelectionEnabled(bool enabled, bool isCoopMode)
    {
        UI_MissionSelectionButton[] missionButtons = GetComponentsInChildren<UI_MissionSelectionButton>(true);

        foreach (UI_MissionSelectionButton missionButton in missionButtons)
        {
            missionButton.SetLocalInteractionEnabled(enabled, isCoopMode);

            Button button = missionButton.GetComponent<Button>();
            if (button != null)
                button.interactable = enabled;
        }

        defaultDescription = enabled ? "Choose a mission" : "Waiting for host to choose a mission";
        ShowDefaultDescription();
    }

    public void ConfigureForCoop(bool isHost, UnityEngine.Events.UnityAction hostBackAction)
    {
        ClearRemotePreview();
        SetRootInteractionEnabled(isHost);
        SetSelectionEnabled(isHost, true);
        defaultDescription = isHost
            ? "COOP HOST: Choose a mission for the squad. Back returns to the COOP lobby."
            : "COOP CLIENT: Waiting for host to choose a mission.";
        ShowDefaultDescription();
        CacheBackButton();

        if (backButton == null)
            return;

        backButton.gameObject.SetActive(isHost);

        if (isHost == false)
            return;

        backButton.interactable = true;
        SetButtonRaycastEnabled(backButton, true);
        backButton.onClick = new Button.ButtonClickedEvent();

        if (hostBackAction != null)
            backButton.onClick.AddListener(hostBackAction);
    }

    public void ConfigureForSinglePlayer()
    {
        ClearRemotePreview();
        SetRootInteractionEnabled(true);
        SetSelectionEnabled(true, false);
        defaultDescription = "Choose a mission";
        ShowDefaultDescription();
        CacheBackButton();

        if (backButton == null)
            return;

        backButton.gameObject.SetActive(true);
        backButton.interactable = true;
        SetButtonRaycastEnabled(backButton, true);
        backButton.onClick = new Button.ButtonClickedEvent();
        backButton.onClick.AddListener(ReturnToMainMenu);
    }

    public void SelectMissionByName(string missionName)
    {
        UI_MissionSelectionButton[] missionButtons = GetComponentsInChildren<UI_MissionSelectionButton>(true);

        foreach (UI_MissionSelectionButton missionButton in missionButtons)
        {
            Mission mission = missionButton.Mission;
            if (mission == null || mission.missionName != missionName)
                continue;

            GameSessionData.SetSelectedMission(mission);

            if (MissionManager.instance != null)
                MissionManager.instance.SetCurrentMission(mission);

            UpdateMissionDesicription(mission.missionDescription);
            return;
        }
    }

    public void PreviewMissionByName(string missionName)
    {
        UI_MissionSelectionButton[] missionButtons = GetComponentsInChildren<UI_MissionSelectionButton>(true);

        foreach (UI_MissionSelectionButton missionButton in missionButtons)
        {
            Mission mission = missionButton.Mission;
            bool isPreviewed = mission != null && mission.missionName == missionName;
            missionButton.SetRemotePreview(isPreviewed);

            if (isPreviewed)
            {
                previewedMissionButton = missionButton;
                UpdateMissionDesicription($"HOST VIEWING: {mission.missionName}\n{mission.missionDescription}");
            }
        }
    }

    public void ClearRemotePreview()
    {
        if (previewedMissionButton != null)
            previewedMissionButton.SetRemotePreview(false);

        previewedMissionButton = null;
        ShowDefaultDescription();
    }

    public void ShowDefaultDescription()
    {
        UpdateMissionDesicription(defaultDescription);
    }

    private void CacheBackButton()
    {
        if (backButton != null)
            return;

        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name != "Button - Back")
                continue;

            backButton = button;
            return;
        }
    }

    private void CacheRootCanvasGroup()
    {
        if (rootCanvasGroup != null)
            return;

        rootCanvasGroup = GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null)
            rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetRootInteractionEnabled(bool enabled)
    {
        CacheRootCanvasGroup();
        rootCanvasGroup.interactable = enabled;
        rootCanvasGroup.blocksRaycasts = enabled;
        rootCanvasGroup.alpha = 1f;
    }

    private void SetButtonRaycastEnabled(Button button, bool enabled)
    {
        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
        canvasGroup.alpha = 1f;
    }

    private void ReturnToMainMenu()
    {
        GameObject mainMenu = FindInactiveChildByName("MainMenu_UI");
        if (UI.instance != null && mainMenu != null)
            UI.instance.SwitchTo(mainMenu);
    }

    private GameObject FindInactiveChildByName(string targetName)
    {
        Transform root = transform.root;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == targetName)
                return child.gameObject;
        }

        return null;
    }
}
