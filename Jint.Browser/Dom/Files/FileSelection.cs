using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.WebApi.Files;

namespace Jint.Browser.Dom.Files;

/// <summary>
/// One file on its way into an <c>&lt;input type=file&gt;</c>: the bytes, and the three things a
/// <c>File</c> is built from.
/// </summary>
/// <param name="Name">The file's name, which is what a submission and <c>file.name</c> report.</param>
/// <param name="Type">The MIME type, or <see cref="FileSelection.DefaultType"/> when nothing named one.</param>
/// <param name="Content">The bytes, already read: a selection holds a file rather than a path to one.</param>
/// <param name="LastModified">Milliseconds since the Unix epoch.</param>
internal readonly record struct SelectedFile(
    string Name,
    string Type,
    ReadOnlyMemory<byte> Content,
    long LastModified);

/// <summary>
/// HTML's File Upload state: what a host, a protocol client or an adapter selects into a file input, and
/// the two events the page hears when the selection moves.
/// <para>
/// https://html.spec.whatwg.org/multipage/input.html#file-upload-state-(type=file)
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no picker, and this is the seam that stands in for one.</b> Clicking a file input runs
/// <c>BrowserActivationHost.OpenFileChooser</c>, which a headless host answers by recording that a page
/// asked for something nobody could give it. Everything that <i>can</i> answer —
/// <c>DOM.setFileInputFiles</c>, <c>Page.SetInputFilesAsync</c>, the Playwright adapter's
/// <c>SetInputFilesAsync</c> — comes through here, so one selection algorithm serves all three rather than
/// three that drift.
/// </para>
/// <para>
/// <b>The selection is the same state a script sets through <c>input.files</c>.</b>
/// <see cref="FileTransferRealm"/> owns it, mirrors it into AngleSharp's own input model so
/// <c>input.value</c> and constraint validation agree with the <c>FileList</c>, and
/// <c>Runtime/FormSubmitter</c> reads it when it builds an entry list. Nothing here keeps a second copy.
/// </para>
/// </remarks>
internal static class FileSelection
{
    /// <summary>
    /// The type a file gets when nothing named one — the value HTML's own "empty file" entry carries, and
    /// what a <c>multipart/form-data</c> part is labelled with when no type could be worked out.
    /// </summary>
    internal const string DefaultType = "application/octet-stream";

    /// <summary>Whether <paramref name="node"/> is an <c>input</c> element in the File Upload state.</summary>
    internal static bool IsFileInput(INode? node)
        => node is IHtmlInputElement input && ActivationBehaviors.IsType(input, "file");

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#update-the-file-selection — replace the element's
    /// selected files and let the page hear it.
    /// </summary>
    /// <param name="runtime">The page the input belongs to. The caller is already on its loop.</param>
    /// <param name="input">The file input whose selection is being made.</param>
    /// <param name="files">The files chosen, in the order they were chosen.</param>
    /// <remarks>
    /// <para>
    /// <b>The events are fired rather than queued, and the reason is the thread.</b> HTML queues an element
    /// task because a picker is asynchronous user interface that finishes on some later turn; here the
    /// caller <i>is</i> a turn of the page loop — a protocol command, a mailbox request — so this call is
    /// that task. Queueing a second one would only mean a client could be told the command succeeded before
    /// the page had heard about it, which is not what any of them expects.
    /// </para>
    /// <para>
    /// <b>Both events fire whenever a selection is made</b>, even one that leaves the same files in place:
    /// the algorithm has no "did it change" step, and a caller that reached here made a selection. An empty
    /// selection is a selection too, and clears the input — Blink returns early instead, which is the bug
    /// Puppeteer's <c>uploadFile</c> works around by not sending the command at all for an empty list.
    /// </para>
    /// </remarks>
    internal static void Update(PageRuntime runtime, IHtmlInputElement input, IReadOnlyList<SelectedFile> files)
    {
        var realm = FileTransferRealm.Of(runtime.Engine);
        var list = realm.NewFileList();

        foreach (var file in Allowed(input, files))
        {
            list.Add(ToFile(runtime.Engine, file));
        }

        realm.SetInputFiles(input, list);

        if (runtime.Dom.WrapNode(input) is { } target)
        {
            ActivationBehaviors.FireInputAndChange(target);
        }
    }

