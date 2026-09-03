using System.Diagnostics.CodeAnalysis;

namespace Jint.Browser.Tool;

/// <summary>Whether an option carries a value, and whether it may be given more than once.</summary>
internal enum OptionKind
{
    /// <summary>A switch: <c>--untrusted</c>.</summary>
    Flag,

    /// <summary>One value: <c>--port 9222</c> or <c>--port=9222</c>. A second occurrence replaces the first.</summary>
    Value,

    /// <summary>A value that accumulates: <c>--header a:b --header c:d</c>.</summary>
    Repeated,
}

/// <summary>
/// The command line, parsed against the options one command declares.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hand-rolled, and that is a decision.</b> A <c>dotnet tool</c> is downloaded and restored by everyone
/// who runs it, so every dependency is one more thing between a user and a page; the whole grammar here is
/// four commands and about twenty options, none of them positional beyond the first two, and none of them
/// needing completion, help generation or subcommand trees. <c>Jint.Repl</c> parses its own arguments for
/// the same reason. If this grows a third level of subcommand, that is the moment to take
/// <c>System.CommandLine</c> rather than to grow this.
/// </para>
/// <para>
/// <b>An unknown option is an error, never a positional argument.</b> A typo in <c>--main-content</c> that
/// silently became the URL to fetch would be the worst failure this program has, so anything starting with
/// <c>-</c> that no command declared stops the run with a usage error. <c>--</c> ends the options, which is
/// what lets <c>jint-browser eval &lt;url&gt; -- -1 + 2</c> pass an expression that starts with a dash.
/// </para>
/// </remarks>
internal sealed class CommandLine
{
    private readonly Dictionary<string, List<string>> _values;
    private readonly HashSet<string> _flags;

    private CommandLine(List<string> positional, Dictionary<string, List<string>> values, HashSet<string> flags)
    {
        Positional = positional;
        _values = values;
        _flags = flags;
    }

    /// <summary>Everything that was not an option, in the order it was given.</summary>
    internal IReadOnlyList<string> Positional { get; }

    /// <summary>Whether a flag was given.</summary>
    internal bool Flag(string name) => _flags.Contains(name);

    /// <summary>The last value given for an option, or <see langword="null"/>.</summary>
    internal string? Value(string name) => _values.TryGetValue(name, out var values) ? values[^1] : null;

    /// <summary>Every value given for a repeated option, in order.</summary>
    internal IReadOnlyList<string> Values(string name)
        => _values.TryGetValue(name, out var values) ? values : [];

    /// <summary>Parses <paramref name="arguments"/> against the options <paramref name="syntax"/> declares.</summary>
    /// <param name="arguments">The arguments after the command name.</param>
    /// <param name="syntax">Every option the command accepts, without its leading dashes.</param>
    /// <param name="parsed">The parse, when it succeeded.</param>
    /// <param name="error">What was wrong with the command line, when it did not.</param>
    internal static bool TryParse(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, OptionKind> syntax,
        [NotNullWhen(true)] out CommandLine? parsed,
        [NotNullWhen(false)] out string? error)
    {
        var positional = new List<string>();
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var optionsEnded = false;

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            if (optionsEnded || argument.Length == 0 || argument[0] != '-')
            {
                positional.Add(argument);
                continue;
            }

            if (argument == "--")
            {
                optionsEnded = true;
                continue;
            }

            var name = argument.TrimStart('-');
            string? inlineValue = null;

            var equals = name.IndexOf('=', StringComparison.Ordinal);
            if (equals >= 0)
            {
                inlineValue = name[(equals + 1)..];
                name = name[..equals];
            }

            if (!syntax.TryGetValue(name, out var kind))
            {
                parsed = null;
                error = $"unknown option '{argument}'";
                return false;
            }

            if (kind == OptionKind.Flag)
            {
                if (inlineValue is not null)
                {
                    parsed = null;
                    error = $"'--{name}' is a switch and takes no value";
                    return false;
                }

                flags.Add(name);
                continue;
            }

            var value = inlineValue;
            if (value is null)
            {
                if (i + 1 >= arguments.Count)
                {
                    parsed = null;
                    error = $"'--{name}' needs a value";
                    return false;
                }

                value = arguments[++i];
            }

            if (!values.TryGetValue(name, out var list))
            {
                list = [];
                values[name] = list;
            }
            else if (kind == OptionKind.Value)
            {
                list.Clear();
            }

            list.Add(value);
        }

        parsed = new CommandLine(positional, values, flags);
        error = null;
        return true;
    }
}
