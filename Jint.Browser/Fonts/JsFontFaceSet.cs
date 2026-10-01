using Jint.Browser.Events;
using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Streams;

namespace Jint.Browser.Fonts;

/// <summary>
/// A <c>FontFaceSet</c> — https://drafts.csswg.org/css-font-loading/#fontfaceset — the set a document's
/// <c>fonts</c> answers.
/// </summary>
/// <remarks>
/// <para>
/// The set entries are a <see cref="JsSet"/>, so the setlike members and iteration are ECMAScript's own
/// insertion-ordered set. <c>[[LoadingFonts]]</c>, <c>[[LoadedFonts]]</c> and <c>[[FailedFonts]]</c> drive
/// <c>status</c>, <c>ready</c> and the three load events exactly as the specification's "switch the
/// FontFaceSet to loading/loaded" steps do.
/// </para>
/// <para>
/// A set is never "pending on the environment": with no layout there is no pending layout operation that
/// might request a font, so <c>ready</c> is fulfilled whenever no added face is loading. That also means
/// <c>ready</c> does not wait for the document itself to finish loading.
/// </para>
/// </remarks>
internal sealed class JsFontFaceSet : JsEventTarget
{
    private readonly JsSet _entries;
    private readonly List<JsFontFace> _loadingFonts = [];
    private readonly List<JsFontFace> _loadedFonts = [];
    private readonly List<JsFontFace> _failedFonts = [];
    private PromiseCapability? _ready;
    private bool _readyFulfilled;

    internal JsFontFaceSet(FontRealm owner) : base(owner.Engine, owner.Realm)
    {
        Owner = owner;
        _prototype = owner.SetPrototype;
        _entries = new JsSet(owner.Engine);
    }

    internal FontRealm Owner { get; }

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontfaceset-status</summary>
    internal string Status { get; private set; } = "loaded";

    internal int Size => _entries.Size;

    /// <summary>
    /// https://drafts.csswg.org/css-font-loading/#dom-fontfaceset-ready — created on first read, fulfilled
    /// with the set when nothing is loading, and replaced only once fulfilled.
    /// </summary>
    internal JsPromise Ready
    {
        get
        {
            if (_ready is null)
            {
                _ready = StreamPromises.NewPromise(Engine, Owner.Realm);
                if (string.Equals(Status, "loaded", StringComparison.Ordinal))
                {
                    _ready.Resolve(this);
                    _readyFulfilled = true;
                }
            }

            return StreamPromises.PromiseOf(_ready);
        }
    }

