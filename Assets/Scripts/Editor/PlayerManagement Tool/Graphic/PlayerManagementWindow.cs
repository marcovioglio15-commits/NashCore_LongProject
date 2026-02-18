using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PlayerManagementWindow : EditorWindow
{
    #region Fields
    private PlayerMasterPresetsPanel masterPresetsPanel;
    private PlayerControllerPresetsPanel controllerPresetsPanel;
    private VisualElement contentRoot;
    private VisualElement placeholderPanel;
    private Label sessionStatusLabel;
    private PanelType activePanel = PanelType.PlayerMasterPresets;
    private IVisualElementScheduledItem pendingCheckSchedule;
    #endregion

    #region Menu
    [MenuItem("Tools/Player Management Tool")]
    public static void ShowWindow()
    {
        PlayerManagementWindow window = GetWindow<PlayerManagementWindow>();
        window.titleContent = new GUIContent("Player Management Tool");
        window.minSize = new Vector2(980f, 640f);
        window.Focus();
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnEnable()
    {
        saveChangesMessage = "There are unapplied changes in Player Management Tool. Apply before closing?";

        if (PlayerManagementDraftSession.IsInitialized == false)
            PlayerManagementDraftSession.BeginSession();

        UpdateUnsavedState();
    }

    private void CreateGUI()
    {
        BuildWindowLayout();
    }

    private void OnDisable()
    {
        if (pendingCheckSchedule != null)
            pendingCheckSchedule.Pause();
    }

    private void OnDestroy()
    {
        if (hasUnsavedChanges == false)
            PlayerManagementDraftSession.EndSession();
    }

    public override void SaveChanges()
    {
        ApplyChanges();
    }

    public override void DiscardChanges()
    {
        DiscardChangesAndRebuild();
    }
    #endregion

    #region Layout
    private void BuildWindowLayout()
    {
        rootVisualElement.Clear();

        VisualElement toolbar = BuildToolbar();
        rootVisualElement.Add(toolbar);

        contentRoot = new VisualElement();
        contentRoot.style.flexGrow = 1f;
        rootVisualElement.Add(contentRoot);

        BuildPanels();
        ShowPanel(activePanel);
        RefreshSessionStatus();

        if (pendingCheckSchedule != null)
            pendingCheckSchedule.Pause();

        pendingCheckSchedule = rootVisualElement.schedule.Execute(RefreshSessionStatus).Every(1000);
    }

    private VisualElement BuildToolbar()
    {
        Toolbar toolbar = new Toolbar();

        ToolbarToggle masterToggle = CreatePanelToggle("Player Master Presets", PanelType.PlayerMasterPresets, true);
        toolbar.Add(masterToggle);

        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        toolbar.Add(spacer);

        Button undoButton = new Button(UndoLastChange);
        undoButton.text = "Undo";
        undoButton.tooltip = "Undo latest change in this tool session.";
        toolbar.Add(undoButton);

        Button redoButton = new Button(RedoLastChange);
        redoButton.text = "Redo";
        redoButton.tooltip = "Redo latest undone change in this tool session.";
        toolbar.Add(redoButton);

        Button applyButton = new Button(ApplyChanges);
        applyButton.text = "Apply";
        applyButton.tooltip = "Persist all pending changes to assets.";
        toolbar.Add(applyButton);

        Button discardButton = new Button(DiscardChangesAndRebuild);
        discardButton.text = "Discard";
        discardButton.tooltip = "Discard unapplied changes and restore baseline assets.";
        toolbar.Add(discardButton);

        sessionStatusLabel = new Label();
        sessionStatusLabel.style.marginLeft = 8f;
        sessionStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolbar.Add(sessionStatusLabel);

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
        controllerPresetsPanel = new PlayerControllerPresetsPanel();

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

        if (contentRoot == null)
            return;

        contentRoot.Clear();

        if (panelType == PanelType.PlayerMasterPresets)
        {
            contentRoot.Add(masterPresetsPanel.Root);
            return;
        }

        if (panelType == PanelType.PlayerControllerPresets)
        {
            contentRoot.Add(controllerPresetsPanel.Root);
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

        if (panelType == PanelType.PowerUps)
            return "Power-Ups";

        return "Animation Bindings";
    }
    #endregion

    #region Session Actions
    private void UndoLastChange()
    {
        PlayerManagementDraftSession.PerformUndo();
        RefreshPanelsAfterSessionChange();
    }

    private void RedoLastChange()
    {
        PlayerManagementDraftSession.PerformRedo();
        RefreshPanelsAfterSessionChange();
    }

    private void ApplyChanges()
    {
        PlayerManagementDraftSession.Apply();
        RefreshPanelsAfterSessionChange();
    }

    private void DiscardChangesAndRebuild()
    {
        PlayerManagementDraftSession.Discard();
        RefreshPanelsAfterSessionChange();
    }

    private void RefreshSessionStatus()
    {
        PlayerManagementDraftSession.RecomputePendingChanges();
        UpdateUnsavedState();

        if (sessionStatusLabel == null)
            return;

        if (hasUnsavedChanges)
            sessionStatusLabel.text = "Pending Changes";
        else
            sessionStatusLabel.text = "Clean";
    }

    private void RefreshPanelsAfterSessionChange()
    {
        if (masterPresetsPanel != null)
            masterPresetsPanel.RefreshFromSessionChange();

        if (controllerPresetsPanel != null)
            controllerPresetsPanel.RefreshFromSessionChange();

        RefreshSessionStatus();
    }

    private void UpdateUnsavedState()
    {
        hasUnsavedChanges = PlayerManagementDraftSession.HasPendingChanges;
    }
    #endregion

    #endregion

    #region Nested Types
    public enum PanelType
    {
        PlayerMasterPresets = 0,
        PlayerControllerPresets = 1,
        LevelUpProgression = 2,
        PowerUps = 3,
        AnimationBindings = 4
    }
    #endregion
}
