#nullable enable

using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A <b>writable</b> named record on <see cref="NamedPropertyObject"/> — the shape the two integrators this
/// class was designed against actually have, and the one the read-only first version could not serve.
///
/// <para>
/// RavenDB's <c>BlittableObjectInstance</c> overrides six <see cref="ObjectInstance"/> members by hand
/// (<c>GetOwnProperty</c>, <c>Set</c>, <c>Delete</c>, <c>DefineOwnProperty</c>, <c>GetOwnPropertyKeys</c> and
/// <c>GetOwnProperties</c>) and, to be correct <em>and</em> fast, would need two more it does not have
/// (<c>TryGetOwnPropertyValue</c>, so a read stops costing a descriptor, and <c>ProbeOwnProperty</c>, so an
/// existence question does too). Squidex's <c>ContentDataObject</c> and <c>ContentFieldObject</c> override
/// eight and nine with the same two omissions. Every one of those is an implementation of a specification
/// algorithm that has to agree with the other five, seven or eight.
/// </para>
///
/// <para>
/// <see cref="WritableHostRecord"/> below is the same record expressed as <b>six declarations</b> over the
/// host's own data — nothing that could disagree with anything, because the base class derives the whole
/// property model from them. The defects the hand-rolled form ships are pinned as tests here: measured against
/// the hand-rolled equivalent on unfixed code, one <c>Object.keys</c>, one <c>in</c> and one property read each
/// materialized a <c>PropertyDescriptor</c>, and
/// <c>Object.getOwnPropertySymbols(record)</c> answered <c>1</c> — the string key — because the
/// <c>GetOwnPropertyKeys(Types)</c> override ignored its mask.
/// </para>
/// </summary>
public class HostNamedPropertyWriteTests
{
    private static Engine EngineWith(out WritableHostRecord record)
    {
        var engine = new Engine();
        record = new WritableHostRecord(engine)
            .AddField("title", "Hello")
            .AddField("views", 7)
            .AddComputed("id", "doc/1");

        engine.SetValue("doc", record);
        return engine;
    }

    // -------------------------------------------------------------------------------------------------
    // Writing
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AssigningAWritableNameReachesTheHost()
    {
        var engine = EngineWith(out var record);

        engine.Evaluate("doc.title = 'Changed'").Should().Be("Changed");
        record.Read("title").Should().Be("Changed");
        engine.Evaluate("doc.title").Should().Be("Changed");

        // and through every other spelling of an assignment
        engine.Evaluate("doc['views'] = 9; doc.views").Should().Be(9);
        engine.Evaluate("doc.views += 1; doc.views").Should().Be(10);
        engine.Evaluate("Reflect.set(doc, 'title', 'reflected')").Should().Be(true);
        record.Read("title").Should().Be("reflected");
    }

