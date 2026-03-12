/// <summary>
/// Enumerates token categories used by the player stat formula parser and evaluator.
/// /params none.
/// /returns void.
/// </summary>
internal enum PlayerStatFormulaTokenType
{
    Number = 0,
    Variable = 1,
    Operator = 2,
    Function = 3,
    LeftParenthesis = 4,
    RightParenthesis = 5
}

/// <summary>
/// Represents one parsed infix token before reverse-polish conversion.
/// /params none.
/// /returns void.
/// </summary>
internal readonly struct PlayerStatFormulaToken
{
    #region Fields
    public readonly PlayerStatFormulaTokenType Type;
    public readonly float NumberValue;
    public readonly string TextValue;
    public readonly char OperatorSymbol;
    public readonly bool IsUnary;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one token value with the specified raw data.
    /// /params type: Token category.
    /// /params numberValue: Parsed numeric value when applicable.
    /// /params textValue: Parsed text payload when applicable.
    /// /params operatorSymbol: Operator character when applicable.
    /// /params isUnary: Whether the operator acts as unary.
    /// /returns New token instance.
    /// </summary>
    private PlayerStatFormulaToken(PlayerStatFormulaTokenType type,
                                   float numberValue,
                                   string textValue,
                                   char operatorSymbol,
                                   bool isUnary)
    {
        Type = type;
        NumberValue = numberValue;
        TextValue = textValue;
        OperatorSymbol = operatorSymbol;
        IsUnary = isUnary;
    }
    #endregion

    #region Methods
    public static PlayerStatFormulaToken CreateNumber(float numberValue)
    {
        return new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Number, numberValue, string.Empty, '\0', false);
    }

    public static PlayerStatFormulaToken CreateVariable(string textValue)
    {
        return new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Variable, 0f, textValue, '\0', false);
    }

    public static PlayerStatFormulaToken CreateOperator(char operatorSymbol, bool isUnary)
    {
        return new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Operator, 0f, string.Empty, operatorSymbol, isUnary);
    }

    public static PlayerStatFormulaToken CreateFunction(string textValue)
    {
        return new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Function, 0f, textValue, '\0', false);
    }

    public static PlayerStatFormulaToken CreateParenthesis(bool isLeft)
    {
        PlayerStatFormulaTokenType type = isLeft ? PlayerStatFormulaTokenType.LeftParenthesis : PlayerStatFormulaTokenType.RightParenthesis;
        return new PlayerStatFormulaToken(type, 0f, string.Empty, '\0', false);
    }
    #endregion
}

/// <summary>
/// Represents one reverse-polish token used by the compiled formula runtime evaluator.
/// /params none.
/// /returns void.
/// </summary>
internal readonly struct PlayerStatFormulaRpnToken
{
    #region Fields
    public readonly PlayerStatFormulaTokenType Type;
    public readonly float NumberValue;
    public readonly string TextValue;
    public readonly char OperatorSymbol;
    public readonly bool IsUnary;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one reverse-polish token value.
    /// /params type: Token category.
    /// /params numberValue: Parsed numeric value when applicable.
    /// /params textValue: Parsed text payload when applicable.
    /// /params operatorSymbol: Operator character when applicable.
    /// /params isUnary: Whether the operator acts as unary.
    /// /returns New reverse-polish token instance.
    /// </summary>
    private PlayerStatFormulaRpnToken(PlayerStatFormulaTokenType type,
                                      float numberValue,
                                      string textValue,
                                      char operatorSymbol,
                                      bool isUnary)
    {
        Type = type;
        NumberValue = numberValue;
        TextValue = textValue;
        OperatorSymbol = operatorSymbol;
        IsUnary = isUnary;
    }
    #endregion

    #region Methods
    public static PlayerStatFormulaRpnToken CreateNumber(float numberValue)
    {
        return new PlayerStatFormulaRpnToken(PlayerStatFormulaTokenType.Number, numberValue, string.Empty, '\0', false);
    }

    public static PlayerStatFormulaRpnToken CreateVariable(string textValue)
    {
        return new PlayerStatFormulaRpnToken(PlayerStatFormulaTokenType.Variable, 0f, textValue, '\0', false);
    }

    public static PlayerStatFormulaRpnToken CreateFunction(string textValue)
    {
        return new PlayerStatFormulaRpnToken(PlayerStatFormulaTokenType.Function, 0f, textValue, '\0', false);
    }

    public static PlayerStatFormulaRpnToken FromOperatorToken(PlayerStatFormulaToken token)
    {
        return new PlayerStatFormulaRpnToken(PlayerStatFormulaTokenType.Operator, 0f, string.Empty, token.OperatorSymbol, token.IsUnary);
    }
    #endregion
}
