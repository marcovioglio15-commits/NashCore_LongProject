using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PlayerManagementWindow : EditorWindow
{
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

    #region Fields
    private PlayerMasterPresetsPanel m_MasterPresetsPanel;
    private PlayerControllerPresetsPanel m_PresetsPanel;
    private VisualElement m_ContentRoot;
    private VisualElement m_PlaceholderPanel;
    private PanelType m_ActivePanel = PanelType.PlayerMasterPresets;
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

        m_ContentRoot = new VisualElement();
        m_ContentRoot.style.flexGrow = 1f;
        rootVisualElement.Add(m_ContentRoot);

        BuildPanels();
        ShowPanel(m_ActivePanel);
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
        m_MasterPresetsPanel = new PlayerMasterPresetsPanel();
        m_PresetsPanel = new PlayerControllerPresetsPanel();

        m_PlaceholderPanel = new VisualElement();
        m_PlaceholderPanel.style.flexGrow = 1f;
        m_PlaceholderPanel.style.justifyContent = Justify.Center;
        m_PlaceholderPanel.style.alignItems = Align.Center;
        Label placeholderLabel = new Label("Section not implemented yet.");
        placeholderLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        m_PlaceholderPanel.Add(placeholderLabel);
    }

    private void ShowPanel(PanelType panelType)
    {
        m_ActivePanel = panelType;
        m_ContentRoot.Clear();

        if (panelType == PanelType.PlayerMasterPresets)
        {
            m_ContentRoot.Add(m_MasterPresetsPanel.Root);
            return;
        }

        if (panelType == PanelType.PlayerControllerPresets)
        {
            m_ContentRoot.Add(m_PresetsPanel.Root);
            return;
        }

        m_ContentRoot.Add(m_PlaceholderPanel);
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
}
