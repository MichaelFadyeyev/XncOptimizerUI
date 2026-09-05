using System.Globalization;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Minimal recursive-descent evaluator for the arithmetic found in XNC program
    /// attributes: <c>+ - * /</c>, parentheses, unary sign, decimal literals and
    /// identifiers (including the dotted <c>tool.dia</c>). Identifiers are resolved
    /// case-insensitively against an <see cref="XncSymbolTable"/>.
    /// </summary>
    public static class XncExpressionEvaluator
    {
        /// <summary>
        /// Evaluates <paramref name="expression"/> (a literal such as <c>-10</c>, a bare
        /// variable such as <c>contMillDepth</c>, or a formula such as <c>dy-35-40</c>).
        /// </summary>
        /// <exception cref="XncProgramFormatException">The text is empty, malformed, or names an unknown identifier.</exception>
        public static double Evaluate(string expression, XncSymbolTable symbols)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new XncProgramFormatException("Empty XNC expression.");
            }

            var parser = new Parser(expression, symbols);
            var value = parser.ParseExpression();
            parser.ExpectEnd();

            return value;
        }

        private ref struct Parser(ReadOnlySpan<char> text, XncSymbolTable symbols)
        {
            private readonly ReadOnlySpan<char> _text = text;
            private readonly XncSymbolTable _symbols = symbols;
            private int _pos = 0;

            public double ParseExpression()
            {
                var value = ParseTerm();

                while (true)
                {
                    SkipWhitespace();
                    if (TryConsume('+')) value += ParseTerm();
                    else if (TryConsume('-')) value -= ParseTerm();
                    else return value;
                }
            }

            private double ParseTerm()
            {
                var value = ParseFactor();

                while (true)
                {
                    SkipWhitespace();
                    if (TryConsume('*')) value *= ParseFactor();
                    else if (TryConsume('/')) value /= ParseFactor();
                    else return value;
                }
            }

            private double ParseFactor()
            {
                SkipWhitespace();

                if (TryConsume('+')) return ParseFactor();
                if (TryConsume('-')) return -ParseFactor();

                if (TryConsume('('))
                {
                    var value = ParseExpression();
                    SkipWhitespace();
                    if (!TryConsume(')'))
                    {
                        throw Error("expected ')'");
                    }

                    return value;
                }

                if (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                {
                    return ParseNumber();
                }

                if (_pos < _text.Length && IsIdentifierStart(_text[_pos]))
                {
                    return ParseIdentifier();
                }

                throw Error("expected a number, identifier or '('");
            }

            private double ParseNumber()
            {
                var start = _pos;
                while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                {
                    _pos++;
                }

                var slice = _text[start.._pos];
                if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    throw Error($"'{slice.ToString()}' is not a number");
                }

                return value;
            }

            private double ParseIdentifier()
            {
                var start = _pos;
                while (_pos < _text.Length && IsIdentifierPart(_text[_pos]))
                {
                    _pos++;
                }

                var name = _text[start.._pos].ToString();
                if (!_symbols.TryGet(name, out var value))
                {
                    throw Error($"unknown identifier '{name}'");
                }

                return value;
            }

            public void ExpectEnd()
            {
                SkipWhitespace();
                if (_pos != _text.Length)
                {
                    throw Error("unexpected trailing characters");
                }
            }

            private void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
                {
                    _pos++;
                }
            }

            private bool TryConsume(char c)
            {
                if (_pos < _text.Length && _text[_pos] == c)
                {
                    _pos++;
                    return true;
                }

                return false;
            }

            private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

            private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';

            private readonly XncProgramFormatException Error(string what) =>
                new($"Invalid XNC expression '{_text.ToString()}' near position {_pos}: {what}.");
        }
    }
}
