using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides shared visual helpers for shared Modules and Patterns preset card lists.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetCardUtility
{
    #region Constants
    private const float CardsMaxHeight = 520f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one horizontal row used by filters or section actions.
    /// /params alignToBottom True when children should align to their bottom edge.
    /// /returns Created row element.
    /// </summary>
    public static VisualElement CreateHorizontalRow(bool alignToBottom)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginBottom = 4f;
        row.style.alignItems = alignToBottom ? Align.FlexEnd : Align.Center;
        return row;
    }

    /// <summary>
    /// Creates one italic count label used below filter and action rows.
    /// /params None.
    /// /returns Created count label.
    /// </summary>
    public static Label CreateCountLabel()
    {
        Label countLabel = new Label();
        countLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        countLabel.style.marginBottom = 2f;
        return countLabel;
    }

    /// <summary>
    /// Creates one italic status label used for empty or unavailable list states.
    /// /params text Visible status text.
    /// /returns Created status label.
    /// </summary>
    public static Label CreateStatusLabel(string text)
    {
        Label statusLabel = new Label(text);
        statusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        return statusLabel;
    }

    /// <summary>
    /// Creates one scroll view used by card collections.
    /// /params None.
    /// /returns Created scroll view.
    /// </summary>
    public static ScrollView CreateCardsScrollView()
    {
        ScrollView cardsContainer = new ScrollView();
        cardsContainer.style.maxHeight = CardsMaxHeight;
        cardsContainer.style.paddingRight = 2f;
        cardsContainer.style.marginBottom = 2f;
        return cardsContainer;
    }

    /// <summary>
    /// Creates one styled card container used by module and pattern entries.
    /// /params None.
    /// /returns Created card container.
    /// </summary>
    public static VisualElement CreateCardContainer()
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = 10f;
        card.style.paddingLeft = 6f;
        card.style.paddingRight = 6f;
        card.style.paddingTop = 4f;
        card.style.paddingBottom = 4f;
        card.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
        card.style.borderBottomWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
        return card;
    }

    /// <summary>
    /// Creates one card-local actions row indented below the foldout header.
    /// /params None.
    /// /returns Created actions row.
    /// </summary>
    public static VisualElement CreateCardActionsRow()
    {
        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.flexWrap = Wrap.Wrap;
        actionsRow.style.marginLeft = 14f;
        actionsRow.style.marginTop = 2f;
        actionsRow.style.marginBottom = 4f;
        return actionsRow;
    }

    /// <summary>
    /// Creates one action button with optional left margin.
    /// /params buttonText Visible button label.
    /// /params tooltip Tooltip shown on the button.
    /// /params onClick Click callback.
    /// /params leftMargin Left margin applied after creation.
    /// /returns Created button.
    /// </summary>
    public static Button CreateActionButton(string buttonText,
                                            string tooltip,
                                            Action onClick,
                                            float leftMargin)
    {
        Button button = new Button(onClick);
        button.text = buttonText;
        button.tooltip = tooltip;

        if (leftMargin > 0f)
            button.style.marginLeft = leftMargin;

        return button;
    }

    /// <summary>
    /// Creates one delayed text field used by shared preset filters.
    /// /params label Visible field label.
    /// /params tooltip Tooltip shown on the field.
    /// /params value Current field value.
    /// /params rightMargin Right margin applied after creation.
    /// /returns Created filter field.
    /// </summary>
    public static TextField CreateDelayedFilterField(string label,
                                                     string tooltip,
                                                     string value,
                                                     float rightMargin)
    {
        TextField filterField = new TextField(label);
        filterField.isDelayed = true;
        filterField.tooltip = tooltip;
        filterField.value = value;
        filterField.style.flexGrow = 1f;

        if (rightMargin > 0f)
            filterField.style.marginRight = rightMargin;

        return filterField;
    }

    /// <summary>
    /// Creates one lightweight subsection header label.
    /// /params title Visible label text.
    /// /params tooltip Tooltip shown on the subsection label.
    /// /returns Created label.
    /// </summary>
    public static Label CreateSubSectionHeader(string title, string tooltip)
    {
        Label header = new Label(title);
        header.tooltip = tooltip;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 4f;
        header.style.marginBottom = 4f;
        return header;
    }

    /// <summary>
    /// Creates one subdued description label for section guidance without using helper boxes.
    /// /params text Visible description text.
    /// /returns Created description label.
    /// </summary>
    public static Label CreateDescriptionLabel(string text)
    {
        Label descriptionLabel = new Label(text);
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.marginBottom = 4f;
        return descriptionLabel;
    }
    #endregion

    #endregion
}
