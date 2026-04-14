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

        public string? GetValue(Node sourceTextNode)
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

            var (start, end) = sourceTextNode.Range;

            // In the case of static class methods, the static keyword is not included.
            if (sourceTextNode is MethodDefinition { Static: true } m)
            {
                start = AstExtensions.GetSecondTokenStartIndex(fullSourceText, start, end);
            }

            var value = fullSourceText.Substring(start, end - start);

            // Setting this field also releases the reference to the full source text string instance.
            _fullSourceTextOrBoxedValue = new StrongBox<string>(value);

            return value;
        }
    }
}
