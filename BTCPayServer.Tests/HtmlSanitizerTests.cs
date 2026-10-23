using BTCPayServer.Hosting;
using Xunit;

namespace BTCPayServer.Tests;

public class HtmlSanitizerTests
{
    [Fact]
    public void RestrictsCssProperties()
    {
        var sanitizer = BTCPayServerServices.CreateHtmlSanitizer();

        var sanitized = sanitizer.Sanitize(
            "<style>p { color: red; background-image: url(https://example.com/pixel); position: fixed; }</style>" +
            "<p style=\"font-weight: bold; background-image: url(https://example.com/pixel); position: fixed\">Test</p>");

        Assert.Contains("color:", sanitized);
        Assert.Contains("font-weight: bold", sanitized);
        Assert.DoesNotContain("background-image", sanitized);
        Assert.DoesNotContain("position", sanitized);
    }

    [Fact]
    public void EnforcesIframeSecurityAttributes()
    {
        var sanitizer = BTCPayServerServices.CreateHtmlSanitizer();

        var sanitized = sanitizer.Sanitize(
            "<iframe src=\"https://example.com/embed\" sandbox=\"allow-same-origin allow-top-navigation\" " +
            "referrerpolicy=\"unsafe-url\" loading=\"eager\"></iframe>");

        Assert.Contains("sandbox=\"allow-scripts allow-forms allow-popups\"", sanitized);
        Assert.Contains("referrerpolicy=\"no-referrer\"", sanitized);
        Assert.Contains("loading=\"lazy\"", sanitized);
        Assert.DoesNotContain("allow-same-origin", sanitized);
        Assert.DoesNotContain("allow-top-navigation", sanitized);
        Assert.DoesNotContain("unsafe-url", sanitized);
        Assert.DoesNotContain("eager", sanitized);
    }
}
