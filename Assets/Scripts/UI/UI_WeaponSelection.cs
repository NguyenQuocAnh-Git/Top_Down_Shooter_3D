using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_WeaponSelection : MonoBehaviour
{
    [SerializeField] private GameObject nextUIToSwitchOn;
    public UI_SelectedWeaponWindow[] selectedWeapon;

    [Header("Warning Info")]
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private float disaperaingSpeed = .25f;
    private float currentWarningAlpha;
    private float targetWarningAlpha;
    private bool localSelectionLocked;
    private Button backButton;


    private void Awake()
    {
        CacheBackButton();
    }

    private void Start()
    {
        EnsureSelectedWeaponWindows();
    }

    private void Update()
    {
        if (warningText == null)
            return;

        if (currentWarningAlpha > targetWarningAlpha)
        {
            currentWarningAlpha -= Time.deltaTime * disaperaingSpeed;
            warningText.color = new Color(1, 1, 1, currentWarningAlpha);
        }
    }

    public void ConfirmWeaponSelection()
    {
        if (AtLeastOneWeaponSelected())
        {
            GameSessionData.SetSelectedWeapons(SelectedWeaponData());

            if (CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.IsInRoom)
            {
                if (localSelectionLocked)
                {
                    ShowWarningMessage("Weapons already locked. Waiting for other players.");
                    return;
                }

                if (CoopNetworkManager.Instance.CanLocalPlayerSelectWeapons == false)
                {
                    ShowWarningMessage("Weapon selection is not available right now.");
                    return;
                }

                localSelectionLocked = true;
                SetWeaponInputEnabled(false);
                CoopNetworkManager.Instance.NotifyLocalWeaponSelectionReady();
                ShowWarningMessage("Weapons locked. Waiting for other players.");
                return;
            }

            UI.instance.SwitchTo(nextUIToSwitchOn);
            UI.instance.StartLevelGeneration();
        }
        else
            ShowWarningMessage("Select at least one weapon.");
    }

    public void ConfigureForCoop(bool isHost, UnityEngine.Events.UnityAction hostBackAction)
    {
        localSelectionLocked = CoopNetworkManager.Instance != null
            && CoopNetworkManager.Instance.IsLocalPlayerWeaponReady();

        if (localSelectionLocked == false)
            ClearSelectedWeapons();

        SetWeaponInputEnabled(localSelectionLocked == false);
        ConfigureCoopBackButton(isHost, hostBackAction);

        if (localSelectionLocked)
            ShowWarningMessage("Weapons locked. Waiting for other players.");
        else if (isHost)
            ShowWarningMessage("Choose your weapons, then confirm. Back returns to the COOP lobby.");
        else
            ShowWarningMessage("Choose your weapons, then confirm.");
    }

    public void ConfigureForSinglePlayer()
    {
        localSelectionLocked = false;
        ClearSelectedWeapons();
        SetWeaponInputEnabled(true);
        ConfigureBackButton(true, ReturnToMainMenu);
    }

    private bool AtLeastOneWeaponSelected() => SelectedWeaponData().Count > 0;

    public List<Weapon_Data> SelectedWeaponData()
    {
        EnsureSelectedWeaponWindows();

        List<Weapon_Data> selectedData = new List<Weapon_Data> ();

        foreach(UI_SelectedWeaponWindow weapon in selectedWeapon)
        {
            if(weapon.weaponData != null)
                selectedData.Add(weapon.weaponData);
        }

        return selectedData;
    }

    public UI_SelectedWeaponWindow FindEmptySlot()
    {
        EnsureSelectedWeaponWindows();

        for (int i = 0; i < selectedWeapon.Length; i++)
        {
            if (selectedWeapon[i].IsEmpty())
                return selectedWeapon[i];
        }

        return null;
    }
    public UI_SelectedWeaponWindow FindSlowWithWeaponOfType(Weapon_Data weaponData)
    {
        EnsureSelectedWeaponWindows();

        for(int i = 0;i < selectedWeapon.Length;i++)
        {
            if (selectedWeapon[i].weaponData == weaponData)
                return selectedWeapon[i];
        }

        return null;
    }

    public void ShowWarningMessage(string message)
    {
        if (warningText == null)
            return;

        warningText.color = Color.white;
        warningText.text = message;

        currentWarningAlpha = warningText.color.a;
        targetWarningAlpha = 0;
    }

    private void SetWeaponInputEnabled(bool enabled)
    {
        UI_WeaponSelectionButton[] weaponButtons = GetComponentsInChildren<UI_WeaponSelectionButton>(true);

        foreach (UI_WeaponSelectionButton weaponButton in weaponButtons)
            weaponButton.SetLocalInteractionEnabled(enabled);
    }

    private void EnsureSelectedWeaponWindows()
    {
        if (selectedWeapon != null && selectedWeapon.Length > 0)
            return;

        selectedWeapon = GetComponentsInChildren<UI_SelectedWeaponWindow>(true);
    }

    private void ClearSelectedWeapons()
    {
        EnsureSelectedWeaponWindows();

        foreach (UI_SelectedWeaponWindow weaponWindow in selectedWeapon)
            weaponWindow.SetWeaponSlot(null);
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

    private void ConfigureCoopBackButton(bool isHost, UnityEngine.Events.UnityAction hostBackAction)
    {
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

    private void ConfigureBackButton(bool enabled, UnityEngine.Events.UnityAction backAction)
    {
        CacheBackButton();

        if (backButton == null)
            return;

        backButton.gameObject.SetActive(true);
        backButton.interactable = enabled;
        SetButtonRaycastEnabled(backButton, enabled);
        backButton.onClick = new Button.ButtonClickedEvent();

        if (enabled && backAction != null)
            backButton.onClick.AddListener(backAction);
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
