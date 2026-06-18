using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CoopMenuSceneSetup
{
    private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    private const string CoopRootName = "Coop_UI";
    private const string CoopButtonName = "Button - COOP";

    [InitializeOnLoadMethod]
    private static void SetupWhenMenuSceneIsOpen()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MenuScenePath)
                return;

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null || canvas.transform.Find(CoopRootName) != null)
                return;

            SetupLoadedScene(scene, false);
        };
    }

    [MenuItem("Tools/UI/Setup Coop Menu UI")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != MenuScenePath)
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        SetupLoadedScene(scene, true);
    }

    private static void SetupLoadedScene(Scene scene, bool saveScene)
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        UI ui = Object.FindObjectOfType<UI>();

        if (canvas == null || ui == null)
        {
            Debug.LogError("MenuScene must contain Canvas and UI components.");
            return;
        }

        Transform existingCoop = canvas.transform.Find(CoopRootName);
        if (existingCoop != null)
            Object.DestroyImmediate(existingCoop.gameObject);

        Transform mainMenu = canvas.transform.Find("MainMenu_UI");
        Transform mainButtonHolder = mainMenu != null ? mainMenu.Find("Button_Holder") : null;
        Button templateButton = FindButton("Button - Settings") ?? Object.FindObjectOfType<Button>();
        TextMeshProUGUI templateTitle = FindText("Main Menu");

        if (mainMenu == null || mainButtonHolder == null || templateButton == null || templateTitle == null)
        {
            Debug.LogError("Could not find required MainMenu UI templates.");
            return;
        }

        RemoveExistingCoopButton(mainButtonHolder);

        GameObject coopRoot = CreateRoot(canvas.transform, CoopRootName);
        UI_CoopMenu coopMenu = coopRoot.AddComponent<UI_CoopMenu>();

        GameObject modePanel = CreatePanel(coopRoot.transform, "CoopModeSelection_Panel");
        GameObject hostPanel = CreatePanel(coopRoot.transform, "CoopHostLobby_Panel");
        GameObject joinPanel = CreatePanel(coopRoot.transform, "CoopJoinRoom_Panel");

        BuildModeSelection(modePanel.transform, coopMenu, templateButton, templateTitle);
        BuildHostLobby(hostPanel.transform, coopMenu, templateButton, templateTitle);
        BuildJoinRoom(joinPanel.transform, coopMenu, templateButton, templateTitle);

        AssignCoopMenuReferences(coopMenu, mainMenu.gameObject, modePanel, hostPanel, joinPanel);
        AddMainMenuCoopButton(mainButtonHolder, templateButton, coopMenu);
        AddUiElement(ui, coopRoot);

        coopRoot.SetActive(false);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);

        if (saveScene)
            EditorSceneManager.SaveScene(scene);

        Debug.Log("Coop menu UI has been added to MenuScene.");
    }

    private static void BuildModeSelection(
        Transform parent,
        UI_CoopMenu coopMenu,
        Button templateButton,
        TextMeshProUGUI templateTitle)
    {
        CreateTitle(parent, templateTitle, "COOP", new Vector2(0, -95), new Vector2(700, 120));
        CreateInfoText(parent, templateTitle, "Choose how this player enters the squad.", new Vector2(0, -185), new Vector2(900, 70), 34);

        Transform buttons = CreateVerticalGroup(parent, "ModeButtons", new Vector2(0, -365), new Vector2(360, 330), new Vector2(360, 100), 28);

        Button hostButton = CreateButton(buttons, templateButton, "Button - Host", "HOST");
        SetButtonAction(hostButton, coopMenu.ShowHostLobby);

        Button joinButton = CreateButton(buttons, templateButton, "Button - Join", "JOIN");
        SetButtonAction(joinButton, coopMenu.ShowJoinRoom);

        Button backButton = CreateButton(buttons, templateButton, "Button - Back", "BACK");
        SetButtonAction(backButton, coopMenu.ShowMainMenu);
    }

    private static void BuildHostLobby(
        Transform parent,
        UI_CoopMenu coopMenu,
        Button templateButton,
        TextMeshProUGUI templateTitle)
    {
        CreateTitle(parent, templateTitle, "HOST LOBBY", new Vector2(0, -70), new Vector2(850, 100));
        CreateInfoText(parent, templateTitle, "Waiting for players. Photon room data connects here later.", new Vector2(0, -145), new Vector2(1100, 70), 31);

        Transform slots = CreateVerticalGroup(parent, "PlayerSlots", new Vector2(0, -395), new Vector2(820, 430), new Vector2(820, 82), 20);
        CreateSlot(slots, templateTitle, "PLAYER 1", "HOST", "READY");
        CreateSlot(slots, templateTitle, "PLAYER 2", "EMPTY", "WAITING");
        CreateSlot(slots, templateTitle, "PLAYER 3", "EMPTY", "WAITING");
        CreateSlot(slots, templateTitle, "PLAYER 4", "EMPTY", "WAITING");

        Button readyButton = CreateButton(parent, templateButton, "Button - Ready", "NOT READY");
        SetRect(readyButton.GetComponent<RectTransform>(), new Vector2(-200, 95), new Vector2(300, 100), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        SetButtonAction(readyButton, coopMenu.ToggleReady);

        Button backButton = CreateButton(parent, templateButton, "Button - Back", "BACK");
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(200, 95), new Vector2(300, 100), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        SetButtonAction(backButton, coopMenu.ShowModeSelection);
    }

    private static void BuildJoinRoom(
        Transform parent,
        UI_CoopMenu coopMenu,
        Button templateButton,
        TextMeshProUGUI templateTitle)
    {
        CreateTitle(parent, templateTitle, "JOIN ROOM", new Vector2(0, -70), new Vector2(850, 100));
        TextMeshProUGUI status = CreateInfoText(parent, templateTitle, "No LAN rooms found yet.", new Vector2(0, -165), new Vector2(1050, 70), 34);

        Transform rooms = CreateVerticalGroup(parent, "RoomList", new Vector2(0, -390), new Vector2(850, 360), new Vector2(850, 90), 18);
        CreateRoomSlot(rooms, templateTitle, "Room slot 1", "Waiting for host room data");
        CreateRoomSlot(rooms, templateTitle, "Room slot 2", "Waiting for host room data");
        CreateRoomSlot(rooms, templateTitle, "Room slot 3", "Waiting for host room data");

        Button refreshButton = CreateButton(parent, templateButton, "Button - Refresh", "REFRESH");
        SetRect(refreshButton.GetComponent<RectTransform>(), new Vector2(-200, 95), new Vector2(300, 100), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        SetButtonAction(refreshButton, coopMenu.RefreshRooms);

        Button backButton = CreateButton(parent, templateButton, "Button - Back", "BACK");
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(200, 95), new Vector2(300, 100), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        SetButtonAction(backButton, coopMenu.ShowModeSelection);

        AssignObjectReference(coopMenu, "roomStatusText", status);
    }

    private static GameObject CreateRoot(Transform canvas, string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        root.transform.SetParent(canvas, false);
        Stretch(root.GetComponent<RectTransform>());
        return root;
    }

    private static GameObject CreatePanel(Transform parent, string name)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.layer = LayerMask.NameToLayer("UI");
        panel.transform.SetParent(parent, false);
        Stretch(panel.GetComponent<RectTransform>());

        GameObject background = new GameObject("DarkBackgroud", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        background.layer = LayerMask.NameToLayer("UI");
        background.transform.SetParent(panel.transform, false);
        Stretch(background.GetComponent<RectTransform>());

        Image image = background.GetComponent<Image>();
        image.color = new Color(0.18867922f, 0.09172084f, 0.057553068f, 0.69803923f);
        image.raycastTarget = true;

        return panel;
    }

    private static Button AddMainMenuCoopButton(Transform holder, Button templateButton, UI_CoopMenu coopMenu)
    {
        Button coopButton = CreateButton(holder, templateButton, CoopButtonName, "COOP");
        SetButtonAction(coopButton, coopMenu.ShowRoot);
        return coopButton;
    }

    private static Button CreateButton(Transform parent, Button templateButton, string name, string label)
    {
        Button button = Object.Instantiate(templateButton, parent);
        button.name = name;
        button.gameObject.SetActive(true);

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = label;

        ClearButtonActions(button);
        return button;
    }

    private static TextMeshProUGUI CreateTitle(Transform parent, TextMeshProUGUI template, string label, Vector2 position, Vector2 size)
    {
        TextMeshProUGUI title = Object.Instantiate(template, parent);
        title.name = "Title";
        title.text = label;
        title.enableAutoSizing = true;
        title.fontSizeMax = 72;
        title.fontSizeMin = 28;
        title.alignment = TextAlignmentOptions.Center;
        SetRect(title.rectTransform, position, size, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        return title;
    }

    private static TextMeshProUGUI CreateInfoText(Transform parent, TextMeshProUGUI template, string label, Vector2 position, Vector2 size, float maxFontSize)
    {
        TextMeshProUGUI text = Object.Instantiate(template, parent);
        text.name = "InfoText";
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMax = maxFontSize;
        text.fontSizeMin = 16;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        SetRect(text.rectTransform, position, size, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        return text;
    }

    private static void CreateSlot(Transform parent, TextMeshProUGUI template, string left, string center, string right)
    {
        GameObject slot = CreateBox(parent, "Slot - " + left);
        CreateSlotText(slot.transform, template, left, TextAlignmentOptions.Left, new Vector2(28, 0), new Vector2(250, 60), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        CreateSlotText(slot.transform, template, center, TextAlignmentOptions.Center, Vector2.zero, new Vector2(280, 60), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        CreateSlotText(slot.transform, template, right, TextAlignmentOptions.Right, new Vector2(-28, 0), new Vector2(250, 60), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
    }

    private static void CreateRoomSlot(Transform parent, TextMeshProUGUI template, string title, string subtitle)
    {
        GameObject slot = CreateBox(parent, title);
        CreateSlotText(slot.transform, template, title, TextAlignmentOptions.Left, new Vector2(28, 14), new Vector2(500, 46), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        CreateSlotText(slot.transform, template, subtitle, TextAlignmentOptions.Right, new Vector2(-28, -14), new Vector2(500, 46), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
    }

    private static TextMeshProUGUI CreateSlotText(
        Transform parent,
        TextMeshProUGUI template,
        string label,
        TextAlignmentOptions alignment,
        Vector2 position,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot)
    {
        TextMeshProUGUI text = Object.Instantiate(template, parent);
        text.name = "Text - " + label;
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16;
        text.fontSizeMax = 34;
        text.fontStyle = FontStyles.Normal;
        text.alignment = alignment;
        SetRect(text.rectTransform, position, size, anchorMin, anchorMax, pivot);
        return text;
    }

    private static GameObject CreateBox(Transform parent, string name)
    {
        GameObject box = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.layer = LayerMask.NameToLayer("UI");
        box.transform.SetParent(parent, false);

        Image image = box.GetComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.08f, 0.55f);
        image.raycastTarget = true;

        return box;
    }

    private static Transform CreateVerticalGroup(Transform parent, string name, Vector2 position, Vector2 size, Vector2 cellSize, float spacing)
    {
        GameObject group = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
        group.layer = LayerMask.NameToLayer("UI");
        group.transform.SetParent(parent, false);
        SetRect(group.GetComponent<RectTransform>(), position, size, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));

        GridLayoutGroup grid = group.GetComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = new Vector2(0, spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.UpperCenter;

        return group.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetButtonAction(Button button, UnityAction action)
    {
        ClearButtonActions(button);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }

    private static void ClearButtonActions(Button button)
    {
        button.onClick.RemoveAllListeners();

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
    }

    private static Button FindButton(string name)
    {
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == name)
                return button;
        }

        return null;
    }

    private static TextMeshProUGUI FindText(string text)
    {
        TextMeshProUGUI[] texts = Object.FindObjectsOfType<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI tmp in texts)
        {
            if (tmp.text.Contains(text))
                return tmp;
        }

        return null;
    }

    private static void RemoveExistingCoopButton(Transform holder)
    {
        Transform existing = holder.Find(CoopButtonName);
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);
    }

    private static void AssignCoopMenuReferences(UI_CoopMenu coopMenu, GameObject mainMenu, GameObject modePanel, GameObject hostPanel, GameObject joinPanel)
    {
        AssignObjectReference(coopMenu, "mainMenuPanel", mainMenu);
        AssignObjectReference(coopMenu, "modeSelectionPanel", modePanel);
        AssignObjectReference(coopMenu, "hostLobbyPanel", hostPanel);
        AssignObjectReference(coopMenu, "joinRoomPanel", joinPanel);

        Button readyButton = hostPanel.transform.Find("Button - Ready")?.GetComponent<Button>();
        if (readyButton != null)
        {
            AssignObjectReference(coopMenu, "readyButtonText", readyButton.GetComponentInChildren<TextMeshProUGUI>(true));
            AssignObjectReference(coopMenu, "readyButtonImage", readyButton.image);
        }
    }

    private static void AssignObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AddUiElement(UI ui, GameObject uiElement)
    {
        SerializedObject serializedObject = new SerializedObject(ui);
        SerializedProperty uiElements = serializedObject.FindProperty("UIElements");

        if (uiElements == null || !uiElements.isArray)
            return;

        List<Object> elements = new List<Object>();
        for (int i = 0; i < uiElements.arraySize; i++)
        {
            Object element = uiElements.GetArrayElementAtIndex(i).objectReferenceValue;
            if (element != null && element != uiElement)
                elements.Add(element);
        }

        elements.Add(uiElement);
        uiElements.arraySize = elements.Count;

        for (int i = 0; i < elements.Count; i++)
            uiElements.GetArrayElementAtIndex(i).objectReferenceValue = elements[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
