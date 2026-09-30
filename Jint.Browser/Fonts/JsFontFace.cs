using System.Runtime.ExceptionServices;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Browser.Runtime.Parsing;
using Jint.HtmlParser.Css.Values;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Encoding;
using Jint.WebApi.Streams;

namespace Jint.Browser.Fonts;

/// <summary>
/// A <c>FontFace</c> — https://drafts.csswg.org/css-font-loading/#fontface-interface.
/// </summary>
/// <remarks>
/// Exactly one of <c>[[Urls]]</c> (<see cref="_urls"/>) and <c>[[Data]]</c> (<see cref="_data"/>) is set, by
/// the constructor. A URL face loads through the page's resource owner on <c>load()</c>; a buffer face
/// "parses" its bytes in a queued task. Either way the result is decided by the file signature alone, since
/// nothing here shapes text with it.
/// </remarks>
internal sealed class JsFontFace : ObjectInstance
{
    /// <summary>https://drafts.csswg.org/css-font-loading/#dictdef-fontfacedescriptors — indexed by <see cref="CssFontFaceDescriptor"/>.</summary>
    private static readonly string[] _defaults =
    [
        "normal", "normal", "normal", "U+0-10FFFF", "normal", "normal", "normal", "auto", "normal", "normal", "normal", "100%",
    ];

    /// <summary>
    /// The dictionary's members, in the lexicographic order WebIDL converts them in. <c>width</c> and
    /// <c>stretch</c> set the same descriptor, the former winning when both are present.
    /// </summary>
    private static readonly (JsString Name, CssFontFaceDescriptor Descriptor)[] _members =
    [
        (new JsString("ascentOverride"), CssFontFaceDescriptor.AscentOverride),
        (new JsString("descentOverride"), CssFontFaceDescriptor.DescentOverride),
        (new JsString("display"), CssFontFaceDescriptor.Display),
        (new JsString("featureSettings"), CssFontFaceDescriptor.FeatureSettings),
        (new JsString("lineGapOverride"), CssFontFaceDescriptor.LineGapOverride),
        (new JsString("sizeAdjust"), CssFontFaceDescriptor.SizeAdjust),
        (new JsString("stretch"), CssFontFaceDescriptor.Stretch),
        (new JsString("style"), CssFontFaceDescriptor.Style),
        (new JsString("unicodeRange"), CssFontFaceDescriptor.UnicodeRange),
        (new JsString("variant"), CssFontFaceDescriptor.Variant),
        (new JsString("variationSettings"), CssFontFaceDescriptor.VariationSettings),
        (new JsString("weight"), CssFontFaceDescriptor.Weight),
        (new JsString("width"), CssFontFaceDescriptor.Stretch),
    ];

    private readonly string[] _descriptors;
    private readonly CssFontSource[]? _urls;
    private readonly byte[]? _data;
    private readonly PromiseCapability _fontStatus;
    private List<JsFontFaceSet>? _sets;

    private JsFontFace(FontRealm owner, string family, string[] descriptors, CssFontSource[]? urls, byte[]? data) : base(owner.Engine)
    {
        Owner = owner;
        _prototype = owner.FacePrototype;
        Family = family;
        _descriptors = descriptors;
        _urls = urls;
        _data = data;
        _fontStatus = StreamPromises.NewPromise(owner.Engine, owner.Realm);
    }

    internal FontRealm Owner { get; }

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontface-family</summary>
    internal string Family { get; set; }

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontface-status</summary>
    internal string Status { get; private set; } = "unloaded";

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontface-loaded — <c>[[FontStatusPromise]]</c>.</summary>
    internal JsPromise Loaded => StreamPromises.PromiseOf(_fontStatus);

    internal string Descriptor(CssFontFaceDescriptor descriptor) => _descriptors[(int) descriptor];

