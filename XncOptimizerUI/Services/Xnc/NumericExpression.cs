namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Validates and evaluates a numeric expression typed into a program table (bore coordinates,
    /// <c>&lt;var&gt;</c> <c>expr</c>). Accepted: decimal numbers (digits <c>0-9</c>, at most one
    /// <c>.</c> separator), the given symbols (any case) - typically <c>dx</c>/<c>dy</c>/<c>dz</c>
    /// plus the program's numeric <c>&lt;var&gt;</c>s - <c>+ - * /</c>, parentheses and spaces.
    /// A sign (<c>+</c>/<c>-</c>) may only open the text or a parenthesis - <c>2*-3</c> or
    /// <c>dx--5</c> are rejected. The text must evaluate to a finite number.
    /// </summary>
    public static class NumericExpression
    {
        private enum TokenKind
        {
            Number,
            Identifier,
            Operator,
            OpenParen,
            CloseParen,
            Invalid
        }

        private readonly record struct Token(TokenKind Kind, string Text);

        /// <summary>
        /// Evaluates <paramref name="text"/> against <paramref name="symbols"/>. Returns
        /// <c>false</c> with a user-facing <paramref name="error"/> when the text is not acceptable.
        /// </summary>
        public static bool TryEvaluate(string? text, XncSymbolTable symbols, out double value, out string? error)
        {
            value = 0d;
            error = string.IsNullOrWhiteSpace(text)
                ? "Value is required"
                : CheckTokens(Tokenize(text), symbols);

            return error is null && TryCompute(text!, symbols, out value, out error);
        }

        private static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            var pos = 0;

            while (pos < text.Length)
            {
                var c = text[pos];

                if (char.IsWhiteSpace(c))
                {
                    pos++;
                    continue;
                }

                var length = c switch
                {
                    _ when IsNumberChar(c) => RunLength(text, pos, IsNumberChar),
                    _ when IsIdentifierStart(c) => RunLength(text, pos, IsIdentifierPart),
                    _ => 1
                };

                tokens.Add(new Token(KindOf(c), text.Substring(pos, length)));
                pos += length;
            }

            return tokens;
        }

        private static TokenKind KindOf(char c) => c switch
        {
            _ when IsNumberChar(c) => TokenKind.Number,
            _ when IsIdentifierStart(c) => TokenKind.Identifier,
            '+' or '-' or '*' or '/' => TokenKind.Operator,
            '(' => TokenKind.OpenParen,
            ')' => TokenKind.CloseParen,
            _ => TokenKind.Invalid
        };

        /// <summary>First lexical problem in <paramref name="tokens"/>, or <c>null</c>.</summary>
        private static string? CheckTokens(IReadOnlyList<Token> tokens, XncSymbolTable symbols) =>
            tokens
                .Select((token, i) => CheckToken(token, i == 0 ? null : tokens[i - 1], symbols))
                .FirstOrDefault(error => error is not null);

        private static string? CheckToken(Token token, Token? previous, XncSymbolTable symbols) => token.Kind switch
        {
            TokenKind.Invalid => $"Invalid character '{token.Text}'",
            TokenKind.Number when token.Text.Count(ch => ch == '.') > 1 => $"'{token.Text}' has more than one decimal separator",
            TokenKind.Identifier when !symbols.TryGet(token.Text, out _) => $"Unknown variable '{token.Text}' (use dx, dy, dz or a program variable)",
            TokenKind.Operator when IsSign(token.Text) && IsSignMisplaced(previous) => $"'{token.Text}' sign is allowed only at the beginning",
            _ => null
        };

        /// <summary>
        /// A <c>+</c>/<c>-</c> right after another operator is a unary sign in the middle of the
        /// expression, which is not accepted (only at the start or right after <c>(</c>).
        /// </summary>
        private static bool IsSignMisplaced(Token? previous) => previous?.Kind == TokenKind.Operator;

        private static bool TryCompute(string text, XncSymbolTable symbols, out double value, out string? error)
        {
            try
            {
                value = XncExpressionEvaluator.Evaluate(text, symbols);
                error = double.IsFinite(value) ? null : "Value is not a finite number";
            }
            catch (XncProgramFormatException)
            {
                value = 0d;
                error = "Incomplete or malformed expression";
            }

            return error is null;
        }

        private static int RunLength(string text, int start, Func<char, bool> belongs)
        {
            var end = start;

            while (end < text.Length && belongs(text[end]))
            {
                end++;
            }

            return end - start;
        }

        private static bool IsSign(string op) => op is "+" or "-";

        private static bool IsNumberChar(char c) => c is (>= '0' and <= '9') or '.';

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        // '.' included so a dotted name such as tool.dia is reported as one unknown variable.
        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '.';
    }
}
