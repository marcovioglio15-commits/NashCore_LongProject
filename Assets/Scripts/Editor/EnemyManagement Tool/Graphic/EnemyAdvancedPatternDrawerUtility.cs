using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides shared editor helpers for Enemy Advanced Pattern property drawers.
/// </summary>
public static class EnemyAdvancedPatternDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a bound property field to a parent container.
    /// </summary>
    /// <param name="parent">Parent visual element that receives the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">UI label text.</param>
    /// <returns>Returns true when the field is successfully added.</returns>
    public static bool AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return false;

        if (property == null)
            return false;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        parent.Add(field);
        return true;
    }

    /// <summary>
    /// Resolves module kind enum from serialized property with fallback.
    /// </summary>
    /// <param name="moduleKindProperty">Serialized enum property.</param>
    /// <returns>Returns a valid module kind value.</returns>
    public static EnemyPatternModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
    {
        if (moduleKindProperty == null)
            return EnemyPatternModuleKind.Grunt;

        if (moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyPatternModuleKind.Grunt;

        int enumValue = moduleKindProperty.enumValueIndex;

        if (!Enum.IsDefined(typeof(EnemyPatternModuleKind), enumValue))
            return EnemyPatternModuleKind.Grunt;

        return (EnemyPatternModuleKind)enumValue;
    }

    /// <summary>
    /// Builds and refreshes payload UI for a specific module kind.
    /// </summary>
    /// <param name="payloadDataProperty">Serialized payload data root.</param>
    /// <param name="moduleKind">Target module kind.</param>
    /// <param name="payloadContainer">Container to rebuild.</param>
    /// <returns>Returns true when payload UI is built.</returns>
    public static bool RefreshPayloadEditor(SerializedProperty payloadDataProperty,
                                            EnemyPatternModuleKind moduleKind,
                                            VisualElement payloadContainer)
    {
        if (payloadContainer == null)
            return false;

        payloadContainer.Clear();

        if (payloadDataProperty == null)
        {
            HelpBox missingPayloadBox = new HelpBox("Payload data property is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingPayloadBox);
            return false;
        }

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildStationaryPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Wanderer:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildWandererPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Shooter:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildShooterPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.DropItems:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildDropItemsPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Grunt:
                HelpBox noPayloadBox = new HelpBox("No payload is required for this module kind.", HelpBoxMessageType.Info);
                payloadContainer.Add(noPayloadBox);
                return true;

            default:
                HelpBox unsupportedBox = new HelpBox("Unsupported module kind.", HelpBoxMessageType.Warning);
                payloadContainer.Add(unsupportedBox);
                return false;
        }
    }

    /// <summary>
    /// Builds module ID options from the current preset module catalog.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing moduleDefinitions.</param>
    /// <returns>Returns distinct module IDs preserving list order.</returns>
    public static List<string> BuildModuleIdOptions(SerializedObject serializedObject)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        SerializedProperty moduleDefinitionsProperty = serializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
            return options;

        for (int index = 0; index < moduleDefinitionsProperty.arraySize; index++)
        {
            SerializedProperty moduleDefinitionProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(index);

            if (moduleDefinitionProperty == null)
                continue;

            SerializedProperty moduleIdProperty = moduleDefinitionProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            string moduleId = moduleIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            if (ContainsOption(options, moduleId))
                continue;

            options.Add(moduleId);
        }

        return options;
    }

    /// <summary>
    /// Resolves current module ID to a valid option value.
    /// </summary>
    /// <param name="currentModuleId">Current module ID string value.</param>
    /// <param name="options">Available module options.</param>
    /// <returns>Returns selected module ID guaranteed to be in options when options are present.</returns>
    public static string ResolveInitialModuleId(string currentModuleId, List<string> options)
    {
        if (options == null || options.Count == 0)
            return string.Empty;

        for (int index = 0; index < options.Count; index++)
        {
            string option = options[index];

            if (string.Equals(option, currentModuleId, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }

    /// <summary>
    /// Resolves module metadata for a specific module ID.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing moduleDefinitions.</param>
    /// <param name="moduleId">Module ID to resolve.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="displayName">Resolved module display name.</param>
    /// <returns>Returns true when module definition is found.</returns>
    public static bool TryResolveModuleInfo(SerializedObject serializedObject,
                                            string moduleId,
                                            out EnemyPatternModuleKind moduleKind,
                                            out string displayName)
    {
        moduleKind = EnemyPatternModuleKind.Grunt;
        displayName = string.Empty;

        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        SerializedProperty moduleDefinitionsProperty = serializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
            return false;

        for (int index = 0; index < moduleDefinitionsProperty.arraySize; index++)
        {
            SerializedProperty moduleDefinitionProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(index);

            if (moduleDefinitionProperty == null)
                continue;

            SerializedProperty moduleIdProperty = moduleDefinitionProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            if (!string.Equals(moduleIdProperty.stringValue, moduleId, StringComparison.OrdinalIgnoreCase))
                continue;

            SerializedProperty moduleKindProperty = moduleDefinitionProperty.FindPropertyRelative("moduleKind");
            SerializedProperty displayNameProperty = moduleDefinitionProperty.FindPropertyRelative("displayName");
            moduleKind = ResolveModuleKind(moduleKindProperty);
            displayName = displayNameProperty != null && !string.IsNullOrWhiteSpace(displayNameProperty.stringValue)
                ? displayNameProperty.stringValue
                : moduleId;
            return true;
        }

        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks if a list already contains an option value using case-insensitive comparison.
    /// </summary>
    /// <param name="options">Current options list.</param>
    /// <param name="value">Value to test.</param>
    /// <returns>Returns true when the value exists in the list.</returns>
    private static bool ContainsOption(List<string> options, string value)
    {
        if (options == null)
            return false;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
