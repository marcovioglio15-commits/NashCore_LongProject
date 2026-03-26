using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Provides the parser and compiler steps that transform raw player stat formulas into executable RPN programs.
///  none.
/// </summary>
internal static class PlayerStatFormulaCompilationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles one normalized formula string into a reusable compiled formula.
    /// Used by the cached public engine wrapper after cache lookup.
    ///  formula: Normalized formula string to compile.
    ///  requireAtLeastOneVariable: Whether formulas without variables should be rejected.
    /// returns Compilation result containing the compiled formula or the failure reason.
    /// </summary>
    internal static PlayerStatFormulaCompileResult Compile(string formula, bool requireAtLeastOneVariable)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return new PlayerStatFormulaCompileResult(false, null, "Formula is empty.");

        if (!Tokenize(formula, out List<PlayerStatFormulaToken> tokens, out string tokenizationError))
            return new PlayerStatFormulaCompileResult(false, null, tokenizationError);

        if (!BuildRpn(tokens, out List<PlayerStatFormulaRpnToken> rpnTokens, out HashSet<string> variableNames, out string parseError))
            return new PlayerStatFormulaCompileResult(false, null, parseError);

        if (requireAtLeastOneVariable && variableNames.Count == 0)
            return new PlayerStatFormulaCompileResult(false, null, "Formula must reference at least one variable ([this] or [StatName]).");

        if (!ValidateRpnStructure(rpnTokens, out string structureError))
            return new PlayerStatFormulaCompileResult(false, null, structureError);

        string[] variableNamesArray = new string[variableNames.Count];
        variableNames.CopyTo(variableNamesArray);
        PlayerCompiledStatFormula compiledFormula = new PlayerCompiledStatFormula(rpnTokens.ToArray(), variableNamesArray);
        return new PlayerStatFormulaCompileResult(true, compiledFormula, string.Empty);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Tokenizes one raw formula string into infix tokens.
    ///  formula: Formula text to scan.
    ///  tokens: Produced infix token list.
    ///  errorMessage: Failure reason when tokenization stops.
    /// returns True when tokenization succeeded.
    /// </summary>
    private static bool Tokenize(string formula,
                                 out List<PlayerStatFormulaToken> tokens,
                                 out string errorMessage)
    {
        tokens = new List<PlayerStatFormulaToken>();
        errorMessage = string.Empty;
        int index = 0;

        while (index < formula.Length)
        {
            char currentCharacter = formula[index];

            if (char.IsWhiteSpace(currentCharacter))
            {
                index += 1;
                continue;
            }

            if (currentCharacter == '[')
            {
                int closingBracketIndex = formula.IndexOf(']', index + 1);

                if (closingBracketIndex < 0)
                {
                    errorMessage = "Variable token is missing closing ']'.";
                    return false;
                }

                string variableName = formula.Substring(index + 1, closingBracketIndex - index - 1).Trim();

                if (string.IsNullOrWhiteSpace(variableName))
                {
                    errorMessage = "Variable token cannot be empty.";
                    return false;
                }

                tokens.Add(PlayerStatFormulaToken.CreateVariable(variableName));
                index = closingBracketIndex + 1;
                continue;
            }

            if (IsNumberStart(formula, index))
            {
                if (!TryReadNumberToken(formula, ref index, out float numberValue))
                {
                    errorMessage = "Invalid number literal in formula.";
                    return false;
                }

                tokens.Add(PlayerStatFormulaToken.CreateNumber(numberValue));
                continue;
            }

            if (IsOperator(currentCharacter))
            {
                tokens.Add(PlayerStatFormulaToken.CreateOperator(currentCharacter, false));
                index += 1;
                continue;
            }

            if (currentCharacter == '(')
            {
                tokens.Add(PlayerStatFormulaToken.CreateParenthesis(true));
                index += 1;
                continue;
            }

            if (currentCharacter == ')')
            {
                tokens.Add(PlayerStatFormulaToken.CreateParenthesis(false));
                index += 1;
                continue;
            }

            if (char.IsLetter(currentCharacter))
            {
                int identifierStartIndex = index;

                while (index < formula.Length && char.IsLetter(formula[index]))
                    index += 1;

                string identifier = formula.Substring(identifierStartIndex, index - identifierStartIndex);

                if (!string.Equals(identifier, "log", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture, "Unsupported identifier '{0}'.", identifier);
                    return false;
                }

                int lookAheadIndex = index;

                while (lookAheadIndex < formula.Length && char.IsWhiteSpace(formula[lookAheadIndex]))
                    lookAheadIndex += 1;

                if (lookAheadIndex >= formula.Length || formula[lookAheadIndex] != '(')
                {
                    errorMessage = "Function log must be followed by parentheses.";
                    return false;
                }

                tokens.Add(PlayerStatFormulaToken.CreateFunction("log"));
                continue;
            }

            errorMessage = string.Format(CultureInfo.InvariantCulture, "Unsupported character '{0}' in formula.", currentCharacter);
            return false;
        }

        if (tokens.Count == 0)
        {
            errorMessage = "Formula produced no tokens.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts one infix token stream into reverse-polish notation.
    ///  tokens: Infix token sequence produced by the tokenizer.
    ///  rpnTokens: Resulting RPN token list.
    ///  variableNames: Distinct variables discovered while compiling.
    ///  errorMessage: Failure reason when parsing fails.
    /// returns True when the conversion succeeded.
    /// </summary>
    private static bool BuildRpn(List<PlayerStatFormulaToken> tokens,
                                 out List<PlayerStatFormulaRpnToken> rpnTokens,
                                 out HashSet<string> variableNames,
                                 out string errorMessage)
    {
        rpnTokens = new List<PlayerStatFormulaRpnToken>(tokens.Count);
        variableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        errorMessage = string.Empty;
        Stack<PlayerStatFormulaToken> operatorStack = new Stack<PlayerStatFormulaToken>();
        PlayerStatFormulaToken? previousToken = null;

        for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            PlayerStatFormulaToken currentToken = tokens[tokenIndex];

            switch (currentToken.Type)
            {
                case PlayerStatFormulaTokenType.Number:
                    rpnTokens.Add(PlayerStatFormulaRpnToken.CreateNumber(currentToken.NumberValue));
                    previousToken = currentToken;
                    continue;
                case PlayerStatFormulaTokenType.Variable:
                    variableNames.Add(currentToken.TextValue);
                    rpnTokens.Add(PlayerStatFormulaRpnToken.CreateVariable(currentToken.TextValue));
                    previousToken = currentToken;
                    continue;
                case PlayerStatFormulaTokenType.Function:
                    operatorStack.Push(currentToken);
                    previousToken = currentToken;
                    continue;
                case PlayerStatFormulaTokenType.LeftParenthesis:
                    operatorStack.Push(currentToken);
                    previousToken = currentToken;
                    continue;
                case PlayerStatFormulaTokenType.RightParenthesis:
                    if (!PopUntilLeftParenthesis(operatorStack, rpnTokens, out errorMessage))
                        return false;

                    if (operatorStack.Count > 0 && operatorStack.Peek().Type == PlayerStatFormulaTokenType.Function)
                    {
                        PlayerStatFormulaToken functionToken = operatorStack.Pop();
                        rpnTokens.Add(PlayerStatFormulaRpnToken.CreateFunction(functionToken.TextValue));
                    }

                    previousToken = currentToken;
                    continue;
                case PlayerStatFormulaTokenType.Operator:
                    PlayerStatFormulaToken resolvedOperator = ResolveOperatorToken(currentToken, previousToken);

                    while (operatorStack.Count > 0 && ShouldPopOperator(operatorStack.Peek(), resolvedOperator))
                    {
                        PlayerStatFormulaToken poppedOperator = operatorStack.Pop();
                        rpnTokens.Add(PlayerStatFormulaRpnToken.FromOperatorToken(poppedOperator));
                    }

                    operatorStack.Push(resolvedOperator);
                    previousToken = resolvedOperator;
                    continue;
            }
        }

        while (operatorStack.Count > 0)
        {
            PlayerStatFormulaToken stackToken = operatorStack.Pop();

            if (stackToken.Type == PlayerStatFormulaTokenType.LeftParenthesis || stackToken.Type == PlayerStatFormulaTokenType.RightParenthesis)
            {
                errorMessage = "Mismatched parentheses in formula.";
                return false;
            }

            if (stackToken.Type == PlayerStatFormulaTokenType.Function)
            {
                rpnTokens.Add(PlayerStatFormulaRpnToken.CreateFunction(stackToken.TextValue));
                continue;
            }

            rpnTokens.Add(PlayerStatFormulaRpnToken.FromOperatorToken(stackToken));
        }

        return true;
    }

    /// <summary>
    /// Validates the final RPN stream by simulating stack depth changes.
    ///  rpnTokens: Reverse-polish token stream to validate.
    ///  errorMessage: Failure reason when structure is invalid.
    /// returns True when the RPN stream is structurally valid.
    /// </summary>
    private static bool ValidateRpnStructure(List<PlayerStatFormulaRpnToken> rpnTokens, out string errorMessage)
    {
        errorMessage = string.Empty;
        int depth = 0;

        for (int index = 0; index < rpnTokens.Count; index++)
        {
            PlayerStatFormulaRpnToken token = rpnTokens[index];

            switch (token.Type)
            {
                case PlayerStatFormulaTokenType.Number:
                case PlayerStatFormulaTokenType.Variable:
                    depth += 1;
                    continue;
                case PlayerStatFormulaTokenType.Function:
                    if (depth < 1)
                    {
                        errorMessage = "Function has no operand.";
                        return false;
                    }

                    continue;
                case PlayerStatFormulaTokenType.Operator:
                    if (token.IsUnary)
                    {
                        if (depth < 1)
                        {
                            errorMessage = "Unary operator has no operand.";
                            return false;
                        }

                        continue;
                    }

                    if (depth < 2)
                    {
                        errorMessage = "Binary operator has insufficient operands.";
                        return false;
                    }

                    depth -= 1;
                    continue;
            }
        }

        if (depth != 1)
        {
            errorMessage = "Formula expression is incomplete.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether one formula character starts a number literal.
    ///  formula: Formula source text.
    ///  index: Character index to inspect.
    /// returns True when the character begins a valid numeric token.
    /// </summary>
    private static bool IsNumberStart(string formula, int index)
    {
        if (index < 0 || index >= formula.Length)
            return false;

        char currentCharacter = formula[index];

        if (char.IsDigit(currentCharacter))
            return true;

        if (currentCharacter == '.')
        {
            int nextIndex = index + 1;

            if (nextIndex < formula.Length && char.IsDigit(formula[nextIndex]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reads one numeric literal starting at the provided index.
    ///  formula: Formula source text.
    ///  index: Current parser index, advanced past the parsed literal.
    ///  value: Parsed float value.
    /// returns True when the number literal is valid.
    /// </summary>
    private static bool TryReadNumberToken(string formula, ref int index, out float value)
    {
        value = 0f;
        int startIndex = index;
        bool hasDecimalSeparator = false;

        while (index < formula.Length)
        {
            char currentCharacter = formula[index];

            if (char.IsDigit(currentCharacter))
            {
                index += 1;
                continue;
            }

            if (currentCharacter == '.')
            {
                if (hasDecimalSeparator)
                    return false;

                hasDecimalSeparator = true;
                index += 1;
                continue;
            }

            break;
        }

        string numberToken = formula.Substring(startIndex, index - startIndex);
        return float.TryParse(numberToken, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Checks whether the character is one of the supported operator symbols.
    ///  character: Character to inspect.
    /// returns True when the character maps to one supported operator.
    /// </summary>
    private static bool IsOperator(char character)
    {
        switch (character)
        {
            case '+':
            case '-':
            case '*':
            case '/':
            case '^':
                return true;
        }

        return false;
    }

    /// <summary>
    /// Converts one raw operator token into its final unary or binary representation.
    ///  token: Operator token being resolved.
    ///  previousToken: Previous token in the infix stream.
    /// returns Operator token with the correct unary flag.
    /// </summary>
    private static PlayerStatFormulaToken ResolveOperatorToken(PlayerStatFormulaToken token, PlayerStatFormulaToken? previousToken)
    {
        if (token.OperatorSymbol != '-')
            return token;

        if (!previousToken.HasValue)
            return PlayerStatFormulaToken.CreateOperator('-', true);

        PlayerStatFormulaTokenType previousType = previousToken.Value.Type;

        if (previousType == PlayerStatFormulaTokenType.Operator)
            return PlayerStatFormulaToken.CreateOperator('-', true);

        if (previousType == PlayerStatFormulaTokenType.LeftParenthesis)
            return PlayerStatFormulaToken.CreateOperator('-', true);

        if (previousType == PlayerStatFormulaTokenType.Function)
            return PlayerStatFormulaToken.CreateOperator('-', true);

        return token;
    }

    /// <summary>
    /// Applies precedence and associativity rules between two operators.
    ///  stackToken: Operator currently on the stack.
    ///  currentToken: Incoming operator token.
    /// returns True when the stacked operator must be emitted first.
    /// </summary>
    private static bool ShouldPopOperator(PlayerStatFormulaToken stackToken, PlayerStatFormulaToken currentToken)
    {
        if (stackToken.Type == PlayerStatFormulaTokenType.Function)
            return true;

        if (stackToken.Type != PlayerStatFormulaTokenType.Operator)
            return false;

        int stackPrecedence = GetPrecedence(stackToken);
        int currentPrecedence = GetPrecedence(currentToken);

        if (stackPrecedence > currentPrecedence)
            return true;

        if (stackPrecedence < currentPrecedence)
            return false;

        if (currentToken.IsUnary)
            return false;

        if (currentToken.OperatorSymbol == '^')
            return false;

        return true;
    }

    /// <summary>
    /// Resolves numeric precedence for one operator token.
    ///  token: Operator token being inspected.
    /// returns Numeric precedence used by the shunting-yard algorithm.
    /// </summary>
    private static int GetPrecedence(PlayerStatFormulaToken token)
    {
        if (token.Type != PlayerStatFormulaTokenType.Operator)
            return 0;

        if (token.IsUnary)
            return 4;

        switch (token.OperatorSymbol)
        {
            case '^':
                return 3;
            case '*':
            case '/':
                return 2;
            case '+':
            case '-':
                return 1;
        }

        return 0;
    }

    /// <summary>
    /// Pops operators until one left parenthesis is reached.
    ///  operatorStack: Current shunting-yard operator stack.
    ///  rpnTokens: Output RPN token list.
    ///  errorMessage: Failure reason when no matching parenthesis exists.
    /// returns True when one matching left parenthesis was found.
    /// </summary>
    private static bool PopUntilLeftParenthesis(Stack<PlayerStatFormulaToken> operatorStack,
                                                List<PlayerStatFormulaRpnToken> rpnTokens,
                                                out string errorMessage)
    {
        errorMessage = string.Empty;

        while (operatorStack.Count > 0)
        {
            PlayerStatFormulaToken stackToken = operatorStack.Pop();

            if (stackToken.Type == PlayerStatFormulaTokenType.LeftParenthesis)
                return true;

            if (stackToken.Type == PlayerStatFormulaTokenType.Function)
            {
                rpnTokens.Add(PlayerStatFormulaRpnToken.CreateFunction(stackToken.TextValue));
                continue;
            }

            rpnTokens.Add(PlayerStatFormulaRpnToken.FromOperatorToken(stackToken));
        }

        errorMessage = "Mismatched parentheses in formula.";
        return false;
    }
    #endregion

    #endregion
}
