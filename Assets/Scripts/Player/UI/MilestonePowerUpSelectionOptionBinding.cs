using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stores references for one HUD option used in milestone power-up selection.
/// </summary>
[System.Serializable]
public sealed class MilestonePowerUpSelectionOptionBinding
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Button that submits this option index to ECS when clicked.")]
    [SerializeField] private Button selectButton;

    [Tooltip("UI Text used to display the power-up name.")]
    [SerializeField] private Text nameText;

    [Tooltip("UI Text used to display the power-up description.")]
    [SerializeField] private Text descriptionText;

    [Tooltip("UI Text used to display the power-up kind (Active/Passive).")]
    [SerializeField] private Text typeText;

    [Tooltip("Hide this option GameObject when no rolled offer exists at the same index.")]
    [SerializeField] private bool hideWhenUnused = true;
    #endregion

    #endregion

    #region Properties
    public Button SelectButton
    {
        get
        {
            return selectButton;
        }
    }

    public Text NameText
    {
        get
        {
            return nameText;
        }
    }

    public Text DescriptionText
    {
        get
        {
            return descriptionText;
        }
    }

    public Text TypeText
    {
        get
        {
            return typeText;
        }
    }

    public bool HideWhenUnused
    {
        get
        {
            return hideWhenUnused;
        }
    }
    #endregion
}
