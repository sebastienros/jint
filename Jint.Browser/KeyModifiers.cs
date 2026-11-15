namespace Jint.Browser;

/// <summary>Modifier keys held while driving a page's keyboard.</summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>No modifier keys.</summary>
    None = 0,
    /// <summary>The Alt key.</summary>
    Alt = 1,
    /// <summary>The Control key.</summary>
    Control = 2,
    /// <summary>The Meta key (Command on macOS).</summary>
    Meta = 4,
    /// <summary>The Shift key.</summary>
    Shift = 8,
}
