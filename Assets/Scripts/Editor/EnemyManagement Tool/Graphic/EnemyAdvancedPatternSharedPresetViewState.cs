using System.Collections.Generic;

/// <summary>
/// Stores transient filter text used by the shared Modules and Patterns preset authoring views.
/// /params None.
/// /returns None.
/// </summary>
internal sealed class EnemyAdvancedPatternSharedPresetViewState
{
    #region Fields
    private readonly Dictionary<EnemyPatternModuleCatalogSection, string> moduleIdFilterTextBySection = new Dictionary<EnemyPatternModuleCatalogSection, string>();
    private readonly Dictionary<EnemyPatternModuleCatalogSection, string> moduleDisplayNameFilterTextBySection = new Dictionary<EnemyPatternModuleCatalogSection, string>();

    private string patternIdFilterText = string.Empty;
    private string patternDisplayNameFilterText = string.Empty;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns the current module-ID filter text for one catalog section.
    /// /params section Catalog section whose filter text is requested.
    /// /returns Current module-ID filter text, or an empty string when unset.
    /// </summary>
    public string GetModuleIdFilterText(EnemyPatternModuleCatalogSection section)
    {
        if (moduleIdFilterTextBySection.TryGetValue(section, out string filterText))
            return filterText;

        return string.Empty;
    }

    /// <summary>
    /// Returns the current module display-name filter text for one catalog section.
    /// /params section Catalog section whose filter text is requested.
    /// /returns Current display-name filter text, or an empty string when unset.
    /// </summary>
    public string GetModuleDisplayNameFilterText(EnemyPatternModuleCatalogSection section)
    {
        if (moduleDisplayNameFilterTextBySection.TryGetValue(section, out string filterText))
            return filterText;

        return string.Empty;
    }

    /// <summary>
    /// Stores the module-ID filter text for one catalog section.
    /// /params section Catalog section whose filter text is being updated.
    /// /params filterText New filter text.
    /// /returns None.
    /// </summary>
    public void SetModuleIdFilterText(EnemyPatternModuleCatalogSection section, string filterText)
    {
        moduleIdFilterTextBySection[section] = filterText ?? string.Empty;
    }

    /// <summary>
    /// Stores the module display-name filter text for one catalog section.
    /// /params section Catalog section whose filter text is being updated.
    /// /params filterText New filter text.
    /// /returns None.
    /// </summary>
    public void SetModuleDisplayNameFilterText(EnemyPatternModuleCatalogSection section, string filterText)
    {
        moduleDisplayNameFilterTextBySection[section] = filterText ?? string.Empty;
    }

    /// <summary>
    /// Clears both filter texts stored for one catalog section.
    /// /params section Catalog section whose filter texts should be cleared.
    /// /returns None.
    /// </summary>
    public void ClearModuleFilters(EnemyPatternModuleCatalogSection section)
    {
        moduleIdFilterTextBySection[section] = string.Empty;
        moduleDisplayNameFilterTextBySection[section] = string.Empty;
    }

    /// <summary>
    /// Returns the current pattern-ID filter text.
    /// /params None.
    /// /returns Current pattern-ID filter text.
    /// </summary>
    public string GetPatternIdFilterText()
    {
        return patternIdFilterText;
    }

    /// <summary>
    /// Returns the current pattern display-name filter text.
    /// /params None.
    /// /returns Current pattern display-name filter text.
    /// </summary>
    public string GetPatternDisplayNameFilterText()
    {
        return patternDisplayNameFilterText;
    }

    /// <summary>
    /// Stores the current pattern-ID filter text.
    /// /params filterText New pattern-ID filter text.
    /// /returns None.
    /// </summary>
    public void SetPatternIdFilterText(string filterText)
    {
        patternIdFilterText = filterText ?? string.Empty;
    }

    /// <summary>
    /// Stores the current pattern display-name filter text.
    /// /params filterText New pattern display-name filter text.
    /// /returns None.
    /// </summary>
    public void SetPatternDisplayNameFilterText(string filterText)
    {
        patternDisplayNameFilterText = filterText ?? string.Empty;
    }

    /// <summary>
    /// Clears both pattern filter texts.
    /// /params None.
    /// /returns None.
    /// </summary>
    public void ClearPatternFilters()
    {
        patternIdFilterText = string.Empty;
        patternDisplayNameFilterText = string.Empty;
    }
    #endregion

    #endregion
}
