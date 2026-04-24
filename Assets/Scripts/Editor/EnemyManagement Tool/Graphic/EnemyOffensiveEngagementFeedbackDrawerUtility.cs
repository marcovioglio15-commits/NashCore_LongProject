using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds reusable UI Toolkit editors and warnings for offensive engagement feedback settings.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyOffensiveEngagementFeedbackDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the editor UI for one offensive engagement feedback settings block.
    /// /params settingsProperty Serialized settings block to draw.
    /// /params onValueChanged Optional callback invoked after any serialized value changes.
    /// /returns The built visual element tree.
    /// </summary>
    public static VisualElement BuildSettingsEditor(SerializedProperty settingsProperty, Action onValueChanged = null)
    {
        VisualElement root = new VisualElement();

        if (settingsProperty == null)
        {
            HelpBox missingSettingsBox = new HelpBox("Offensive engagement feedback settings are missing.", HelpBoxMessageType.Warning);
            root.Add(missingSettingsBox);
            return root;
        }

        SerializedProperty enableColorBlendProperty = settingsProperty.FindPropertyRelative("enableColorBlend");
        SerializedProperty colorBlendColorProperty = settingsProperty.FindPropertyRelative("colorBlendColor");
        SerializedProperty colorBlendLeadTimeSecondsProperty = settingsProperty.FindPropertyRelative("colorBlendLeadTimeSeconds");
        SerializedProperty colorBlendFadeOutSecondsProperty = settingsProperty.FindPropertyRelative("colorBlendFadeOutSeconds");
        SerializedProperty colorBlendMaximumBlendProperty = settingsProperty.FindPropertyRelative("colorBlendMaximumBlend");
        SerializedProperty enableBillboardProperty = settingsProperty.FindPropertyRelative("enableBillboard");
        SerializedProperty billboardSpriteProperty = settingsProperty.FindPropertyRelative("billboardSprite");
        SerializedProperty billboardColorProperty = settingsProperty.FindPropertyRelative("billboardColor");
        SerializedProperty billboardLocalOffsetProperty = settingsProperty.FindPropertyRelative("billboardLocalOffset");
        SerializedProperty billboardLeadTimeSecondsProperty = settingsProperty.FindPropertyRelative("billboardLeadTimeSeconds");
        SerializedProperty billboardBaseScaleProperty = settingsProperty.FindPropertyRelative("billboardBaseScale");
        SerializedProperty billboardPulseScaleMultiplierProperty = settingsProperty.FindPropertyRelative("billboardPulseScaleMultiplier");
        SerializedProperty billboardPulseExpandDurationSecondsProperty = settingsProperty.FindPropertyRelative("billboardPulseExpandDurationSeconds");
        SerializedProperty billboardPulseContractDurationSecondsProperty = settingsProperty.FindPropertyRelative("billboardPulseContractDurationSeconds");

        if (enableColorBlendProperty == null ||
            colorBlendColorProperty == null ||
            colorBlendLeadTimeSecondsProperty == null ||
            colorBlendFadeOutSecondsProperty == null ||
            colorBlendMaximumBlendProperty == null ||
            enableBillboardProperty == null ||
            billboardSpriteProperty == null ||
            billboardColorProperty == null ||
            billboardLocalOffsetProperty == null ||
            billboardLeadTimeSecondsProperty == null ||
            billboardBaseScaleProperty == null ||
            billboardPulseScaleMultiplierProperty == null ||
            billboardPulseExpandDurationSecondsProperty == null ||
            billboardPulseContractDurationSecondsProperty == null)
        {
            HelpBox invalidSettingsBox = new HelpBox("Offensive engagement feedback fields are incomplete.", HelpBoxMessageType.Warning);
            root.Add(invalidSettingsBox);
            return root;
        }

        VisualElement colorBlendGroup = CreateGroupContainer("Color Blend");
        AddField(colorBlendGroup, enableColorBlendProperty, "Enable Color Blend", onValueChanged);
        AddField(colorBlendGroup, colorBlendColorProperty, "Color Blend Color", onValueChanged);
        AddField(colorBlendGroup, colorBlendLeadTimeSecondsProperty, "Color Blend Lead Time Seconds", onValueChanged);
        AddField(colorBlendGroup, colorBlendFadeOutSecondsProperty, "Color Blend Fade Out Seconds", onValueChanged);
        AddField(colorBlendGroup, colorBlendMaximumBlendProperty, "Color Blend Maximum Blend", onValueChanged);
        root.Add(colorBlendGroup);

        VisualElement billboardGroup = CreateGroupContainer("Billboard");
        AddField(billboardGroup, enableBillboardProperty, "Enable Billboard", onValueChanged);
        AddField(billboardGroup, billboardSpriteProperty, "Billboard Sprite", onValueChanged);
        AddField(billboardGroup, billboardColorProperty, "Billboard Color", onValueChanged);
        AddField(billboardGroup, billboardLocalOffsetProperty, "Billboard Local Offset", onValueChanged);
        AddField(billboardGroup, billboardLeadTimeSecondsProperty, "Billboard Lead Time Seconds", onValueChanged);
        AddField(billboardGroup, billboardBaseScaleProperty, "Billboard Base Scale", onValueChanged);
        AddField(billboardGroup, billboardPulseScaleMultiplierProperty, "Billboard Pulse Scale Multiplier", onValueChanged);
        AddField(billboardGroup, billboardPulseExpandDurationSecondsProperty, "Billboard Pulse Expand Duration Seconds", onValueChanged);
        AddField(billboardGroup, billboardPulseContractDurationSecondsProperty, "Billboard Pulse Contract Duration Seconds", onValueChanged);
        root.Add(billboardGroup);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.marginTop = 6f;
        root.Add(warningBox);
        RefreshWarnings(warningBox,
                        enableColorBlendProperty,
                        colorBlendLeadTimeSecondsProperty,
                        colorBlendFadeOutSecondsProperty,
                        colorBlendMaximumBlendProperty,
                        enableBillboardProperty,
                        billboardSpriteProperty,
                        billboardLeadTimeSecondsProperty,
                        billboardBaseScaleProperty,
                        billboardPulseScaleMultiplierProperty,
                        billboardPulseExpandDurationSecondsProperty,
                        billboardPulseContractDurationSecondsProperty);
        RegisterWarningRefresh(root,
                               enableColorBlendProperty,
                               colorBlendLeadTimeSecondsProperty,
                               colorBlendFadeOutSecondsProperty,
                               colorBlendMaximumBlendProperty,
                               enableBillboardProperty,
                               billboardSpriteProperty,
                               billboardLeadTimeSecondsProperty,
                               billboardBaseScaleProperty,
                               billboardPulseScaleMultiplierProperty,
                               billboardPulseExpandDurationSecondsProperty,
                               billboardPulseContractDurationSecondsProperty,
                               warningBox);
        return root;
    }

    /// <summary>
    /// Returns whether the currently selected module binding supports predictive engagement feedback in the provided catalog section.
    /// /params bindingProperty Serialized module binding.
    /// /params section Catalog section used to interpret the binding.
    /// /returns True when the currently selected module kind maps to a supported engagement timing mode.
    /// </summary>
    public static bool SupportsDisplayTrigger(SerializedProperty bindingProperty, EnemyPatternModuleCatalogSection section)
    {
        if (bindingProperty == null)
        {
            return false;
        }

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
        {
            return false;
        }

        string moduleId = moduleIdProperty.stringValue;

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return false;
        }

        bool resolvedModuleInfo = EnemyAdvancedPatternDrawerUtility.TryResolveModuleInfo(bindingProperty,
                                                                                         moduleId,
                                                                                         out EnemyPatternModuleKind moduleKind,
                                                                                         out string _);

        if (!resolvedModuleInfo)
        {
            return false;
        }

        return EnemyOffensiveEngagementSupportUtility.SupportsTimingMode(section, moduleKind);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one titled container used to visually separate major settings groups.
    /// /params title Group title shown above the contained fields.
    /// /returns The created group container.
    /// </summary>
    private static VisualElement CreateGroupContainer(string title)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;

        Label header = new Label(title);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);
        return container;
    }

    /// <summary>
    /// Adds one bound property field and routes local change notifications through the optional callback.
    /// /params parent Parent container that receives the property field.
    /// /params property Serialized property to bind.
    /// /params label UI label for the property field.
    /// /params onValueChanged Optional callback invoked after the field changes.
    /// /returns None.
    /// </summary>
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 string label,
                                 Action onValueChanged)
    {
        if (parent == null || property == null)
        {
            return;
        }

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            if (onValueChanged != null)
            {
                onValueChanged();
            }
        });
        parent.Add(field);
    }

    /// <summary>
    /// Registers warning refresh callbacks for every property that affects warning generation.
    /// /params root Root visual element that tracks property changes.
    /// /params warningProperties All properties that may affect warning text.
    /// /params warningBox Warning box refreshed after any tracked property changes.
    /// /returns None.
    /// </summary>
    private static void RegisterWarningRefresh(VisualElement root,
                                               SerializedProperty enableColorBlendProperty,
                                               SerializedProperty colorBlendLeadTimeSecondsProperty,
                                               SerializedProperty colorBlendFadeOutSecondsProperty,
                                               SerializedProperty colorBlendMaximumBlendProperty,
                                               SerializedProperty enableBillboardProperty,
                                               SerializedProperty billboardSpriteProperty,
                                               SerializedProperty billboardLeadTimeSecondsProperty,
                                               SerializedProperty billboardBaseScaleProperty,
                                               SerializedProperty billboardPulseScaleMultiplierProperty,
                                               SerializedProperty billboardPulseExpandDurationSecondsProperty,
                                               SerializedProperty billboardPulseContractDurationSecondsProperty,
                                               HelpBox warningBox)
    {
        if (root == null || warningBox == null)
        {
            return;
        }

        SerializedProperty[] warningProperties = new SerializedProperty[]
        {
            enableColorBlendProperty,
            colorBlendLeadTimeSecondsProperty,
            colorBlendFadeOutSecondsProperty,
            colorBlendMaximumBlendProperty,
            enableBillboardProperty,
            billboardSpriteProperty,
            billboardLeadTimeSecondsProperty,
            billboardBaseScaleProperty,
            billboardPulseScaleMultiplierProperty,
            billboardPulseExpandDurationSecondsProperty,
            billboardPulseContractDurationSecondsProperty
        };

        for (int propertyIndex = 0; propertyIndex < warningProperties.Length; propertyIndex++)
        {
            SerializedProperty trackedProperty = warningProperties[propertyIndex];

            if (trackedProperty == null)
            {
                continue;
            }

            root.TrackPropertyValue(trackedProperty, changedProperty =>
            {
                RefreshWarnings(warningBox,
                                enableColorBlendProperty,
                                colorBlendLeadTimeSecondsProperty,
                                colorBlendFadeOutSecondsProperty,
                                colorBlendMaximumBlendProperty,
                                enableBillboardProperty,
                                billboardSpriteProperty,
                                billboardLeadTimeSecondsProperty,
                                billboardBaseScaleProperty,
                                billboardPulseScaleMultiplierProperty,
                                billboardPulseExpandDurationSecondsProperty,
                                billboardPulseContractDurationSecondsProperty);
            });
        }
    }

    /// <summary>
    /// Rebuilds the consolidated warning text for the current settings block.
    /// /params warningBox Warning box updated in place.
    /// /params enableColorBlendProperty Serialized color-blend enable toggle.
    /// /params colorBlendLeadTimeSecondsProperty Serialized color-blend lead time.
    /// /params colorBlendFadeOutSecondsProperty Serialized color-blend fade-out duration.
    /// /params colorBlendMaximumBlendProperty Serialized color-blend maximum blend.
    /// /params enableBillboardProperty Serialized billboard enable toggle.
    /// /params billboardSpriteProperty Serialized billboard sprite reference.
    /// /params billboardLeadTimeSecondsProperty Serialized billboard lead time.
    /// /params billboardBaseScaleProperty Serialized billboard base scale.
    /// /params billboardPulseScaleMultiplierProperty Serialized billboard pulse multiplier.
    /// /params billboardPulseExpandDurationSecondsProperty Serialized billboard expand duration.
    /// /params billboardPulseContractDurationSecondsProperty Serialized billboard contract duration.
    /// /returns None.
    /// </summary>
    private static void RefreshWarnings(HelpBox warningBox,
                                        SerializedProperty enableColorBlendProperty,
                                        SerializedProperty colorBlendLeadTimeSecondsProperty,
                                        SerializedProperty colorBlendFadeOutSecondsProperty,
                                        SerializedProperty colorBlendMaximumBlendProperty,
                                        SerializedProperty enableBillboardProperty,
                                        SerializedProperty billboardSpriteProperty,
                                        SerializedProperty billboardLeadTimeSecondsProperty,
                                        SerializedProperty billboardBaseScaleProperty,
                                        SerializedProperty billboardPulseScaleMultiplierProperty,
                                        SerializedProperty billboardPulseExpandDurationSecondsProperty,
                                        SerializedProperty billboardPulseContractDurationSecondsProperty)
    {
        if (warningBox == null)
        {
            return;
        }

        List<string> warningLines = new List<string>();
        bool colorBlendEnabled = enableColorBlendProperty.boolValue;
        bool billboardEnabled = enableBillboardProperty.boolValue;

        if (!colorBlendEnabled && !billboardEnabled)
        {
            warningLines.Add("Both visual channels are disabled, so this feedback block will bake no visible result.");
        }

        if (colorBlendEnabled && colorBlendMaximumBlendProperty.floatValue <= 0f)
        {
            warningLines.Add("Color Blend Maximum Blend is 0 or below, so the color warning will stay invisible.");
        }

        if (colorBlendLeadTimeSecondsProperty.floatValue < 0f)
        {
            warningLines.Add("Negative Color Blend Lead Time Seconds values are treated as 0 at bake/runtime.");
        }

        if (colorBlendFadeOutSecondsProperty.floatValue < 0f)
        {
            warningLines.Add("Negative Color Blend Fade Out Seconds values are treated as 0 at bake/runtime.");
        }

        if (billboardEnabled && billboardSpriteProperty.objectReferenceValue == null)
        {
            warningLines.Add("Billboard is enabled but no sprite is assigned, so only the color channel can render.");
        }

        if (billboardLeadTimeSecondsProperty.floatValue < 0f)
        {
            warningLines.Add("Negative Billboard Lead Time Seconds values are treated as 0 at bake/runtime.");
        }

        if (billboardEnabled && billboardBaseScaleProperty.floatValue <= 0f)
        {
            warningLines.Add("Billboard Base Scale is 0 or below, so the billboard will stay invisible.");
        }

        if (billboardEnabled && billboardPulseScaleMultiplierProperty.floatValue < 1f)
        {
            warningLines.Add("Billboard Pulse Scale Multiplier below 1 shrinks the pulse instead of growing it.");
        }

        if (billboardPulseExpandDurationSecondsProperty.floatValue < 0f)
        {
            warningLines.Add("Negative Billboard Pulse Expand Duration Seconds values are treated as 0 at bake/runtime.");
        }

        if (billboardPulseContractDurationSecondsProperty.floatValue < 0f)
        {
            warningLines.Add("Negative Billboard Pulse Contract Duration Seconds values are treated as 0 at bake/runtime.");
        }

        warningBox.style.display = warningLines.Count > 0
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        warningBox.text = string.Join("\n", warningLines);
    }
    #endregion

    #endregion
}
