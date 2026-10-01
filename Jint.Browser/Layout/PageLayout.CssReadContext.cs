using Jint.Browser.Styling;

namespace Jint.Browser.Layout;

internal sealed partial class PageLayout
{
    // A condition callback must reuse its caller's traversal, never start a new CSS query.
    internal FlatLayout.SizeQuery MeasureSizes(NativeCssReadContext context)
    {
        Diagnostics?.SizeQueryRequested();
        context.Verify();
        return context.MeasureSizes();
    }
}
