using System.Text;

namespace Jint.Native.String;

/// <summary>
/// Some internacionalization logic that is special or specific to determined culture.
/// </summary>
internal class StringInlHelper
{
    private static List<int> GetLithuaninanReplaceableCharIdx(string input)
    {
        List<int> replaceableCharsIdx = new List<int>();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i].Equals('\u0307'))
            {
                replaceableCharsIdx.Add(i);
            }
        }

        // For capital I and J we do not replace the dot above (\u3017).
        replaceableCharsIdx
            .RemoveAll(idx => (idx > 0) && input[idx - 1] == 'I' || input[idx - 1] == 'J');

        return replaceableCharsIdx;
    }

    /// <summary>
    /// Lithuanian case is a bit special. For more info see:
    /// https://github.com/tc39/test262/blob/main/test/intl402/String/prototype/toLocaleUpperCase/special_casing_Lithuanian.js
    /// </summary>
    public static string LithuanianStringProcessor(string input)
    {
        var replaceableCharsIdx = GetLithuaninanReplaceableCharIdx(input);
        if (replaceableCharsIdx.Count > 0)
        {
            StringBuilder stringBuilder = new StringBuilder(input);

            // Remove characters in reverse order to avoid index shifting
            for (int i = replaceableCharsIdx.Count - 1; i >= 0; i--)
            {
                int index = replaceableCharsIdx[i];
                if (index >= 0 && index < stringBuilder.Length)
                {
                    stringBuilder.Remove(index, 1);
                }
            }

            return stringBuilder.ToString();
        }

        return input;
    }
}
