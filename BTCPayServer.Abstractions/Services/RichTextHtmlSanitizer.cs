using Ganss.Xss;

namespace BTCPayServer.Abstractions.Services;

public sealed class RichTextHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public RichTextHtmlSanitizer(HtmlSanitizer sanitizer)
    {
        _sanitizer = sanitizer;
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
