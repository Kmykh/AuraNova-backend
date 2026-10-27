using System.Text;
using System.Text.RegularExpressions;

namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>Color de la etiqueta de estado del correo.</summary>
    public enum EmailTone { Neutral, Success, Warning, Danger }

    /// <summary>Encabezado y botón del correo (el cuerpo es el mismo texto del WhatsApp).</summary>
    public record EmailLayout(string Eyebrow, string Title, string? Badge, EmailTone Tone, string? ActionLabel, string? ActionUrl);

    /// <summary>Datos de la marca para el encabezado y el pie.</summary>
    public record EmailBrand(string BusinessName, string SiteUrl, string? WhatsAppNumber);

    /// <summary>
    /// Correo con el estilo de la tienda: fondo crema, tarjeta blanca, títulos en serif marrón y detalles en dorado.
    /// El mensaje se escribe en texto (el mismo del WhatsApp) y aquí se le da forma:
    /// "Hola …" es el saludo, los bloques de líneas "Dato: valor" son la tabla de detalles
    /// y "Gracias por …" es la despedida. Incluye estilos para el modo oscuro.
    /// </summary>
    public static partial class EmailTemplate
    {
        private const string Serif = "'Playfair Display',Georgia,'Times New Roman',serif";
        private const string Sans = "Inter,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

        /// <summary>Ajustes para celular y colores del modo oscuro (Apple Mail, iOS y otros que lo soportan).</summary>
        private const string Style = """
            <style>
              :root { color-scheme: light dark; supported-color-schemes: light dark; }
              body { margin:0; padding:0; -webkit-text-size-adjust:100%; }
              @media (max-width:600px) {
                .px { padding-left:24px !important; padding-right:24px !important; }
                .title { font-size:25px !important; }
              }
              @media (prefers-color-scheme: dark) {
                .bg { background:#1d1815 !important; }
                .card { background:#28211c !important; border-color:#3b3029 !important; }
                .t1 { color:#f3ebe3 !important; }
                .t2 { color:#d2c3b8 !important; }
                .t3 { color:#a2948a !important; }
                .gold { color:#dcc08a !important; }
                .box { background:#322922 !important; }
                .line { border-color:#3b3029 !important; }
                .btn { background:#c8a96b !important; color:#1d1815 !important; }
              }
            </style>
            """;

        [GeneratedRegex(@"^(?<key>[^:\n]{2,40}):\s+(?<value>\S.*)$")]
        private static partial Regex DetailLine();

        public static string Render(EmailBrand brand, EmailLayout layout, string message)
        {
            var site = brand.SiteUrl.TrimEnd('/');
            var blocks = message.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var body = new StringBuilder();
            string? closing = null;
            string? preheader = null;

            foreach (var block in blocks)
            {
                var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (lines.Length == 0) continue;

                if (lines.Length == 1 && lines[0].StartsWith("Hola", StringComparison.OrdinalIgnoreCase) && body.Length == 0)
                {
                    body.Append($"""<p class="t1" style="margin:0 0 14px;font-size:16px;font-weight:600;color:#4a3933">{Encode(lines[0])}</p>""");
                }
                else if (lines.Length == 1 && lines[0].StartsWith("Gracias", StringComparison.OrdinalIgnoreCase))
                {
                    closing = lines[0];
                }
                else if (lines.All(IsDetail))
                {
                    body.Append(Details(lines, layout.ActionUrl));
                }
                else
                {
                    // En el WhatsApp la primera frase da el contexto; en el correo ya es el título y no se repite
                    if (preheader == null) lines = WithoutTitle(lines, layout.Title);
                    if (lines.Length == 0) continue;
                    preheader ??= string.Join(' ', lines);
                    body.Append($"""<p class="t2" style="margin:0 0 14px;font-size:15px;line-height:1.65;color:#6b5a50">{string.Join("<br>", lines.Select(l => Linkify(Encode(l))))}</p>""");
                }
            }

            var badge = string.IsNullOrWhiteSpace(layout.Badge) ? "" : Badge(layout.Badge, layout.Tone);
            var button = string.IsNullOrWhiteSpace(layout.ActionUrl) || string.IsNullOrWhiteSpace(layout.ActionLabel) ? "" : Button(layout.ActionLabel, layout.ActionUrl);
            var closingRow = closing == null ? "" : $"""
                <tr><td class="px" style="padding:0 40px 34px">
                  <p class="t2 line" style="margin:0;padding-top:22px;border-top:1px solid #f1e9e4;font-family:{Serif};font-style:italic;font-size:16px;color:#6b5a50">{Encode(closing)}</p>
                </td></tr>
                """;
            var whatsapp = WhatsAppLink(brand.WhatsAppNumber);
            var contact = whatsapp == null
                ? "¿Tienes alguna duda? Responde este correo y te ayudamos."
                : $"""¿Tienes alguna duda? Responde este correo o <a href="{whatsapp}" style="color:#8f6d28;font-weight:600">escríbenos por WhatsApp</a>.""";
            var name = Encode(brand.BusinessName);
            var host = Encode(site.Replace("https://", "").Replace("http://", "").Replace("www.", ""));

            return $"""
                <!doctype html>
                <html lang="es">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width,initial-scale=1">
                <meta name="color-scheme" content="light dark">
                <meta name="supported-color-schemes" content="light dark">
                <title>{Encode(layout.Title)}</title>
                <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&family=Playfair+Display:ital,wght@0,600;0,700;1,500&display=swap" rel="stylesheet">
                {Style}
                </head>
                <body class="bg" style="margin:0;padding:0;background:#f7f1ea">
                  <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all">{Encode(Preview(preheader ?? layout.Title))}&#8199;&#65279;&#847;&#8199;&#65279;&#847;&#8199;&#65279;&#847;&#8199;&#65279;&#847;</div>
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" class="bg" style="background:#f7f1ea">
                    <tr><td align="center" style="padding:32px 12px">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px">
                        <tr><td align="center" style="padding:0 0 22px">
                          <img src="{site}/mail/flortallo.png" width="60" height="60" alt="" style="display:block;margin:0 auto 4px;width:60px;height:60px;border:0">
                          <p class="t1" style="margin:0;font-family:{Serif};font-size:28px;font-weight:700;letter-spacing:0.5px;color:#4a3933">{name}</p>
                          <p class="gold" style="margin:4px 0 0;font-family:{Serif};font-style:italic;font-size:14px;color:#b08a4e">El arte de regalar</p>
                        </td></tr>
                        <tr><td class="card" style="background:#ffffff;border:1px solid #ece3da;border-radius:28px">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                            <tr><td class="px" style="padding:36px 40px 6px">
                              <p class="gold" style="margin:0;font-family:{Sans};font-size:11px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#b08a4e">{Encode(layout.Eyebrow)}</p>
                              <h1 class="t1 title" style="margin:10px 0 0;font-family:{Serif};font-size:29px;line-height:1.25;font-weight:700;color:#4a3933">{Encode(layout.Title)}</h1>
                              {badge}
                            </td></tr>
                            <tr><td class="px" style="padding:22px 40px 4px;font-family:{Sans}">
                              {body}
                            </td></tr>
                            {button}
                            {closingRow}
                          </table>
                        </td></tr>
                        <tr><td align="center" class="px" style="padding:24px 28px 0;font-family:{Sans};font-size:12px;line-height:1.7;color:#a3958c">
                          <p class="t3" style="margin:0">{contact}</p>
                          <p class="t3" style="margin:6px 0 0"><a href="{site}" style="color:#a3958c;text-decoration:none">{host}</a> · Huancayo, Perú</p>
                          <p class="t3" style="margin:6px 0 0;font-size:11px;color:#b7aaa1">Te escribimos porque hiciste un pedido en {name}.</p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;
        }

        /// <summary>"Dato: valor" con una etiqueta corta (una frase con dos puntos no es un dato).</summary>
        private static bool IsDetail(string line)
        {
            var match = DetailLine().Match(line);
            return match.Success && match.Groups["key"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4;
        }

        /// <summary>Bloque "Dato: valor" como tabla; un enlace igual al del botón no se repite.</summary>
        private static string Details(string[] lines, string? actionUrl)
        {
            var rows = new List<(string Key, string Value)>();
            foreach (var line in lines)
            {
                var match = DetailLine().Match(line);
                var key = match.Groups["key"].Value.Trim();
                var value = match.Groups["value"].Value.Trim();
                if (IsUrl(value) && string.Equals(value, actionUrl, StringComparison.OrdinalIgnoreCase)) continue;
                rows.Add((key, value));
            }
            if (rows.Count == 0) return "";

            var html = new StringBuilder("""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" class="box" style="background:#faf7f2;border-radius:18px;margin:4px 0 18px"><tr><td style="padding:4px 20px">""");
            for (var i = 0; i < rows.Count; i++)
            {
                var (key, value) = rows[i];
                var border = i < rows.Count - 1 ? "border-bottom:1px solid #efe6dd;" : "";
                var emphasis = key.StartsWith("Total", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Falta", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Saldo", StringComparison.OrdinalIgnoreCase);
                string shown;
                if (IsUrl(value))
                    shown = $"""<a href="{Encode(value)}" style="color:#8f6d28;font-weight:700;text-decoration:none">{(value.Contains("/pago/") ? "Ir a pagar" : value.Contains("/seguimiento") ? "Ver mi pedido" : "Abrir")} &rarr;</a>""";
                else if (key.StartsWith("Código", StringComparison.OrdinalIgnoreCase))
                    shown = $"""<span style="font-family:'SFMono-Regular',Menlo,Consolas,monospace;letter-spacing:1.5px">{Encode(value)}</span>""";
                else
                    shown = Encode(value);

                html.Append($"""
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                      <td class="t3 line" style="padding:12px 12px 12px 0;{border}font-size:13px;color:#887870;vertical-align:top">{Encode(key)}</td>
                      <td class="t1 line" align="right" style="padding:12px 0;{border}font-size:{(emphasis ? "16" : "14")}px;font-weight:700;color:#4a3933;text-align:right;vertical-align:top">{shown}</td>
                    </tr></table>
                    """);
            }
            return html.Append("</td></tr></table>").ToString();
        }

        private static string Badge(string text, EmailTone tone)
        {
            var (background, color) = tone switch
            {
                EmailTone.Success => ("#e9f3ec", "#3f6b4c"),
                EmailTone.Warning => ("#fdf3df", "#8f6d28"),
                EmailTone.Danger => ("#fbeceb", "#a1453f"),
                _ => ("#f3eee9", "#6b5a50")
            };
            return $"""<p style="margin:14px 0 0"><span style="display:inline-block;padding:6px 13px;border-radius:999px;background:{background};color:{color};font-family:{Sans};font-size:12px;font-weight:700">&#9679;&nbsp; {Encode(text)}</span></p>""";
        }

        private static string Button(string label, string url) => $"""
            <tr><td class="px" align="center" style="padding:10px 40px 30px;font-family:{Sans}">
              <a class="btn" href="{Encode(url)}" style="display:inline-block;background:#4a3933;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px;padding:15px 36px;border-radius:999px">{Encode(label)}</a>
              <p class="t3" style="margin:14px 0 0;font-size:12px;line-height:1.5;color:#a3958c">¿El botón no funciona? Copia este enlace:<br><a href="{Encode(url)}" style="color:#8f6d28;word-break:break-all">{Encode(url)}</a></p>
            </td></tr>
            """;

        private static string? WhatsAppLink(string? number)
        {
            var digits = new string((number ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 9) digits = "51" + digits;
            return digits.Length >= 9 ? $"https://wa.me/{digits}" : null;
        }

        /// <summary>Quita la primera frase del párrafo si dice lo mismo que el título.</summary>
        private static string[] WithoutTitle(string[] lines, string title)
        {
            static string Normalize(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            var first = lines[0];
            var end = first.IndexOfAny(['.', '!']);
            var sentence = end < 0 ? first : first[..(end + 1)];
            if (Normalize(sentence) != Normalize(title))
                return lines;
            var rest = first[sentence.Length..].Trim();
            return rest.Length > 0 ? [rest, .. lines[1..]] : lines[1..];
        }

        private static string Preview(string text) => text.Length <= 120 ? text : text[..117].TrimEnd() + "…";

        private static bool IsUrl(string value) => value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        /// <summary>Escapa solo lo que rompe el HTML; las tildes y la ñ quedan tal cual (el correo va en UTF-8).</summary>
        private static string Encode(string value) => value
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

        /// <summary>Convierte las URLs del párrafo en enlaces (ya vienen escapadas).</summary>
        private static string Linkify(string encodedLine)
        {
            var index = encodedLine.IndexOf("https://", StringComparison.Ordinal);
            if (index < 0)
                return encodedLine;
            var end = encodedLine.IndexOf(' ', index);
            var url = end < 0 ? encodedLine[index..] : encodedLine[index..end];
            return encodedLine.Replace(url, $"<a href=\"{url}\" style=\"color:#8f6d28\">{url}</a>");
        }
    }
}