    [Fact]
    public void AWritableNameReportsWritableTrue()
    {
        var engine = EngineWith(out _);

        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(doc, 'title'))")
            .Should().Be("""{"value":"Hello","writable":true,"enumerable":true,"configurable":true}""");

        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(doc, 'id'))")
            .Should().Be("""{"value":"doc/1","writable":false,"enumerable":true,"configurable":true}""");
    }

    /// <summary>
    /// Writability is declared per name, so a record with computed or read-only fields beside writable ones
    /// stays honest: the read-only one refuses exactly the way an ordinary non-writable data property does —
    /// a silent no-op in sloppy mode, a <c>TypeError</c> in strict mode.
    /// </summary>
    [Fact]
    public void ARefusedWriteIsSilentInSloppyModeAndThrowsInStrictMode()
    {
        var engine = EngineWith(out var record);

        engine.Evaluate("doc.id = 'other'; doc.id").Should().Be("doc/1");
        record.Read("id").Should().Be("doc/1");

        var strict = () => engine.Evaluate("'use strict'; doc.id = 'other';");
        strict.Should().Throw<JavaScriptException>().WithMessage("*read only*");

        // a write the host refuses on its own terms takes the same path
        engine.Evaluate("doc.views = 'not a number'; doc.views").Should().Be(7);
        var refused = () => engine.Evaluate("'use strict'; doc.views = 'not a number';");
        refused.Should().Throw<JavaScriptException>();
    }

    /// <summary>
    /// A name the record does not carry is entirely ordinary until the record says it will take it. Here
    /// <c>IsNameWritable</c> accepts any name in the schema, so an assignment creates the field; anything else
    /// falls through to the prototype chain and then to an ordinary expando, exactly as on a plain object.
    /// </summary>
    [Fact]
    public void AnAssignmentMayCreateAFieldOrStayAnOrdinaryExpando()
    {
        var engine = EngineWith(out var record);

        engine.Evaluate("doc.summary = 'created'");
        record.Read("summary").Should().Be("created");
        engine.Evaluate("Object.keys(doc).join()").Should().Be("title,views,id,summary");

        engine.Evaluate("doc.notInSchema = 'expando'");
        record.Read("notInSchema").Should().BeNull();
        engine.Evaluate("doc.notInSchema").Should().Be("expando");
        engine.Evaluate("Object.keys(doc).join()").Should().Be("title,views,id,summary,notInSchema");
    }

    [Fact]
    public void ReflectSetWithAForeignReceiverDefinesOnTheReceiver()
    {
        var engine = EngineWith(out var record);

        engine.Evaluate("var target = {}; Reflect.set(doc, 'title', 'elsewhere', target)").Should().Be(true);
        record.Read("title").Should().Be("Hello");
        engine.Evaluate("target.title").Should().Be("elsewhere");
    }

    // -------------------------------------------------------------------------------------------------
    // Deleting
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void DeletingAWritableNameReachesTheHost()
    {
        var engine = EngineWith(out var record);

        engine.Evaluate("delete doc.title").Should().Be(true);
        record.Read("title").Should().BeNull();
        engine.Evaluate("'title' in doc").Should().Be(false);
        engine.Evaluate("doc.title").Should().BeUndefined();
        engine.Evaluate("Object.keys(doc).join()").Should().Be("views,id");
    }

    [Fact]
    public void ARefusedDeleteIsFalseInSloppyModeAndThrowsInStrictMode()
    {
        var engine = EngineWith(out var record);

        engine.Evaluate("delete doc.id").Should().Be(false);
        record.Read("id").Should().Be("doc/1");

        var strict = () => engine.Evaluate("'use strict'; delete doc.id;");
        strict.Should().Throw<JavaScriptException>();
    }

    // -------------------------------------------------------------------------------------------------
    // Enumeration
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void EnumerationOrderIsTheOrderTheRecordReportsNames()
    {
        var engine = EngineWith(out var record);
        record.AddField("zzz", "last").AddField("aaa", "after-zzz");

        engine.Evaluate("Object.keys(doc).join()").Should().Be("title,views,id,zzz,aaa");
        engine.Evaluate("Object.getOwnPropertyNames(doc).join()").Should().Be("title,views,id,zzz,aaa");
        engine.Evaluate("var out = []; for (var k in doc) out.push(k); out.join()").Should().Be("title,views,id,zzz,aaa");
        engine.Evaluate("Object.entries(doc).map(function (e) { return e[0]; }).join()").Should().Be("title,views,id,zzz,aaa");
        engine.Evaluate("JSON.stringify({ ...doc })")
            .Should().Be("""{"title":"Hello","views":7,"id":"doc/1","zzz":"last","aaa":"after-zzz"}""");
    }

    /// <summary>
    /// The <c>types</c> mask. A hand-rolled <c>GetOwnPropertyKeys(Types)</c> that ignores its argument — which
    /// is what the integrator source does — answers a symbols-only request with its string keys, so
    /// <c>Object.getOwnPropertySymbols(record)</c> reported <c>1</c> against unfixed code. The base class
    /// honours the mask, and the member is sealed, so it cannot be got wrong.
    /// </summary>
    [Fact]
    public void TheTypesMaskIsHonoured()
    {
        var engine = EngineWith(out _);

        engine.Evaluate("Object.getOwnPropertySymbols(doc).length").Should().Be(0);

        engine.Execute("doc[Symbol.iterator] = function () {};");
        engine.Evaluate("Object.getOwnPropertySymbols(doc).length").Should().Be(1);
        engine.Evaluate("Object.getOwnPropertySymbols(doc)[0] === Symbol.iterator").Should().Be(true);
        engine.Evaluate("Object.getOwnPropertyNames(doc).join()").Should().Be("title,views,id");
    }

    // -------------------------------------------------------------------------------------------------
    // The rest of the model still holds
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void DefinePropertyOnAProjectedNameIsStillRefused()
    {
        var engine = EngineWith(out var record);

        var act = () => engine.Evaluate("Object.defineProperty(doc, 'title', { value: 'defined' })");
        act.Should().Throw<JavaScriptException>();
        record.Read("title").Should().Be("Hello");

        // a name the projection does not carry defines ordinarily
        engine.Evaluate("Object.defineProperty(doc, 'extra', { value: 1, enumerable: true }); doc.extra").Should().Be(1);
    }

    [Fact]
    public void TheRecordIsStillVisibleToTheClrConversion()
    {
        var engine = EngineWith(out var record);
        engine.Evaluate("doc.title = 'converted'");

        var converted = record.ToObject() as IDictionary<string, object?>;
        converted.Should().NotBeNull();
        converted!["title"].Should().Be("converted");
        converted["id"].Should().Be("doc/1");
    }

    [Fact]
    public void ObjectAssignCopiesTheRecordOut()
    {
        var engine = EngineWith(out _);

        engine.Evaluate("JSON.stringify(Object.assign({}, doc))")
            .Should().Be("""{"title":"Hello","views":7,"id":"doc/1"}""");
    }

    /// <summary>
    /// The <c>IsNameWritable</c>/<c>TrySetNamedValue</c> pair is one declaration with two halves, and a build
    /// with host-contract verification on reports either half missing rather than letting the object ship with
    /// a descriptor that advertises an assignment which always fails.
    /// </summary>
    /// <summary>
    /// Whether Jint's host-contract verifiers are running: always in a Debug build, and in Release when
    /// <c>Jint.EnableHostContractVerification</c> was set before the first use of any Jint type — which is what
    /// this repository's Release verification leg does (<c>JINT_HOST_CONTRACT_VERIFICATION=1</c>). Public and
    /// static so xUnit can read it for <c>SkipUnless</c>.
    /// </summary>
    public static bool Verifying => HostContractVerificationSwitch.Enabled;

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void DeclaringANameWritableWithNoWriteHookIsReportedWhenVerifying()
    {
        var engine = new Engine();
        engine.SetValue("broken", new WritableWithoutAWriteHook(engine));

        var act = () => engine.Evaluate("broken.alpha = 1");
        act.Should().Throw<InvalidOperationException>().WithMessage("*does not override TrySetNamedValue*");
    }

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void AWriteHookWithNoWritabilityDeclarationIsReportedWhenVerifying()
    {
        var engine = new Engine();
        engine.SetValue("broken", new WriteHookWithoutWritability(engine));

        var act = () => engine.Evaluate("Object.getOwnPropertyDescriptor(broken, 'alpha')");
        act.Should().Throw<InvalidOperationException>().WithMessage("*never overrides IsNameWritable*");
    }

    /// <summary>
    /// A projection whose names are read-only but <em>removable</em> is an ordinary shape, not a mistake:
    /// deletion is governed by <c>configurable</c>, which a projected name always reports <c>true</c>. It must
    /// therefore work, and must not trip the dead-write-hook verifier.
    /// </summary>
    [Fact]
    public void ADeleteOnlyProjectionIsAnOrdinaryShape()
    {
        var engine = new Engine();
        engine.SetValue("bag", new DeleteOnlyRecord(engine));

        engine.Evaluate("bag.alpha").Should().Be("a");
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(bag, 'alpha'))")
            .Should().Be("""{"value":"a","writable":false,"enumerable":true,"configurable":true}""");

        engine.Evaluate("bag.alpha = 'ignored'; bag.alpha").Should().Be("a");
        engine.Evaluate("delete bag.alpha").Should().Be(true);
        engine.Evaluate("'alpha' in bag").Should().Be(false);
        engine.Evaluate("Object.keys(bag).length").Should().Be(0);
    }

    private sealed class DeleteOnlyRecord : NamedPropertyObject
    {
        private readonly List<string> _names = new() { "alpha" };

        public DeleteOnlyRecord(Engine engine) : base(engine)
        {
        }

        public override int NameCount => _names.Count;

        public override string NameAt(int index) => _names[index];

        public override bool TryGetNamedValue(string name, out JsValue value)
        {
            value = "a";
            return _names.Contains(name);
        }

        protected override bool TryDeleteName(string name) => _names.Remove(name);
    }

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void ADeleteThatDidNotDeleteIsReportedWhenVerifying()
    {
        var engine = new Engine();
        engine.SetValue("broken", new LyingDelete(engine));

        var act = () => engine.Evaluate("delete broken.alpha");
        act.Should().Throw<InvalidOperationException>().WithMessage("*still produces a value*");
    }

    private sealed class WritableWithoutAWriteHook : NamedPropertyObject
    {
        public WritableWithoutAWriteHook(Engine engine) : base(engine)
        {
        }

        public override int NameCount => 1;

        public override string NameAt(int index) => "alpha";

        public override bool TryGetNamedValue(string name, out JsValue value)
        {
            value = "a";
            return string.Equals(name, "alpha", StringComparison.Ordinal);
        }

        protected override bool IsNameWritable(string name) => true;
    }

    private sealed class WriteHookWithoutWritability : NamedPropertyObject
    {
        public WriteHookWithoutWritability(Engine engine) : base(engine)
        {
        }

        public override int NameCount => 1;

        public override string NameAt(int index) => "alpha";

        public override bool TryGetNamedValue(string name, out JsValue value)
        {
            value = "a";
            return string.Equals(name, "alpha", StringComparison.Ordinal);
        }

        protected override bool TrySetNamedValue(string name, JsValue value) => true;
    }

    private sealed class LyingDelete : NamedPropertyObject
    {
        public LyingDelete(Engine engine) : base(engine)
        {
        }

        public override int NameCount => 1;

        public override string NameAt(int index) => "alpha";

        public override bool TryGetNamedValue(string name, out JsValue value)
        {
            value = "a";
            return string.Equals(name, "alpha", StringComparison.Ordinal);
        }

        protected override bool IsNameWritable(string name) => true;

        protected override bool TrySetNamedValue(string name, JsValue value) => true;

        protected override bool TryDeleteName(string name) => true;
    }
}

