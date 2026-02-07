using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PlayerManagementWindow : EditorWindow
{
    #region Fields
    private PlayerMasterPresetsPanel masterPresetsPanel;
    private PlayerControllerPresetsPanel presetsPanel;
    private VisualElement contentRoot;
    private VisualElement placeholderPanel;
    private PanelType activePanel = PanelType.PlayerMasterPresets;
    #endregion

    #region Menu
    [MenuItem("Tools/Player Management Tool")]
    public static void ShowWindow()
    {
        PlayerManagementWindow window = GetWindow<PlayerManagementWindow>();
        window.titleContent = new GUIContent("Player Management Tool");
        window.minSize = new Vector2(980f, 640f);
    }
    #endregion

    #region Unity Methods
    private void CreateGUI()
    {
        rootVisualElement.Clear();

        VisualElement toolbar = BuildToolbar();
        rootVisualElement.Add(toolbar);

        contentRoot = new VisualElement();
        contentRoot.style.flexGrow = 1f;
        rootVisualElement.Add(contentRoot);

        BuildPanels();
        ShowPanel(activePanel);
    }

    private void OnDisable()
    {
        SaveOpenScenes();
    }
    #endregion

    #region Scene Save
    private void SaveOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Panel Management
    private VisualElement BuildToolbar()
    {
        Toolbar toolbar = new Toolbar();

        ToolbarToggle masterToggle = CreatePanelToggle("Player Master Presets", PanelType.PlayerMasterPresets, true);
        toolbar.Add(masterToggle);

        return toolbar;
    }

    private ToolbarToggle CreatePanelToggle(string label, PanelType panelType, bool isDefault)
    {
        ToolbarToggle toggle = new ToolbarToggle();
        toggle.text = label;
        toggle.value = isDefault;
        toggle.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue == false)
                return;

            ShowPanel(panelType);
            UpdateToolbarSelection(panelType);
        });

        return toggle;
    }

    private void UpdateToolbarSelection(PanelType panelType)
    {
        Toolbar toolbar = rootVisualElement.Q<Toolbar>();

        if (toolbar == null)
            return;

        foreach (VisualElement child in toolbar.Children())
        {
            ToolbarToggle toggle = child as ToolbarToggle;

            if (toggle == null)
                continue;

            bool shouldEnable = toggle.text == GetPanelLabel(panelType);

            if (toggle.value != shouldEnable)
                toggle.SetValueWithoutNotify(shouldEnable);
        }
    }

    private void BuildPanels()
    {
        masterPresetsPanel = new PlayerMasterPresetsPanel();
        presetsPanel = new PlayerControllerPresetsPanel();

        placeholderPanel = new VisualElement();
        placeholderPanel.style.flexGrow = 1f;
        placeholderPanel.style.justifyContent = Justify.Center;
        placeholderPanel.style.alignItems = Align.Center;
        Label placeholderLabel = new Label("Section not implemented yet.");
        placeholderLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        placeholderPanel.Add(placeholderLabel);
    }

    private void ShowPanel(PanelType panelType)
    {
        activePanel = panelType;
        contentRoot.Clear();

        if (panelType == PanelType.PlayerMasterPresets)
        {
            contentRoot.Add(masterPresetsPanel.Root);
            return;
        }

        if (panelType == PanelType.PlayerControllerPresets)
        {
            contentRoot.Add(presetsPanel.Root);
            return;
        }

        contentRoot.Add(placeholderPanel);
    }

    private string GetPanelLabel(PanelType panelType)
    {
        if (panelType == PanelType.PlayerMasterPresets)
            return "Player Master Presets";

        if (panelType == PanelType.PlayerControllerPresets)
            return "Player Controller Presets";

        if (panelType == PanelType.LevelUpProgression)
            return "Level-Up & Progression";

        if (panelType == PanelType.CraftablePowerUps)
            return "Craftable Power-Ups";

        return "Animations Bindings";
    }

    #endregion
    
    #region Nested Types
    public enum PanelType
    {
        PlayerMasterPresets = 0,
        PlayerControllerPresets = 1,
        LevelUpProgression = 2,
        CraftablePowerUps = 3,
        AnimationBindings = 4
    }
    #endregion
}