    /// <summary>
    /// https://drafts.csswg.org/css-font-loading/#font-face-constructor. Every argument is converted before
    /// anything is parsed, as WebIDL converts them, and a value that does not parse rejects
    /// <c>loaded</c> rather than throwing.
    /// </summary>
    /// <remarks>
    /// The <c>family</c> argument is kept as given rather than parsed as a <c>&lt;family-name&gt;</c>, which is
    /// what Chrome, Firefox and Safari all do: <c>new FontFace("Foo Bar", …).family</c> is <c>"Foo Bar"</c>
    /// and a quoted name keeps its quotes.
    /// </remarks>
    internal static JsFontFace Construct(FontRealm owner, JsValue[] args)
    {
        var realm = owner.Realm;
        var family = TypeConverter.ToString(args.At(0));
        var source = args.At(1);
        byte[]? data = null;
        string? sourceText = null;
        if (BufferSource.TryGetBytes(source, out var bytes))
        {
            data = bytes.ToArray();
        }
        else
        {
            sourceText = TypeConverter.ToString(source);
        }

        var given = new string?[_defaults.Length];
        var init = args.At(2);
        if (!init.IsNullOrUndefined())
        {
            if (init is not ObjectInstance dictionary)
            {
                Throw.TypeError(realm, "Failed to construct 'FontFace': The provided value is not of type 'FontFaceDescriptors'.");
                return null!;
            }

            foreach (var (name, descriptor) in _members)
            {
                var value = dictionary.Get(name);
                if (!value.IsUndefined())
                {
                    given[(int) descriptor] = TypeConverter.ToString(value);
                }
            }
        }

        var work = new CssValueWork(owner.Dom.CancellationToken);
        var descriptors = new string[_defaults.Length];
        var valid = true;
        for (var i = 0; i < descriptors.Length && valid; i++)
        {
            if (given[i] is not { } text)
            {
                descriptors[i] = _defaults[i];
            }
            else if (CssFontFaceValues.TryParseDescriptor((CssFontFaceDescriptor) i, text, work, out var serialization))
            {
                descriptors[i] = serialization;
            }
            else
            {
                valid = false;
            }
        }

        CssFontSource[]? urls = null;
        if (valid && sourceText is not null && !CssFontFaceValues.TryParseSource(sourceText, work, out urls))
        {
            valid = false;
        }

        if (!valid)
        {
            Array.Fill(descriptors, "");
            var failed = new JsFontFace(owner, family, descriptors, sourceText is null ? null : [], data);
            failed.Status = "error";
            failed._fontStatus.Reject(failed.Exception(DomExceptionNames.Syntax, "Failed to construct 'FontFace': A descriptor or the source could not be parsed."));
            return failed;
        }

        var face = new JsFontFace(owner, family, descriptors, urls, data);
        if (data is not null)
        {
            // Step 4: "queue a task to set font face's status to loading", then parse the bytes
            // asynchronously and settle in a further task.
            owner.Engine.Tasks.Post(() =>
            {
                face.BeginLoading();
                owner.Engine.Tasks.Post(() =>
                {
                    if (IsFontFile(face._data!))
                    {
                        face.Settle(null, null);
                    }
                    else
                    {
                        face.Settle(DomExceptionNames.Syntax, "The font data could not be parsed.");
                    }
                });
            });
        }

        return face;
    }

    /// <summary>
    /// A descriptor's setter: https://drafts.csswg.org/css-font-loading/#dom-fontface-style and its
    /// siblings — parse, and throw a <c>SyntaxError</c> when the value does not match the grammar.
    /// </summary>
    internal void SetDescriptor(CssFontFaceDescriptor descriptor, string member, JsValue value)
    {
        var text = TypeConverter.ToString(value);
        if (!CssFontFaceValues.TryParseDescriptor(descriptor, text, new CssValueWork(Owner.Dom.CancellationToken), out var serialization))
        {
            DomFailures.Refuse(Owner.Dom, "FontFace." + member, DomExceptionNames.Syntax, "'" + text + "' is not a valid value.");
        }

        _descriptors[(int) descriptor] = serialization;
    }

    /// <summary>https://drafts.csswg.org/css-font-loading/#font-face-load</summary>
    internal JsValue Load()
    {
        if (_urls is null || !string.Equals(Status, "unloaded", StringComparison.Ordinal))
        {
            return Loaded;
        }

        BeginLoading();
        Fetch(0);
        return Loaded;
    }