/// <summary>
/// The reconstruction: a document-shaped record with writable data fields, read-only computed fields and a
/// schema that decides which names an assignment may create. Six declarations, no specification algorithm
/// among them, and nothing that can disagree with anything else.
/// </summary>
internal sealed class WritableHostRecord : NamedPropertyObject
{
    private readonly List<string> _names = new();
    private readonly Dictionary<string, JsValue> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _computed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _schema = new(StringComparer.Ordinal) { "title", "views", "summary" };

    public WritableHostRecord(Engine engine) : base(engine)
    {
    }

    public WritableHostRecord AddField(string name, JsValue value)
    {
        Store(name, value);
        _schema.Add(name);
        return this;
    }

    public WritableHostRecord AddComputed(string name, JsValue value)
    {
        Store(name, value);
        _computed.Add(name);
        return this;
    }

    /// <summary>What the host's own state says, so a test can prove a write landed in it.</summary>
    public JsValue? Read(string name) => _values.TryGetValue(name, out var value) ? value : null;

    private void Store(string name, JsValue value)
    {
        if (!_values.ContainsKey(name))
        {
            _names.Add(name);
        }

        _values[name] = value;
    }

    // --- the six declarations ------------------------------------------------------------------------

    public override int NameCount => _names.Count;

    public override string NameAt(int index) => _names[index];

    public override bool TryGetNamedValue(string name, out JsValue value)
    {
        if (_values.TryGetValue(name, out var found))
        {
            value = found;
            return true;
        }

        value = JsValue.Undefined;
        return false;
    }

    // outside the Jint assembly a `protected internal` member is visible as `protected`
    protected override bool IsNameWritable(string name) => !_computed.Contains(name) && _schema.Contains(name);

    protected override bool TrySetNamedValue(string name, JsValue value)
    {
        // the record refuses a value it will not store, which is a TypeError in strict mode
        if (string.Equals(name, "views", StringComparison.Ordinal) && !value.IsNumber())
        {
            return false;
        }

        Store(name, value);
        return true;
    }

    protected override bool TryDeleteName(string name)
    {
        if (_computed.Contains(name))
        {
            return false;
        }

        _names.Remove(name);
        _values.Remove(name);
        return true;
    }
}
