using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace StorePulse.Core;

public sealed record HtmlReportInfo(string NopVersion, DateTime? CompletedOnUtc, string HelpBaseUrl);

/// <summary>A self-contained, print-friendly report. Every dynamic value is HTML-encoded.</summary>
public static class HtmlReport
{
    private const string Styles = """
        :root{--ink:#1f2937;--muted:#6b7280;--line:#e5e7eb;--bg:#f8fafc;--ok:#15803d;--warn:#b45309;--bad:#b91c1c;--na:#64748b}
        *{box-sizing:border-box}body{margin:0;font:15px/1.55 -apple-system,"Segoe UI",Roboto,Arial,sans-serif;color:var(--ink);background:var(--bg)}
        main{max-width:960px;margin:0 auto;padding:32px 20px}
        header{display:flex;justify-content:space-between;align-items:flex-end;gap:16px;border-bottom:2px solid var(--ink);padding-bottom:16px;margin-bottom:24px}
        h1{margin:0;font-size:26px}h2{font-size:18px;margin:32px 0 12px}.sub{color:var(--muted);font-size:13px}
        .tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:12px}
        .tile{background:#fff;border:1px solid var(--line);border-radius:10px;padding:14px}.tile b{display:block;font-size:28px}
        .tile span{color:var(--muted);font-size:13px}
        .finding{background:#fff;border:1px solid var(--line);border-left:5px solid var(--na);border-radius:8px;padding:14px 16px;margin:10px 0;break-inside:avoid}
        .finding.critical{border-left-color:var(--bad)}.finding.warning{border-left-color:var(--warn)}.finding.healthy{border-left-color:var(--ok)}
        .finding h3{margin:0 0 4px;font-size:16px}.pill{display:inline-block;font-size:12px;font-weight:600;padding:2px 8px;border-radius:99px;background:#f1f5f9;color:var(--na)}
        .critical .pill{background:#fee2e2;color:var(--bad)}.warning .pill{background:#fef3c7;color:var(--warn)}.healthy .pill{background:#dcfce7;color:var(--ok)}
        .meta{color:var(--muted);font-size:12px}.evidence{margin:8px 0;padding:0;list-style:none;display:flex;flex-wrap:wrap;gap:6px}
        .evidence li{background:#f1f5f9;border-radius:6px;padding:2px 8px;font-size:13px}p{margin:6px 0}a{color:#1d4ed8}
        table{width:100%;border-collapse:collapse;background:#fff}td,th{border-bottom:1px solid var(--line);padding:6px 8px;text-align:left;font-size:13px}
        footer{margin-top:40px;padding-top:12px;border-top:1px solid var(--line);color:var(--muted);font-size:12px}
        @media print{body{background:#fff}main{padding:0}.finding,.tile{border-color:#ccc}}
        """;

    public static string Render(IReadOnlyList<HealthCheckResult> results, Func<string, string> translate,
        HtmlReportInfo? info = null)
    {
        static string Encode(string value) => HtmlEncoder.Default.Encode(value ?? "");
        string T(string key) => translate(key) ?? key;
        var score = HealthScoring.Calculate(results);
        var catalog = HealthScoring.CalculateCatalog(results);
        var operational = results.Where(r => r.Category != HealthCategory.Catalog).ToArray();
        int Count(HealthStatus status) => operational.Count(r => r.Status == status);

        var html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>StorePulse Health Report</title><style>")
            .Append(Styles).Append("</style></head><body><main><header><div><h1>StorePulse Health Report</h1><div class=\"sub\">");
        if (info?.NopVersion != null)
            html.Append("nopCommerce ").Append(Encode(info.NopVersion));
        if (info?.CompletedOnUtc != null)
            html.Append(" · ").Append(Encode(info.CompletedOnUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)));
        html.Append("</div></div><div class=\"sub\">Read-only diagnostics</div></header>");

