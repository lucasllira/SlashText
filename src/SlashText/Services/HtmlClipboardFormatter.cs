using System.Text;

namespace SlashText.Services;

public static class HtmlClipboardFormatter
{
    private const string Header =
        "Version:0.9\r\n" +
        "StartHTML:{0:0000000000}\r\n" +
        "EndHTML:{1:0000000000}\r\n" +
        "StartFragment:{2:0000000000}\r\n" +
        "EndFragment:{3:0000000000}\r\n";

    public static string Create(string fragment)
    {
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";
        var html = $"<html><body>{startMarker}{fragment}{endMarker}</body></html>";
        var emptyHeader = string.Format(Header, 0, 0, 0, 0);

        var startHtml = Encoding.UTF8.GetByteCount(emptyHeader);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(html[..html.IndexOf(startMarker, StringComparison.Ordinal)]) +
                            Encoding.UTF8.GetByteCount(startMarker);
        var endFragment = startHtml + Encoding.UTF8.GetByteCount(html[..html.IndexOf(endMarker, StringComparison.Ordinal)]);
        var endHtml = startHtml + Encoding.UTF8.GetByteCount(html);

        return string.Format(Header, startHtml, endHtml, startFragment, endFragment) + html;
    }
}
