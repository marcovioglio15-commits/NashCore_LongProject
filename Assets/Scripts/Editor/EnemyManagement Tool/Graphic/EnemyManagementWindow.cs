using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class EnemyManagementWindow : EditorWindow
{
    #region Fields
    private EnemyMasterPresetsPanel masterPresetsPanel;
    private VisualElement contentRoot;
    private Label sessionStatusLabel;
    private PanelType activePanel = PanelType.EnemyMasterPresets;
    private IVisualElementScheduledItem pendingCheckSchedule;
    #endregion

    #region Menu
    [MenuItem("Tools/Enemy Management Tool")]
    public static void ShowWindow()
    {
        EnemyManagementWindow window = GetWindow<EnemyManagementWindow>();
        window.titleContent = new GUIContent("Enemy Management Tool");
        window.minSize = new Vector2(980f, 640f);
        window.Focus();
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnEnable()
    {
        saveChangesMessage = "There are unapplied changes in Enemy Management Tool. Apply before closing?";

        if (EnemyManagementDraftSession.IsInitialized == false)
            EnemyManagementDraftSession.BeginSession();

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
            EnemyManagementDraftSession.EndSession();
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

        ToolbarToggle masterToggle = CreatePanelToggle("Enemy Master Presets", PanelType.EnemyMasterPresets, true);
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
        masterPresetsPanel = new EnemyMasterPresetsPanel();
    }

    private void ShowPanel(PanelType panelType)
    {
        activePanel = panelType;

        if (contentRoot == null)
            return;

        contentRoot.Clear();

        if (panelType == PanelType.EnemyMasterPresets)
        {
            contentRoot.Add(masterPresetsPanel.Root);
            return;
        }
    }

    private string GetPanelLabel(PanelType panelType)
    {
        if (panelType == PanelType.EnemyMasterPresets)
            return "Enemy Master Presets";

        if (panelType == PanelType.EnemyBrainPresets)
            return "Enemy Brain Presets";

        return "Enemy Master Presets";
    }
    #endregion

    #region Session Actions
    private void UndoLastChange()
    {
        EnemyManagementDraftSession.PerformUndo();
        RefreshPanelsAfterSessionChange();
    }

    private void RedoLastChange()
    {
        EnemyManagementDraftSession.PerformRedo();
        RefreshPanelsAfterSessionChange();
    }

    private void ApplyChanges()
    {
        EnemyManagementDraftSession.Apply();
        RefreshPanelsAfterSessionChange();
    }

    private void DiscardChangesAndRebuild()
    {
        EnemyManagementDraftSession.Discard();
        RefreshPanelsAfterSessionChange();
    }

    private void RefreshSessionStatus()
    {
        EnemyManagementDraftSession.RecomputePendingChanges();
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

        RefreshSessionStatus();
    }

    private void UpdateUnsavedState()
    {
        hasUnsavedChanges = EnemyManagementDraftSession.HasPendingChanges;
    }
    #endregion

    #endregion

    #region Nested Types
    public enum PanelType
    {
        EnemyMasterPresets = 0,
        EnemyBrainPresets = 1
    }
    #endregion
}
