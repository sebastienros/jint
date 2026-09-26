using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal static class NativeCssDeclarations
{
    private static readonly ConditionalWeakTable<Element, InlineDeclaration> Inline = new();
    private static readonly ConditionalWeakTable<CssDeclarationBlock, RuleDeclaration> Rules = new();

    internal static NativeCssDeclaration Of(DomRealm realm, Element element) =>
        Inline.GetValue(element, owner => new(realm, owner));
    internal static NativeCssDeclaration Of(DomRealm realm, CssStyleRule rule) =>
        Rules.GetValue(rule.Style, block => new(realm, rule, block));

    private static CssValueWork Work(DomRealm realm) => new(realm.CancellationToken, realm.Engine.Constraints.Check);

    private sealed class RuleDeclaration(DomRealm realm, CssStyleRule rule, CssDeclarationBlock block) : NativeCssDeclaration
    {
        internal override CssRule ParentRule => rule;
        internal override int Length { get { var work = Work(realm); work.CheckCancellation(); return block.Count; } }
        internal override string CssText
        {
            get => block.Serialize(Work(realm));
            set
            {
                var work = Work(realm);
                block.ReplaceText(value, null, work, work.Token);
            }
        }
        internal override string Item(int index)
        {
            var work = Work(realm);
            work.CheckCancellation();
            return (uint) index < (uint) block.Count ? block.GetPropertyName(index) : "";
        }
        internal override string GetPropertyValue(string name) => block.GetPropertyValue(name, Work(realm));
        internal override string GetPropertyPriority(string name) => block.GetPropertyPriority(name, Work(realm));
        internal override string RemoveProperty(string name) => block.RemoveProperty(name, Work(realm));
        internal override void SetProperty(string name, string value, string? priority = null)
        {
            var work = Work(realm);
            block.SetProperty(name, value, priority, null, work, work.Token);
        }
    }

    private sealed class InlineDeclaration(DomRealm creationRealm, Element element) : NativeCssDeclaration
    {
        private string? _source;
        private CssDeclarationBlock? _block;
        internal override CssRule? ParentRule => null;
        private CssValueWork CurrentWork() => Work(element.OwnerDocument is { } document &&
            NativeCssStyleSheets.RealmOf(document) is { } realm ? realm : creationRealm);
        private string Source(CssValueWork work) => new DomReadWork(work.Charge, work.Token).Attribute(element, "style") ?? "";
        private CssDeclarationBlock Read(CssValueWork work)
        {
            work.CheckCancellation();
            var source = Source(work);
            if (_block is null || _source is null || !CssSubstitutionArguments.Equals(source, _source, work))
            {
                var block = CssDeclarationBlock.Parse(source, CssDeclarationContext.Style, null, work, work.Token);
                work.CheckCancellation();
                _source = source;
                _block = block;
            }
            return _block;
        }
        internal override int Length => Read(CurrentWork()).Count;
        internal override string CssText
        {
            get { var work = CurrentWork(); return Read(work).Serialize(work); }
            set
            {
                var work = CurrentWork();
                var block = CssDeclarationBlock.Parse(value, CssDeclarationContext.Style, null, work, work.Token);
                Publish(block, work);
            }
        }
        internal override string Item(int index)
        {
            var work = CurrentWork();
            var block = Read(work);
            return (uint) index < (uint) block.Count ? block.GetPropertyName(index) : "";
        }
        internal override string GetPropertyValue(string name) { var work = CurrentWork(); return Read(work).GetPropertyValue(name, work); }
        internal override string GetPropertyPriority(string name) { var work = CurrentWork(); return Read(work).GetPropertyPriority(name, work); }
        internal override string RemoveProperty(string name)
        {
            var work = CurrentWork();
            var block = Copy(work);
            var stamp = block.Stamp;
            var result = block.RemoveProperty(name, work);
            if (block.Stamp != stamp) Publish(block, work);
            return result;
        }
        internal override void SetProperty(string name, string value, string? priority = null)
        {
            var work = CurrentWork();
            var block = Copy(work);
            var stamp = block.Stamp;
            block.SetProperty(name, value, priority, null, work, work.Token);
            if (block.Stamp != stamp) Publish(block, work);
        }
        private CssDeclarationBlock Copy(CssValueWork work) => CssDeclarationBlock.Parse(Source(work),
            CssDeclarationContext.Style, null, work, work.Token);
        private void Publish(CssDeclarationBlock block, CssValueWork work)
        {
            var text = block.Serialize(work);
            work.CheckCancellation();
            element.SetAttribute("style", text);
            _source = text;
            _block = block;
        }
    }
}
