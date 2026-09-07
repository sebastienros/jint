using AngleSharp;
using AngleSharp.Scripting;
using Jint;

namespace BindingComparison;

internal sealed class BindingSession : IDisposable
{
    internal const string Arm = "AngleSharp.Js reflection";
    private readonly IBrowsingContext _context;
    internal Engine Engine { get; }
    private BindingSession()
    {
        var scripting = new JsScriptingService();
        _context = BrowsingContext.New(Configuration.Default.With(scripting));
        var document = _context.OpenAsync(request => request.Content(Workloads.Html)).GetAwaiter().GetResult();
        Engine = scripting.GetOrCreateJint(document);
    }
    internal void Validate() { }
    internal static BindingSession Create() => new();
    public void Dispose()
    {
        Engine.Dispose();
        _context.Dispose();
    }
}