    internal void JoinSet(JsFontFaceSet set) => (_sets ??= []).Add(set);

    internal void LeaveSet(JsFontFaceSet set) => _sets?.Remove(set);

    /// <summary>
    /// "Attempt to load a font as defined in CSS Fonts, as if it was the value of a @font-face rule's src
    /// descriptor" — each <c>url()</c> in order until one answers with a font file. A <c>local()</c> source
    /// names an installed font, and this browser has none, so it is skipped as an unavailable one is.
    /// </summary>
    private void Fetch(int index)
    {
        var urls = _urls!;
        while (index < urls.Length && urls[index].IsLocal)
        {
            index++;
        }

        var document = Owner.Dom.Document;
        var parser = PageRuntime.FindBrowsingContext(Engine, document)?.Parser;
        if (index >= urls.Length || document is null || parser is null)
        {
            Engine.Tasks.Post(() => Settle(DomExceptionNames.Network, "The font could not be loaded."));
            return;
        }

        Task<MediaResourceResponse> request;
        try
        {
            // Resolution, constraint checks and request setup happen here, on the page loop.
            request = parser.RequestFontAsync(document, urls[index].Value, CancellationToken.None);
        }
        catch (MediaSourceException)
        {
            Fetch(index + 1);
            return;
        }

        var next = index + 1;
        _ = AwaitFont(request, parser, (response, error) =>
        {
            if (error is not null and not (SubresourceFetchException or OperationCanceledException))
            {
                // A host failure stays a failure of the owning page task, never a fake network error.
                ExceptionDispatchInfo.Capture(error).Throw();
            }

            if (error is null && IsFontFile(response.Bytes))
            {
                Settle(null, null);
            }
            else
            {
                Fetch(next);
            }
        });
    }

    private static async Task AwaitFont(Task<MediaResourceResponse> request, ParserDriver parser,
        Action<MediaResourceResponse, Exception?> complete)
    {
        MediaResourceResponse response = default;
        Exception? error = null;
        try
        {
            response = await request.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception;
        }

        // The completion reads and writes engine state, so it runs on the page loop or not at all.
        parser.TryPostResourceCompletion(() => complete(response, error));
    }

    private void BeginLoading()
    {
        Status = "loading";
        if (_sets is { } sets)
        {
            foreach (var set in sets.ToArray())
            {
                set.FaceLoading(this);
            }
        }
    }

    /// <summary>The last steps of both loads: settle <c>[[FontStatusPromise]]</c>, then tell every set.</summary>
    private void Settle(string? errorName, string? message)
    {
        if (!string.Equals(Status, "loading", StringComparison.Ordinal))
        {
            return;
        }

        if (errorName is null)
        {
            _fontStatus.Resolve(this);
            Status = "loaded";
        }
        else
        {
            _fontStatus.Reject(Exception(errorName, "Failed to load 'FontFace': " + message));
            Status = "error";
        }

        if (_sets is { } sets)
        {
            foreach (var set in sets.ToArray())
            {
                set.FaceSettled(this, errorName is null);
            }
        }
    }

    private Jint.WebApi.DomException.JsDomException Exception(string name, string message) => Owner.Realm.Intrinsics.DomException.CreateException(name, message);

    /// <summary>
    /// Whether <paramref name="bytes"/> open with the signature of a font file a browser would parse:
    /// TrueType (<c>0x00010000</c>, <c>true</c>, <c>typ1</c>), OpenType CFF (<c>OTTO</c>), a collection
    /// (<c>ttcf</c>), or WOFF / WOFF2. The tables behind it are not validated.
    /// </summary>
    internal static bool IsFontFile(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
        {
            return false;
        }

        var tag = (uint) (bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
        return tag is 0x00010000u
            or 0x74727565u // "true"
            or 0x74797031u // "typ1"
            or 0x4F54544Fu // "OTTO"
            or 0x74746366u // "ttcf"
            or 0x774F4646u // "wOFF"
            or 0x774F4632u; // "wOF2"
    }

    /// <inheritdoc />
    public override string ToString() => "[object FontFace]";
}
