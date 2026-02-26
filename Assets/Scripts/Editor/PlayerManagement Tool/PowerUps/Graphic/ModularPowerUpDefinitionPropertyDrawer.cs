using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ModularPowerUpDefinition))]
public sealed class ModularPowerUpDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty commonDataProperty = property.FindPropertyRelative("commonData");
        SerializedProperty moduleBindingsProperty = property.FindPropertyRelative("moduleBindings");
        SerializedProperty unreplaceableProperty = property.FindPropertyRelative("unreplaceable");

        if (commonDataProperty == null ||
            moduleBindingsProperty == null ||
            unreplaceableProperty == null)
        {
            Label errorLabel = new Label("ModularPowerUpDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        AddField(root, commonDataProperty, "Common Data");
        AddField(root, unreplaceableProperty, "Unreplaceable");

        Label modulesHeader = new Label("Module Bindings");
        modulesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        modulesHeader.style.marginTop = 4f;
        root.Add(modulesHeader);

        AddField(root, moduleBindingsProperty, "Modules");

        HelpBox helpBox = new HelpBox("Each binding references a Module ID from Modules Management and can optionally override payload values.", HelpBoxMessageType.Info);
        helpBox.style.marginTop = 4f;
        root.Add(helpBox);

        return root;
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        parent.Add(field);
    }
    #endregion
}
