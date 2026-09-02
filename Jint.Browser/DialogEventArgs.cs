namespace Jint.Browser;

/// <summary>
/// A page called <c>alert</c>, <c>confirm</c> or <c>prompt</c>, and is blocked until the handler answers.
/// </summary>
/// <remarks>
/// <para>
/// The handler runs on the page loop, inside the script that opened the dialog, so it must not call back into
/// the page and must not block: a <see cref="Page"/> method awaited from here deadlocks the loop it is
/// waiting on. Set <see cref="Accepted"/> and, for a prompt, <see cref="PromptText"/>, and return.
/// </para>
/// <para>
/// With no handler attached the dialog is dismissed: <c>alert</c> returns, <c>confirm</c> answers
/// <see langword="false"/> and <c>prompt</c> answers <c>null</c>, which is what a headless browser with
/// nobody at the keyboard does.
/// </para>
/// </remarks>
public sealed class DialogEventArgs : EventArgs
{
    internal DialogEventArgs(DialogKind kind, string message, string defaultPromptText)
    {
        Kind = kind;
        Message = message;
        DefaultPromptText = defaultPromptText;
        PromptText = defaultPromptText;
    }

    /// <summary>Which of the three functions the page called.</summary>
    public DialogKind Kind { get; }

    /// <summary>The message the page passed, coerced to a string.</summary>
    public string Message { get; }

    /// <summary>The second argument to <c>prompt</c>, and the empty string for the other two.</summary>
    public string DefaultPromptText { get; }

    /// <summary>Whether the dialog is accepted; <see langword="false"/> — dismissed — unless a handler sets it.</summary>
    public bool Accepted { get; set; }

    /// <summary>What an accepted <c>prompt</c> answers; ignored by the other two.</summary>
    public string PromptText { get; set; }
}

/// <summary>Which dialog function a page called.</summary>
public enum DialogKind
{
    /// <summary><c>window.alert</c>.</summary>
    Alert,

    /// <summary><c>window.confirm</c>.</summary>
    Confirm,

    /// <summary><c>window.prompt</c>.</summary>
    Prompt,
}
