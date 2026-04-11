using System.Runtime.CompilerServices;

namespace Jint.Native.Function
{
    internal struct SourceText
    {
        // Either a string that stores the full source text that the function was parsed from,
        // or a StrongBox<string> containing the materialized source text of the function,
        // or null when the function doesn't have source text.
        private object? _fullSourceTextOrBoxedValue;

        public SourceText(string? fullSourceText)
        {
            _fullSourceTextOrBoxedValue = fullSourceText;
        }

        public string? GetValue(int start, int end)
        {
            if (_fullSourceTextOrBoxedValue is null)
            {
                return null;
            }

            if (_fullSourceTextOrBoxedValue is StrongBox<string> boxedValue)
            {
                return boxedValue.Value;
            }

            var fullSourceText = (string) _fullSourceTextOrBoxedValue;

            if (start < 0)
            {
                start = AstExtensions.GetSecondTokenStartIndex(fullSourceText, ~start, end);
            }

            var value = fullSourceText.Substring(start, end - start);
            _fullSourceTextOrBoxedValue = new StrongBox<string>(value);
            return value;
        }
    }
}
