using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

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
        internal override int Length => block.ResolveAll(Work(realm)).Length;
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
            var entries = block.ResolveAll(work);
            return (uint) index < (uint) entries.Length ? entries[index].Name : "";
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
        internal override CssRule? ParentRule => null;
        private CssValueWork CurrentWork()
        {
            var document = element.OwnerDocument;
            var stamp = document?.MutationStamp;
            var realm = document is not null && NativeCssStyleSheets.RealmOf(document) is { } host ? host : creationRealm;
            return new(realm.CancellationToken, () =>
            {
                realm.Engine.Constraints.Check();
                if (!ReferenceEquals(element.OwnerDocument, document) || stamp == ulong.MaxValue || document?.MutationStamp != stamp)
                    throw new InvalidOperationException(NativeCssQuery.Invalidated);
            });
        }
        private CssDeclarationBlock Read(CssValueWork work)
        {
            return NativeCssStyleSheets.InlineOf(element, work);
        }
        internal override int Length
        {
            get { var work = CurrentWork(); return Read(work).ResolveAll(work).Length; }
        }
        internal override string CssText
        {
            get { var work = CurrentWork(); return Read(work).Serialize(work); }
            set
            {
                var work = CurrentWork();
                var block = CssDeclarationBlock.Parse(value, CssDeclarationContext.Style, null, work, work.Token);
                Publish(block, work, cssText: true);
            }
        }
        internal override string Item(int index)
        {
            var work = CurrentWork();
            var block = Read(work);
            var entries = block.ResolveAll(work);
            return (uint) index < (uint) entries.Length ? entries[index].Name : "";
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
        private CssDeclarationBlock Copy(CssValueWork work) => Read(work).Copy(work);
        private void Publish(CssDeclarationBlock block, CssValueWork work, bool cssText = false)
        {
            var text = cssText ? block.Serialize(work) : block.SerializeSource(work);
            work.CheckCancellation();
            element.SetAttribute("style", text);
        }
    }
}
