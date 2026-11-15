using System.Globalization;
using System.Text.Json;
using Jint.Browser.Runtime.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// The scripts that run in every new document before the document's own, in the order they were added.
/// </summary>
/// <remarks>
/// <para>
/// <b>One store behind three callers.</b> <see cref="Page.AddInitScriptAsync(string)"/> and CDP's
/// <c>Page.addScriptToEvaluateOnNewDocument</c> share the page's instance, so an in-process caller and a
/// protocol client install into one ordered list; <see cref="BrowserContext.AddInitScriptAsync(string)"/>
/// has its own, run first for every page of the context. Each entry remembers who added it, and only that
/// owner can remove it: a client's <c>removeScriptToEvaluateOnNewDocument</c> cannot remove a host's script.
/// </para>
/// <para>
/// <b>They run from <see cref="ParserDriver"/>, once the empty document exists and before the parser reads a
/// byte</b>, whether or not a client is attached, and each runs as an inline classic script does: a turn of
/// its own, an error reported and not fatal to the load — Chrome does not stop a document for its client's
/// instrumentation either.
/// </para>
/// </remarks>
internal sealed class NewDocumentScripts
{
    private readonly object _gate = new();
    private readonly List<Entry> _scripts = [];

    private int _next;

    /// <summary>Adds a script and answers the identifier its owner removes it by.</summary>
    /// <param name="owner">Who added it, and the only caller <see cref="Remove"/> removes it for.</param>
    /// <param name="origin">What a page error names the script by, followed by <c>#</c> and its identifier.</param>
    /// <param name="source">The script.</param>
    /// <param name="argument">
    /// When present, <paramref name="source"/> is a function expression invoked with this JSON-data argument,
    /// bound the way <c>Page.EvaluateAsync(function, argument)</c> binds one.
    /// </param>
    internal string Add(object owner, string origin, string source, JsonElement? argument = null)
    {
        lock (_gate)
        {
            var id = (++_next).ToString(CultureInfo.InvariantCulture);
            _scripts.Add(new Entry(owner, id, origin + "#" + id, source, argument));
            return id;
        }
    }

    /// <summary>Removes the script <paramref name="owner"/> added under <paramref name="identifier"/>, answering whether there was one.</summary>
    internal bool Remove(object owner, string identifier)
    {
        lock (_gate)
        {
            for (var i = 0; i < _scripts.Count; i++)
            {
                if (ReferenceEquals(_scripts[i].Owner, owner) && string.Equals(_scripts[i].Id, identifier, StringComparison.Ordinal))
                {
                    _scripts.RemoveAt(i);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Runs every script into the document <paramref name="driver"/> is about to parse, in the order they were added.</summary>
    internal void Run(ParserDriver driver)
    {
        Entry[] scripts;
        lock (_gate)
        {
            if (_scripts.Count == 0)
            {
                return;
            }

            scripts = [.. _scripts];
        }

        foreach (var script in scripts)
        {
            driver.RunInitScript(engine =>
            {
                if (script.Argument is { } argument)
                {
                    Page.InvokeWithArgument(engine, script.Source, argument, script.Name);
                }
                else
                {
                    engine.Execute(script.Source, script.Name);
                }
            }, script.Name);
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct Entry(object Owner, string Id, string Name, string Source, JsonElement? Argument);
}
