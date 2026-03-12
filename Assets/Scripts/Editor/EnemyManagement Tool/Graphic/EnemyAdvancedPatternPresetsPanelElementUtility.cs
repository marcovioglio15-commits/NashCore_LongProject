using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides reusable UI helpers for enemy advanced pattern preset detail sections.
/// </summary>
internal static class EnemyAdvancedPatternPresetsPanelElementUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a standard details section container attached to the panel content root.
    /// </summary>
    /// <param name="panel">Owning panel that provides the content root.</param>
    /// <param name="sectionTitle">Header text for the section.</param>
    /// <returns>Returns the created section container, or null when the panel is not ready.</returns>
    public static VisualElement CreateDetailsSectionContainer(EnemyAdvancedPatternPresetsPanel panel, string sectionTitle)
    {
        if (panel == null)
            return null;

        VisualElement detailsSectionContentRoot = panel.DetailsSectionContentRoot;

        if (detailsSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);
        detailsSectionContentRoot.Add(container);
        return container;
    }

    /// <summary>
    /// Adds a property field that marks the draft session dirty and refreshes the preset list on edits.
    /// </summary>
    /// <param name="panel">Owning panel used for list refresh.</param>
    /// <param name="parent">Parent visual element that receives the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">Field label shown in the inspector UI.</param>
    /// <returns>Void.</returns>
    public static void AddTrackedPropertyField(EnemyAdvancedPatternPresetsPanel panel,
                                               VisualElement parent,
                                               SerializedProperty property,
                                               string label)
    {
        if (panel == null)
            return;

        if (parent == null)
            return;

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        parent.Add(field);
    }
    #endregion

    #endregion
}
