namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.7, in-body select/option/optgroup rules
    // (living standard inspected 2026-09-23). Fragment context is H7b.
    private bool SelectStart(string name)
    {
        if (name == "select")
        {
            if (InScope("select"))
            {
                Error("nested-select-start-tag");
                SchedulePopTo(Last("select"), reprocess: false);
                return false;
            }
        }
        else if (InScope("select"))
        {
            // An option start preserves its optgroup; an optgroup start
            // closes both option and optgroup. Each pop is resumable.
            if (!TryGenerateImpliedEndTags(name == "option" ? "optgroup" : null)) return true;
            if (InScope("option") || name == "optgroup" && InScope("optgroup"))
                Error("misnested-select-option-start-tag");
        }
        else if (IsHtmlElement(Current, "option"))
        {
            Pop();
        }

        if (!TryReconstructFormatting()) return true;
        InsertTokenElement();
        if (name == "select") _framesetOk = false;
        return false;
    }
}
