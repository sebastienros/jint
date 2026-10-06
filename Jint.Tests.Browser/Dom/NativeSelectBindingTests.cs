#nullable enable
using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeSelectBindingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void WarmIndexedOptionsUseLinearWorkAcrossTheExistingCollectionWrapper(bool selectedOnly)
    {
        var checks = new ReadChecks();
        using var engine = new Engine(options => options.AddConstraint(checks));
        DomBindings.Install(engine);
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        select.SetAttribute("multiple", "");
        var options = new Element[128];
        for (var index = 0; index < options.Length; index++)
        {
            options[index] = document.CreateElement("option");
            options[index].SetAttribute("selected", "");
            select.AppendChild(options[index]);
        }
        var realm = DomRealm.Of(engine);
        var view = DomSelectCollection.Of(realm, select, selectedOnly);
        var collection = (DomHtmlCollectionObject<Element>) realm.WrapCollection<Element>(view);
        collection.Length.Should().Be((uint) options.Length);
        checks.Clear();
        for (uint index = 0; index < options.Length; index++)
        {
            collection.TryGetIndex(index, out var value).Should().BeTrue();
            ((IDomWrapper) value).DomTarget.Should().BeSameAs(options[index]);
        }
        checks.Count.Should().BeLessThanOrEqualTo(options.Length * 4);
    }

    private sealed class ReadChecks : Constraint
    {
        internal int Count { get; private set; }
        internal void Clear() => Count = 0;
        public override void Check() => Count++;
        public override void Reset() { }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AStableNativeViewKeepsCurrentMembershipAndSelectednessAfterAdoption(bool selectedOnly)
    {
        using var dom = DomTestFixture.Create("<select id=s multiple><option selected>A</option><option selected>B</option></select>");
        var select = ContentDom.ElementById(dom.Document, "s")!;
        var realm = DomRealm.Of(dom.Engine);
        var view = DomSelectCollection.Of(realm, select, selectedOnly);
        view.GetLength(realm).Should().Be(2);
        var first = view.GetItem(realm, 0)!;
        var second = view.GetItem(realm, 1)!;
        select.RemoveChild(first);
        view.GetLength(realm).Should().Be(1);
        view.GetItem(realm, 0).Should().BeSameAs(second);

        var destination = Document.CreateHtml();
        destination.AdoptNode(select);
        var added = destination.CreateElement("option");
        added.SetAttribute("selected", "");
        select.InsertBefore(added, second);
        view.GetLength(realm).Should().Be(2);
        view.GetItem(realm, 0).Should().BeSameAs(added);
        second.GetHtmlState()!.Option!.SetSelected(false, default);
        view.GetLength(realm).Should().Be(selectedOnly ? 1 : 2);
        view.GetItem(realm, 1).Should().BeSameAs(selectedOnly ? null : second);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AWarmNativeViewUsesTheCurrentCallersChecks(bool selectedOnly)
    {
        using var dom = DomTestFixture.Create("<select id=s multiple><option selected>A</option></select>");
        var select = ContentDom.ElementById(dom.Document, "s")!;
        var realm = DomRealm.Of(dom.Engine);
        var view = DomSelectCollection.Of(realm, select, selectedOnly);
        view.GetLength(realm).Should().Be(1);
        using var callerEngine = new Engine(options => options.AddConstraint(new CancelRead()));
        var caller = DomRealm.Of(callerEngine);
        Caught.Exception(() => view.GetLength(caller)).Should().BeOfType<OperationCanceledException>();
        Caught.Exception(() => view.GetItem(caller, 0)).Should().BeOfType<OperationCanceledException>();
        view.GetLength(realm).Should().Be(1);
    }

    private sealed class CancelRead : Constraint
    {
        public override void Check() => throw new OperationCanceledException();
        public override void Reset() { }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AWarmNativeViewUsesTheCurrentCancellationToken(bool selectedOnly)
    {
        using var cancellation = new CancellationTokenSource();
        var constraint = new Jint.Constraints.CancellationConstraint(default);
        using var engine = new Engine(options => options.AddConstraint(constraint));
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var option = document.CreateElement("option");
        option.SetAttribute("selected", "");
        select.AppendChild(option);
        var realm = DomRealm.Of(engine);
        var view = DomSelectCollection.Of(realm, select, selectedOnly);
        view.GetLength(realm).Should().Be(1);
        cancellation.Cancel();
        constraint.Reset(cancellation.Token);
        Caught.Exception(() => view.GetLength(realm)).Should().BeOfType<OperationCanceledException>();
        Caught.Exception(() => view.GetItem(realm, 0)).Should().BeOfType<OperationCanceledException>();
        constraint.Reset(default);
        view.GetLength(realm).Should().Be(1);
    }

    [Test]
    public void OptionsKeepTheirPrototypeAndLiveSelectednessIdentity()
    {
        using var dom = DomTestFixture.Create("<select id=s><option value=a>A</option><option value=b>B</option></select>");
        dom.Execute("var s=document.getElementById('s'), options=s.options, selected=s.selectedOptions;");
        dom.Bool("options===s.options && options instanceof HTMLOptionsCollection && selected===s.selectedOptions && selected instanceof HTMLCollection").Should().BeTrue();
        dom.Bool("s[0]===options[0]").Should().BeTrue();
        dom.Execute("options.selectedIndex=1;");
        dom.Bool("s.selectedIndex===1 && s.value==='b' && selected[0]===options[1]").Should().BeTrue();
        dom.Execute("s.value='missing';");
        dom.Number("selected.length").Should().Be(0);
        dom.Number("options.selectedIndex").Should().Be(-1);
    }

    [Test]
    public void AddUsesTheNativeOptionAndOptgroupUnionAndBeforeIndex()
    {
        using var dom = DomTestFixture.Create("<select id=s><option value=a>A</option></select>");
        dom.Execute("var s=document.getElementById('s'), group=document.createElement('optgroup'), option=document.createElement('option'); option.value='b'; group.appendChild(option); s.add(group,0);");
        dom.Bool("s.options[0]===option && s.options[1].value==='a'").Should().BeTrue();
        dom.Execute("var last=document.createElement('option'); s.options.add(last); s.remove(1);");
        dom.Bool("s.length===2 && s.options[1]===last").Should().BeTrue();
        dom.Text("(()=>{try{s.add(document.createElement('div'))}catch(e){return e.name}})()").Should().Be("TypeError");
    }
}
