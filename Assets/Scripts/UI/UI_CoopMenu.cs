using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_CoopMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private GameObject hostLobbyPanel;
    [SerializeField] private GameObject joinRoomPanel;

    [Header("Host Lobby")]
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private Image readyButtonImage;

    [Header("Join Room")]
    [SerializeField] private TextMeshProUGUI roomStatusText;

    private bool localPlayerReady;

    private void Awake()
    {
        AssignButtonActions();
    }

    public static UI_CoopMenu EnsureCreated(UI ui)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null || ui == null)
            return null;

        Transform coopRoot = canvas.transform.Find("Coop_UI");
        if (coopRoot == null)
            return null;

        ui.RegisterRuntimeUIElement(coopRoot.gameObject);
        return coopRoot.GetComponent<UI_CoopMenu>();
    }

    private void OnEnable()
    {
        ShowModeSelection();
    }

    public void ShowRoot()
    {
        if (UI.instance != null)
            UI.instance.SwitchTo(gameObject);
    }

    public void ShowMainMenu()
    {
        if (UI.instance != null && mainMenuPanel != null)
            UI.instance.SwitchTo(mainMenuPanel);
    }

    public void ShowModeSelection()
    {
        SetActivePanel(modeSelectionPanel);
        localPlayerReady = false;
        RefreshReadyVisual();
    }

    public void ShowHostLobby()
    {
        SetActivePanel(hostLobbyPanel);
        localPlayerReady = false;
        RefreshReadyVisual();
    }

    public void ShowJoinRoom()
    {
        SetActivePanel(joinRoomPanel);
        UpdateRoomStatus("No LAN rooms found yet.");
    }

    public void ToggleReady()
    {
        localPlayerReady = !localPlayerReady;
        RefreshReadyVisual();
    }

    public void RefreshRooms()
    {
        UpdateRoomStatus("Room search will be connected in the Photon step.");
    }

    private void SetActivePanel(GameObject activePanel)
    {
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(modeSelectionPanel == activePanel);

        if (hostLobbyPanel != null)
            hostLobbyPanel.SetActive(hostLobbyPanel == activePanel);

        if (joinRoomPanel != null)
            joinRoomPanel.SetActive(joinRoomPanel == activePanel);
    }

    private void RefreshReadyVisual()
    {
        if (readyButtonText != null)
            readyButtonText.text = localPlayerReady ? "READY" : "NOT READY";

        if (readyButtonImage != null)
            readyButtonImage.color = localPlayerReady ? Color.green : Color.white;
    }

    private void UpdateRoomStatus(string message)
    {
        if (roomStatusText != null)
            roomStatusText.text = message;
    }

    private void AssignButtonActions()
    {
        AssignButton("Button - Host", ShowHostLobby);
        AssignButton("Button - Join", ShowJoinRoom);
        AssignButton("Button - Ready", ToggleReady);
        AssignButton("Button - Refresh", RefreshRooms);
        AssignBackButtonActions();
    }

    private void AssignButton(string buttonName, UnityEngine.Events.UnityAction action)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name != buttonName)
                continue;

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }
    }

    private void AssignBackButtonActions()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name != "Button - Back")
                continue;

            button.onClick = new Button.ButtonClickedEvent();

            if (modeSelectionPanel != null && button.transform.IsChildOf(modeSelectionPanel.transform))
                button.onClick.AddListener(ShowMainMenu);
            else
                button.onClick.AddListener(ShowModeSelection);
        }
    }
}
