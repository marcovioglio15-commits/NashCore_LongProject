using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyModulesPatternDefinition.
/// /params None.
/// /returns None.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyModulesPatternDefinition))]
public sealed class EnemyModulesPatternDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the shared pattern-definition UI with category-specific assembly slots.
    /// /params property Serialized shared pattern-definition property.
    /// /returns The built root visual element.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.marginBottom = 6f;
        SerializedProperty patternIdProperty = property.FindPropertyRelative("patternId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty descriptionProperty = property.FindPropertyRelative("description");
        SerializedProperty unreplaceableProperty = property.FindPropertyRelative("unreplaceable");
        SerializedProperty coreMovementProperty = property.FindPropertyRelative("coreMovement");
        SerializedProperty shortRangeInteractionProperty = property.FindPropertyRelative("shortRangeInteraction");
        SerializedProperty weaponInteractionProperty = property.FindPropertyRelative("weaponInteraction");
        SerializedProperty dropItemsProperty = property.FindPropertyRelative("dropItems");

        if (patternIdProperty == null ||
            displayNameProperty == null ||
            descriptionProperty == null ||
            unreplaceableProperty == null ||
            coreMovementProperty == null ||
            shortRangeInteractionProperty == null ||
            weaponInteractionProperty == null ||
            dropItemsProperty == null)
        {
            Label errorLabel = new Label("EnemyModulesPatternDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        AddDelayedTextField(root, patternIdProperty, "Pattern ID", "Stable identifier used by Enemy Advanced Pattern Presets loadouts.", false);
        AddDelayedTextField(root, displayNameProperty, "Display Name", "Human-readable label shown in shared pattern selection.", false);
        AddDelayedTextField(root, descriptionProperty, "Description", "Optional editor-facing notes for this shared pattern.", true);
        EnemyAdvancedPatternDrawerUtility.AddField(root, unreplaceableProperty, "Unreplaceable");
        root.Add(CreateAssemblyFoldout(coreMovementProperty,
                                       "Core Movement",
                                       "Always-active base movement used while the player stays outside the short-range interaction band.",
                                       true));
        root.Add(CreateAssemblyFoldout(shortRangeInteractionProperty,
                                       "Short-Range Interaction",
                                       "Optional behavior override activated only while the player stays inside the configured short-range band.",
                                       true));
        root.Add(CreateAssemblyFoldout(weaponInteractionProperty,
                                       "Weapon Interaction",
                                       "Optional weapon behavior gated by the shared minimum and maximum range settings defined here.",
                                       true));
        root.Add(CreateAssemblyFoldout(dropItemsProperty,
                                       "Drop Items",
                                       "Optional drop-items interaction preserved for loot-oriented patterns.",
                                       false));
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one delayed bound text field so serialized changes are committed only after edit confirmation.
    /// /params parent Parent visual element that receives the field.
    /// /params property Serialized string property bound to the field.
    /// /params label UI label shown above the field.
    /// /params tooltip Tooltip shown on the field.
    /// /params multiline True when the field should accept multiline input.
    /// /returns None.
    /// </summary>
    private static void AddDelayedTextField(VisualElement parent,
                                            SerializedProperty property,
                                            string label,
                                            string tooltip,
                                            bool multiline)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        TextField textField = new TextField(label);
        textField.isDelayed = true;
        textField.multiline = multiline;
        textField.tooltip = tooltip;

        if (multiline)
            textField.style.height = 60f;

        textField.BindProperty(property);
        parent.Add(textField);
    }

    /// <summary>
    /// Creates one foldout that hosts a full assembly property block with a short explanatory helper.
    /// /params property Serialized assembly property to draw inside the foldout.
    /// /params title Foldout title shown to the user.
    /// /params helperText Explanatory helper text shown above the assembly content.
    /// /params isExpanded Initial foldout open state.
    /// /returns The created foldout, or an empty element when the property is missing.
    /// </summary>
    private static VisualElement CreateAssemblyFoldout(SerializedProperty property,
                                                       string title,
                                                       string helperText,
                                                       bool isExpanded)
    {
        if (property == null)
            return new VisualElement();

        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(property,
                                                                                  title,
                                                                                  title.Replace(" ", string.Empty) + "Assembly",
                                                                                  isExpanded);
        foldout.tooltip = helperText;
        foldout.style.marginTop = 4f;

        PropertyField propertyField = new PropertyField(property, string.Empty);
        propertyField.BindProperty(property);
        propertyField.tooltip = helperText;
        propertyField.style.marginTop = 2f;
        foldout.Add(propertyField);
        return foldout;
    }
    #endregion

    #endregion
}
