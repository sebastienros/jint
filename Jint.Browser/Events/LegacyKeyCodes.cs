namespace Jint.Browser.Events;

/// <summary>
/// UI Events' legacy key attributes — <c>charCode</c> and <c>keyCode</c> — derived from a modern
/// <c>KeyboardEvent.key</c> and the event's type.
/// <para>
/// https://w3c.github.io/uievents/#legacy-key-attributes
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The specification's own words are that these attributes are "not consistent across platform or browser
/// implementations" and that a value here "may differ"; what it does fix is the <b>shape</b>, and that is what
/// this implements. On <c>keypress</c>, <c>charCode</c> is the character's code point and <c>keyCode</c> takes
/// the same value; on <c>keydown</c> and <c>keyup</c>, <c>charCode</c> is zero and <c>keyCode</c> is a virtual
/// key code from the fixed table.
/// </para>
/// <para>
/// The table is the US layout's, which is the one every implementation agrees on and the one a page's
/// <c>keyCode === 13</c> test was written against. A key it does not know answers 0, which is what a browser
/// answers for a key with no legacy code.
/// </para>
/// </remarks>
internal static class LegacyKeyCodes
{
    /// <summary>
    /// https://w3c.github.io/uievents/#dom-keyboardevent-charcode — the character code, non-zero only on
    /// <c>keypress</c> and only for a key that produces a character.
    /// </summary>
    internal static double CharCodeFor(string type, string key)
    {
        if (!string.Equals(type, "keypress", StringComparison.Ordinal))
        {
            return 0;
        }

        // A printable key's `key` is the character it produced, which is one code point; every named key is
        // longer than one UTF-16 unit or is a surrogate pair, and neither produces a character.
        return key.Length == 1 ? key[0] : 0;
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-keyboardevent-keycode — the virtual key code.
    /// </summary>
    internal static double KeyCodeFor(string type, string key)
    {
        if (string.Equals(type, "keypress", StringComparison.Ordinal))
        {
            return CharCodeFor(type, key);
        }

        if (key.Length == 1)
        {
            return CodeForCharacter(key[0]);
        }

        return CodeForNamedKey(key);
    }

    /// <summary>
    /// The virtual key code of a printable key, from the character it produces on a US layout. A letter
    /// answers its upper-case code point whichever case it produced, and a shifted punctuation key answers the
    /// unshifted key's code — both are what the legacy model means by "the key", as opposed to the character.
    /// </summary>
    private static double CodeForCharacter(char c)
    {
        if (c is >= 'a' and <= 'z')
        {
            return c - 'a' + 'A';
        }

        if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return c;
        }

        return c switch
        {
            ' ' => 32,
            ';' or ':' => 186,
            '=' or '+' => 187,
            ',' or '<' => 188,
            '-' or '_' => 189,
            '.' or '>' => 190,
            '/' or '?' => 191,
            '`' or '~' => 192,
            '[' or '{' => 219,
            '\\' or '|' => 220,
            ']' or '}' => 221,
            '\'' or '"' => 222,

            // The shifted digits: the key is the digit, so the code is the digit's.
            '!' => '1',
            '@' => '2',
            '#' => '3',
            '$' => '4',
            '%' => '5',
            '^' => '6',
            '&' => '7',
            '*' => '8',
            '(' => '9',
            ')' => '0',
            _ => 0,
        };
    }

    /// <summary>The fixed virtual key codes for the named keys — https://w3c.github.io/uievents/#fixed-virtual-key-codes.</summary>
    private static double CodeForNamedKey(string key) => key switch
    {
        "Backspace" => 8,
        "Tab" => 9,
        "Clear" => 12,
        "Enter" => 13,
        "Shift" => 16,
        "Control" => 17,
        "Alt" => 18,
        "Pause" => 19,
        "CapsLock" => 20,
        "Escape" => 27,
        "PageUp" => 33,
        "PageDown" => 34,
        "End" => 35,
        "Home" => 36,
        "ArrowLeft" => 37,
        "ArrowUp" => 38,
        "ArrowRight" => 39,
        "ArrowDown" => 40,
        "Insert" => 45,
        "Delete" => 46,
        "Meta" or "OS" => 91,
        "ContextMenu" => 93,
        "NumLock" => 144,
        "ScrollLock" => 145,
        _ => FunctionKey(key),
    };

    /// <summary>F1 through F24, which are 112 upwards and are the only named keys with a numeric suffix.</summary>
    private static double FunctionKey(string key)
    {
        if (key.Length < 2 || key[0] != 'F' || !int.TryParse(key.AsSpan(1), out var number) || number is < 1 or > 24)
        {
            return 0;
        }

        return 111 + number;
    }
}