    internal bool Has(JsFontFace face) => _entries.Has(face);

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontfaceset-add</summary>
    internal JsFontFaceSet Add(JsFontFace face)
    {
        if (_entries.Has(face))
        {
            return this;
        }

        _entries.Add(face);
        face.JoinSet(this);
        if (string.Equals(face.Status, "loading", StringComparison.Ordinal))
        {
            FaceLoading(face);
        }

        return this;
    }

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontfaceset-delete</summary>
    internal bool Delete(JsFontFace face)
    {
        var deleted = _entries.Delete(face);
        face.LeaveSet(this);
        _loadedFonts.Remove(face);
        _failedFonts.Remove(face);
        if (_loadingFonts.Remove(face) && _loadingFonts.Count == 0)
        {
            SwitchToLoaded();
        }

        return deleted;
    }

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontfaceset-clear</summary>
    internal void Clear()
    {
        foreach (var value in _entries)
        {
            ((JsFontFace) value).LeaveSet(this);
        }

        _entries.Clear();
        _loadedFonts.Clear();
        _failedFonts.Clear();
        if (_loadingFonts.Count != 0)
        {
            _loadingFonts.Clear();
            SwitchToLoaded();
        }
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-forEach — the callback receives each value twice and this set, not
    /// the backing <see cref="JsSet"/>; entries added during the walk are visited, as <c>Set.prototype.forEach</c>
    /// visits them.
    /// </summary>
    internal void ForEach(ICallable callback, JsValue thisArgument)
    {
        var cursor = default(KeyedCollectionCursor);
        var iterations = 0;
        int slot;
        while ((slot = _entries._data.Next(ref cursor)) >= 0)
        {
            // A CLR callback runs no statements, so the walk bounds itself as Set.prototype.forEach does.
            if (++iterations % Jint.Engine.ConstraintCheckInterval == 0)
            {
                Engine.Constraints.Check();
            }

            var value = _entries._data.KeyAt(slot)!;
            callback.Call(thisArgument, [value, value, this]);
        }
    }

    internal JsValue Values() => _entries.Values();

    internal JsValue Entries() => _entries.Entries();

    /// <summary>
    /// https://drafts.csswg.org/css-font-loading/#dom-fontfaceset-check — true when rendering
    /// <paramref name="text"/> with <paramref name="font"/> would use no face that is not loaded yet.
    /// </summary>
    internal bool Check(string font, string text)
    {
        var matched = Match(font, text);
        if (matched is null)
        {
            DomFailures.Refuse(Owner.Dom, "FontFaceSet.check", DomExceptionNames.Syntax, "Could not resolve '" + font + "' as a font.");
        }

        foreach (var face in matched)
        {
            if (!string.Equals(face.Status, "loaded", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// https://drafts.csswg.org/css-font-loading/#dom-fontfaceset-load — loads every matching face and
    /// fulfils with them, in order, once all have loaded.
    /// </summary>
    internal JsValue Load(string font, string text)
    {
        var engine = Engine;
        var realm = Owner.Realm;
        var result = StreamPromises.NewPromise(engine, realm);
        var matched = Match(font, text);
        if (matched is null)
        {
            result.Reject(realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.Syntax, "Failed to execute 'load' on 'FontFaceSet': Could not resolve '" + font + "' as a font."));
            return StreamPromises.PromiseOf(result);
        }

        engine.Tasks.Post(() =>
        {
            var promises = new JsPromise[matched.Count];
            for (var i = 0; i < matched.Count; i++)
            {
                _ = matched[i].Load();
                promises[i] = matched[i].Loaded;
            }

            // "Waiting for all": fulfilled with every face in order, rejected with the first rejection.
            var values = new JsValue[promises.Length];
            var remaining = promises.Length;
            if (remaining == 0)
            {
                result.Resolve(realm.Intrinsics.Array.ConstructFast(values));
                return;
            }

            for (var i = 0; i < promises.Length; i++)
            {
                var index = i;
                StreamPromises.UponPromise(
                    engine,
                    promises[i],
                    value =>
                    {
                        values[index] = value;
                        if (--remaining == 0)
                        {
                            result.Resolve(realm.Intrinsics.Array.ConstructFast(values));
                        }
                    },
                    reason => result.Reject(reason));
            }
        });

        return StreamPromises.PromiseOf(result);
    }

    /// <summary>A face in this set changed its status to <c>loading</c>.</summary>
    internal void FaceLoading(JsFontFace face)
    {
        if (_loadingFonts.Count == 0)
        {
            SwitchToLoading();
        }

        _loadingFonts.Add(face);
    }

    /// <summary>A face in this set finished loading, successfully or not.</summary>
    internal void FaceSettled(JsFontFace face, bool loaded)
    {
        (loaded ? _loadedFonts : _failedFonts).Add(face);
        if (_loadingFonts.Remove(face) && _loadingFonts.Count == 0)
        {
            SwitchToLoaded();
        }
    }

    /// <summary>https://drafts.csswg.org/css-font-loading/#switch-the-fontfaceset-to-loading</summary>
    private void SwitchToLoading()
    {
        Status = "loading";
        if (_readyFulfilled)
        {
            _ready = null;
            _readyFulfilled = false;
        }

        Engine.Tasks.Post(() => Fire("loading", []));
    }

    /// <summary>https://drafts.csswg.org/css-font-loading/#switch-the-fontfaceset-to-loaded</summary>
    private void SwitchToLoaded()
    {
        Status = "loaded";
        if (_ready is not null && !_readyFulfilled)
        {
            _ready.Resolve(this);
            _readyFulfilled = true;
        }

        Engine.Tasks.Post(() =>
        {
            JsFontFace[] loaded = [.. _loadedFonts];
            JsFontFace[] failed = [.. _failedFonts];
            _loadedFonts.Clear();
            _failedFonts.Clear();
            Fire("loadingdone", loaded);
            if (failed.Length != 0)
            {
                Fire("loadingerror", failed);
            }
        });
    }

    /// <summary>
    /// https://drafts.csswg.org/css-font-loading/#fire-a-font-load-event — the faces filtered to those still
    /// in this set.
    /// </summary>
    private void Fire(string type, JsFontFace[] faces)
    {
        var contained = new List<JsValue>(faces.Length);
        foreach (var face in faces)
        {
            if (_entries.Has(face))
            {
                contained.Add(face);
            }
        }

        DispatchEvent(JsFontFaceSetLoadEvent.CreateTrusted(Owner.Dom, type, [.. contained]));
    }

    /// <summary>
    /// https://drafts.csswg.org/css-font-loading/#find-the-matching-font-faces, or <see langword="null"/> for
    /// the syntax error. A face matches a family by name alone — style, weight and width do not narrow the
    /// match — and is kept only when its <c>unicode-range</c> covers a code point of <paramref name="text"/>.
    /// </summary>
    private List<JsFontFace>? Match(string font, string text)
    {
        var work = new CssValueWork(Owner.Dom.CancellationToken);
        if (!CssFontFaceValues.TryParseFontFamilies(font, work, out var families))
        {
            return null;
        }

        var matched = new List<JsFontFace>();
        foreach (var family in families)
        {
            foreach (var value in _entries)
            {
                Engine.Constraints.Check();
                var face = (JsFontFace) value;
                if (!matched.Contains(face) && FamilyMatches(face.Family, family) && Covers(face, text, work))
                {
                    matched.Add(face);
                }
            }
        }

        return matched;
    }

    private static bool FamilyMatches(string faceFamily, string family)
    {
        var name = faceFamily.AsSpan().Trim();
        if (name.Length >= 2 && name[0] is '"' or '\'' && name[^1] == name[0])
        {
            name = name[1..^1];
        }

        return CssAscii.EqualsIgnoreCase(name.ToString(), family);
    }

    private static bool Covers(JsFontFace face, string text, CssValueWork work)
    {
        if (!CssFontFaceValues.TryParseUnicodeRange(face.Descriptor(CssFontFaceDescriptor.UnicodeRange), work, out var ranges))
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            int codePoint = text[i];
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
            }

            foreach (var (start, end) in ranges)
            {
                if (codePoint >= start && codePoint <= end)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override string ToString() => "[object FontFaceSet]";
}