        html.Append("<section class=\"tiles\">");
        Tile(html, score.Score?.ToString("0", CultureInfo.InvariantCulture) ?? "Unknown", "Health score (of 100)");
        Tile(html, score.CoveragePercent.ToString("0", CultureInfo.InvariantCulture) + "%", "Checks evaluated");
        Tile(html, Count(HealthStatus.Critical).ToString(CultureInfo.InvariantCulture), "Critical");
        Tile(html, Count(HealthStatus.Warning).ToString(CultureInfo.InvariantCulture), "Need attention");
        Tile(html, Count(HealthStatus.Healthy).ToString(CultureInfo.InvariantCulture), "Healthy");
        if (catalog.HasValue)
            Tile(html, catalog.Value.ToString("0", CultureInfo.InvariantCulture), "Catalog quality (of 100)");
        html.Append("</section>");

        void Section(string heading, IEnumerable<HealthCheckResult> items)
        {
            var list = items.ToArray();
            if (list.Length == 0)
                return;
            html.Append("<h2>").Append(Encode(heading)).Append(" (").Append(list.Length).Append(")</h2>");
            foreach (var result in list)
            {
                var tone = result.Status switch
                {
                    HealthStatus.Critical => "critical", HealthStatus.Warning => "warning",
                    HealthStatus.Healthy => "healthy", _ => "muted"
                };
                html.Append("<article class=\"finding ").Append(tone).Append("\"><h3>")
                    .Append(Encode(T(result.TitleResourceKey))).Append("</h3><span class=\"pill\">")
                    .Append(Encode(T(result.SummaryResourceKey))).Append("</span> <span class=\"meta\">")
                    .Append(Encode(result.CheckId)).Append(" · ")
                    .Append(Encode(T($"Plugins.Misc.StorePulse.Category.{result.Category}"))).Append(" · ")
                    .Append(Encode(result.Scope == HealthScope.Store ? "This store" : "Whole system")).Append("</span>");
                if (ScanComparison.IsProblem(result.Status))
                    html.Append("<p>").Append(Encode(T($"Plugins.Misc.StorePulse.{result.CheckId}.Why"))).Append("</p>");
                if (result.Metrics.Count > 0)
                {
                    html.Append("<ul class=\"evidence\">");
                    foreach (var metric in result.Metrics.Where(item => !MetricFlags.IsFlag(item.Key)))
                        html.Append("<li>").Append(Encode(T($"Plugins.Misc.StorePulse.Metric.{metric.Key}")))
                            .Append(": <b>").Append(MetricFlags.Format(result.Metrics, metric.Value, "0.##", CultureInfo.InvariantCulture))
                            .Append("</b></li>");
                    html.Append("</ul>");
                }
                if (result.Status != HealthStatus.Healthy && result.Status != HealthStatus.NotApplicable)
                {
                    html.Append("<p><b>What to do:</b> ").Append(Encode(T(result.RecommendationResourceKey)));
                    if (!string.IsNullOrEmpty(info?.HelpBaseUrl))
                        html.Append(" <a href=\"").Append(Encode(info.HelpBaseUrl + result.CheckId.ToLowerInvariant()))
                            .Append("\">Learn more</a>");
                    html.Append("</p>");
                }
                html.Append("</article>");
            }
        }

        Section("Critical", results.Where(r => r.Status == HealthStatus.Critical));
        Section("Needs attention", results.Where(r => r.Status == HealthStatus.Warning));
        Section("Couldn't check", results.Where(r => r.Status == HealthStatus.Unknown));
        Section("Healthy", results.Where(r => r.Status == HealthStatus.Healthy));
        Section("Not applicable", results.Where(r => r.Status == HealthStatus.NotApplicable));

        return html.Append("<footer>Unknown results are not passes. The score covers the checks StorePulse can run. ")
            .Append("This report contains counts and settings only — no customer data, email addresses, URLs or log messages.<br>")
            .Append("Generated by StorePulse — <a href=\"https://cartenvo.com/\">cartenvo.com</a></footer></main></body></html>").ToString();
    }

    private static void Tile(StringBuilder html, string value, string label)
        => html.Append("<div class=\"tile\"><b>").Append(HtmlEncoder.Default.Encode(value)).Append("</b><span>")
            .Append(HtmlEncoder.Default.Encode(label)).Append("</span></div>");
}
