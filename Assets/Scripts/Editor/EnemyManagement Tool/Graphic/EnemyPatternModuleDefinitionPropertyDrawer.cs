using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternModuleDefinition.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternModuleDefinition))]
public sealed class EnemyPatternModuleDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the module definition editor UI with smart payload visibility.
    /// </summary>
    /// <param name="property">Serialized module definition property.</param>
    /// <returns>Returns the built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty moduleKindProperty = property.FindPropertyRelative("moduleKind");
        SerializedProperty notesProperty = property.FindPropertyRelative("notes");
        SerializedProperty payloadDataProperty = property.FindPropertyRelative("data");

        if (moduleIdProperty == null ||
            displayNameProperty == null ||
            moduleKindProperty == null ||
            notesProperty == null ||
            payloadDataProperty == null)
        {
            Label errorLabel = new Label("EnemyPatternModuleDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, moduleIdProperty, "Module ID");
        EnemyAdvancedPatternDrawerUtility.AddField(root, displayNameProperty, "Display Name");
        EnemyAdvancedPatternDrawerUtility.AddField(root, moduleKindProperty, "Module Kind");
        EnemyAdvancedPatternDrawerUtility.AddField(root, notesProperty, "Notes");

        HelpBox moduleInfoBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        moduleInfoBox.style.marginTop = 2f;
        moduleInfoBox.style.marginLeft = 126f;
        root.Add(moduleInfoBox);

        Label payloadHeader = new Label("Module Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        payloadHeader.style.marginLeft = 126f;
        root.Add(payloadHeader);

        VisualElement payloadContainer = new VisualElement();
        payloadContainer.style.marginLeft = 126f;
        root.Add(payloadContainer);

        RefreshModuleUi(moduleKindProperty, payloadDataProperty, moduleInfoBox, payloadContainer);
        root.TrackPropertyValue(moduleKindProperty, changedProperty =>
        {
            RefreshModuleUi(changedProperty, payloadDataProperty, moduleInfoBox, payloadContainer);
        });

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Refreshes module info and payload editor according to selected module kind.
    /// </summary>
    /// <param name="moduleKindProperty">Module kind enum property.</param>
    /// <param name="payloadDataProperty">Payload root property.</param>
    /// <param name="moduleInfoBox">Info box UI element.</param>
    /// <param name="payloadContainer">Payload host container.</param>
    private static void RefreshModuleUi(SerializedProperty moduleKindProperty,
                                        SerializedProperty payloadDataProperty,
                                        HelpBox moduleInfoBox,
                                        VisualElement payloadContainer)
    {
        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternDrawerUtility.ResolveModuleKind(moduleKindProperty);
        moduleInfoBox.text = ResolveModuleKindDescription(moduleKind);
        moduleInfoBox.messageType = HelpBoxMessageType.Info;
        EnemyAdvancedPatternDrawerUtility.RefreshPayloadEditor(payloadDataProperty, moduleKind, payloadContainer);
    }

    /// <summary>
    /// Resolves a short description for the selected module kind.
    /// </summary>
    /// <param name="moduleKind">Module kind value.</param>
    /// <returns>Returns human-readable module kind description.</returns>
    private static string ResolveModuleKindDescription(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return "Stationary: disables movement and can optionally freeze rotation.";

            case EnemyPatternModuleKind.Grunt:
                return "Grunt: uses standard Brain-driven chase and steering.";

            case EnemyPatternModuleKind.Wanderer:
                return "Wanderer: autonomous roaming behavior with Basic or DVD mode.";

            case EnemyPatternModuleKind.Coward:
                return "Coward: retreats from the player while scoring open-space escape routes and respecting wall clearance.";

            case EnemyPatternModuleKind.Shooter:
                return "Shooter: emits periodic projectiles with burst and elemental options.";

            case EnemyPatternModuleKind.DropItems:
                return "DropItems: spawns configured drops on enemy death, including experience drops.";

            default:
                return "Unknown module kind.";
        }
    }
    #endregion

    #endregion
}
