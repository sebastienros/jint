namespace Jint.Browser.Accessibility;

/// <summary>
/// What a role target asks of an element's accessibility node: the role, the accessible name, and the
/// states <see cref="AccessibilityTree"/> publishes.
/// </summary>
/// <remarks>
/// <para>
/// Every criterion is answered by the same computation an accessibility snapshot prints — the role from
/// HTML-AAM's table under an author <c>role</c>, the name from accname 1.2, the hidden verdict from
/// <see cref="ElementVisibility"/> — so an element a snapshot prints as <c>button "Next"</c> is the element a
/// query for that role and name finds. This record is the syntax-free half; <c>Runtime/ElementLocator</c>
/// parses a target into one.
/// </para>
/// <para>
/// A state criterion that is <see langword="null"/> is not asked. One that is asked of an element whose role
/// does not have that state does not match, which is what Playwright's role selector does with
/// <c>[checked]</c> on a link.
/// </para>
/// </remarks>
internal sealed record AxRoleQuery
{
    /// <summary>The computed role to match, or <see langword="null"/> for any role.</summary>
    internal string? Role { get; init; }

    /// <summary>The accessible name to match, already whitespace-flattened, or <see langword="null"/>.</summary>
    internal string? Name { get; init; }

    /// <summary>
    /// Whether <see cref="Name"/> must equal the accessible name, case-sensitively; otherwise it is a
    /// case-insensitive substring of it.
    /// </summary>
    internal bool NameExact { get; init; }

    /// <summary><c>true</c>, <c>false</c> or <c>mixed</c>, or <see langword="null"/> when not asked.</summary>
    internal string? Checked { get; init; }

    /// <summary><c>true</c>, <c>false</c> or <c>mixed</c>, or <see langword="null"/> when not asked.</summary>
    internal string? Pressed { get; init; }

    /// <summary>The <c>disabled</c> state to match, or <see langword="null"/> when not asked.</summary>
    internal bool? Disabled { get; init; }

    /// <summary>The <c>expanded</c> state to match, or <see langword="null"/> when not asked.</summary>
    internal bool? Expanded { get; init; }

    /// <summary>The <c>selected</c> state to match, or <see langword="null"/> when not asked.</summary>
    internal bool? Selected { get; init; }

    /// <summary>The <c>level</c> to match, or <see langword="null"/> when not asked.</summary>
    internal int? Level { get; init; }

    /// <summary>Whether elements a snapshot leaves out as hidden are matched too.</summary>
    internal bool IncludeHidden { get; init; }
}