    /// <summary>
    /// The files a selection may actually contain: all of them for a <c>multiple</c> input, the first
    /// otherwise.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/input.html#attr-input-multiple — "if the attribute is not
    /// specified, the user agent must not allow the user to select more than one file". A selection made by
    /// a client rather than by a user is still the user agent's to bound, so the extra files are dropped
    /// rather than the call refused: Blink's <c>FileInputType::SetFilesFromPaths</c> keeps the first path
    /// the same way, and Playwright refuses the call in its own client before one is ever sent.
    /// </remarks>
    internal static IReadOnlyList<T> Allowed<T>(IHtmlInputElement input, IReadOnlyList<T> files)
        => input.IsMultiple || files.Count <= 1 ? files : [files[0]];

    /// <summary>Reads one host file into a selection, in the shape the File API gives it.</summary>
    /// <param name="path">A path on the machine the browser is running on.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <remarks>
    /// <b>The bytes are read now, not when the page asks for them.</b> A <c>File</c> here is a <c>Blob</c>
    /// over memory the engine owns — there is no lazily opened host handle behind it, and there must not be
    /// one, because the page may read it long after the file has changed or gone. The type comes from the
    /// extension, which is the only source a headless browser has; the timestamp is the file system's,
    /// clamped at the epoch the way <see cref="AngleSharpFileAdapter"/> clamps the other end.
    /// </remarks>
    internal static SelectedFile Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Could not find file '" + path + "'.", path);
        }

        var content = File.ReadAllBytes(path);

        return new SelectedFile(info.Name, TypeOf(info.Name), content, Milliseconds(info.LastWriteTimeUtc));
    }

    /// <summary>Milliseconds since the Unix epoch, which is what <c>File.lastModified</c> is.</summary>
    internal static long Milliseconds(DateTime utc)
        => utc <= DateTime.UnixEpoch ? 0 : (long) (utc - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>The MIME type an extension names, or <see cref="DefaultType"/> when it names none.</summary>
    /// <remarks>
    /// <b>AngleSharp's table, plus three registrations it predates.</b> There is no standard mapping from an
    /// extension to a type — a browser asks the platform, which answers differently on each of them — so a
    /// fixed table is what makes this answer the same everywhere, and <c>AngleSharp.Io.MimeTypeNames</c> is
    /// the one already in the dependency set. It answers <see cref="DefaultType"/> for
    /// <c>.json</c>, <c>.csv</c> and <c>.md</c>, which are three of the commonest things a form uploads, so
    /// their IANA registrations are supplied here: RFC 8259, RFC 4180 and RFC 7763. This is a gap in a
    /// convenience table rather than a divergence from a specification, which is why it is filled here
    /// instead of being recorded in <c>Dom/divergences.md</c>.
    /// </remarks>
    internal static string TypeOf(string name)
    {
        var extension = Path.GetExtension(name);
        if (extension.Length == 0)
        {
            return DefaultType;
        }

        var supplement = extension.ToLowerInvariant() switch
        {
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".md" => "text/markdown",
            _ => null,
        };

        if (supplement is not null)
        {
            return supplement;
        }

        var mime = AngleSharp.Io.MimeTypeNames.FromExtension(extension);
        return string.IsNullOrEmpty(mime) ? DefaultType : mime;
    }

    /// <summary>The engine's <c>File</c> for one selected file — the same one <c>new File()</c> builds.</summary>
    private static JsFile ToFile(Engine engine, SelectedFile file)
        => new(engine, file.Content, file.Type, file.Name, file.LastModified)
        {
            _prototype = engine._mainRealm.Intrinsics.File.PrototypeObject,
        };
}
