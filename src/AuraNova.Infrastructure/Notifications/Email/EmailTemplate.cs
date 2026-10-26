using System.Net;
using System.Text;

namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>Correo con la marca: el mismo texto del WhatsApp en HTML y un botón de acción.</summary>
    public static class EmailTemplate
    {
        public static string Render(string businessName, string title, string message, string? actionLabel, string? actionUrl)
        {
            var paragraphs = new StringBuilder();
            foreach (var block in message.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                var lines = block.Split('\n').Select(l => Linkify(WebUtility.HtmlEncode(l.Trim())));
                paragraphs.Append("<p style=\"margin:0 0 14px;line-height:1.6;color:#5e4a42;font-size:15px\">")
                    .Append(string.Join("<br>", lines))
                    .Append("</p>");
            }

            var button = string.IsNullOrWhiteSpace(actionUrl) || string.IsNullOrWhiteSpace(actionLabel)
                ? ""
                : $"""
                  <p style="margin:24px 0 8px;text-align:center">
                    <a href="{WebUtility.HtmlEncode(actionUrl)}" style="display:inline-block;background:#c8a96b;color:#ffffff;text-decoration:none;font-weight:bold;padding:14px 28px;border-radius:999px;font-size:15px">{WebUtility.HtmlEncode(actionLabel)}</a>
                  </p>
                  """;

            var name = WebUtility.HtmlEncode(businessName);
            return $"""
                <!doctype html>
                <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{WebUtility.HtmlEncode(title)}</title></head>
                <body style="margin:0;padding:0;background:#faf7f2;font-family:Georgia,'Times New Roman',serif">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#faf7f2;padding:24px 12px">
                    <tr><td align="center">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:24px;border:1px solid #efe4d6">
                        <tr><td style="padding:28px 28px 8px;text-align:center">
                          <p style="margin:0;letter-spacing:4px;font-size:12px;color:#c8a96b;font-weight:bold">✦ {name.ToUpperInvariant()} ✦</p>
                          <h1 style="margin:10px 0 0;font-size:24px;color:#4a3933">{WebUtility.HtmlEncode(title)}</h1>
                        </td></tr>
                        <tr><td style="padding:16px 28px 24px;font-family:Arial,Helvetica,sans-serif">
                          {paragraphs}
                          {button}
                        </td></tr>
                        <tr><td style="padding:16px 28px 24px;border-top:1px solid #f1e9e4;text-align:center;font-family:Arial,Helvetica,sans-serif;font-size:12px;color:#a89d96">
                          Hecho a mano con cariño · {name}
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body></html>
                """;
        }

        /// <summary>Convierte las URLs del mensaje en enlaces (ya vienen escapadas).</summary>
        private static string Linkify(string encodedLine)
        {
            var index = encodedLine.IndexOf("https://", StringComparison.Ordinal);
            if (index < 0)
                return encodedLine;
            var end = encodedLine.IndexOf(' ', index);
            var url = end < 0 ? encodedLine[index..] : encodedLine[index..end];
            return encodedLine.Replace(url, $"<a href=\"{url}\" style=\"color:#b08a4e\">{url}</a>");
        }
    }
}
