using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Core.Tests;

public class HtmlReportTests
{
    [Test]
    public void TranslatedTextAndIdentifiersCannotInjectMarkup()
    {
        var result = new HealthCheckResult("<script>alert(1)</script>", HealthCategory.Email, HealthScope.System,
            HealthStatus.Unknown, "title", "summary", "action", DateTimeOffset.UtcNow,
            new Dictionary<string, decimal> { ["count"] = 1 });
        var html = HtmlReport.Render([result], _ => "<img src=x onerror=alert(1)>");
        Assert.That(html, Does.Not.Contain("<script>").And.Not.Contain("<img"));
        Assert.That(html, Does.Contain("&lt;script&gt;").And.Contain("&lt;img"));
        Assert.That(html, Does.Contain("<b>Unknown</b>"));
    }
}
