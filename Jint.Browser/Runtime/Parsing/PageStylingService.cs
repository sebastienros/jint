using AngleSharp.Css;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// Leaves CSS parsing to AngleSharp and notifies the page when a linked stylesheet has been processed.
/// </summary>
internal sealed class PageStylingService(ParserDriver driver) : IStylingService
{
    private readonly IStylingService _inner = new CssStylingService();

    public bool SupportsType(string mimeType) => _inner.SupportsType(mimeType);

    public async Task<IStyleSheet> ParseStylesheetAsync(IResponse response, StyleOptions options, CancellationToken cancel)
    {
        IStyleSheet sheet;
        try
        {
            sheet = await _inner.ParseStylesheetAsync(response, options, cancel).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && options.Element is IHtmlLinkElement failedLink)
        {
            // AngleSharp's request processor catches parsing failures and fires into its own event bus.
            // Report the failure to the page too, without turning it into a successful parse.
            driver.StyleSheetProcessed(failedLink, exception);
            throw;
        }

        if (options.Element is IHtmlLinkElement link)
        {
            driver.StyleSheetProcessed(link);
        }

        return sheet;
    }
}
