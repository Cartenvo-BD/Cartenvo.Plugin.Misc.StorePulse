using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using NUnit.Framework;

namespace StorePulse.Adapter.Tests;

public class HelpTests
{
    private static IEnumerable<string> CheckIds() => StorePulseResources.Checks.Select(check => check.Id);

    [Test]
    public void EveryCheckHasPopupHelp()
    {
        Assert.That(StorePulseHelp.All.Select(entry => entry.Id), Is.EquivalentTo(CheckIds()));
        foreach (var entry in StorePulseHelp.All)
        {
            Assert.That(entry.Checks, Is.Not.Empty, entry.Id);
            Assert.That(entry.Causes, Is.Not.Empty, entry.Id);
            Assert.That(entry.Steps, Is.Not.Empty, entry.Id);
            Assert.That(entry.Ignore, Is.Not.Empty, entry.Id);
            // Items are stored joined by the separator, so they must not contain it.
            Assert.That(entry.Causes.Concat(entry.Steps).Any(item => item.Contains(StorePulseHelp.ItemSeparator)), Is.False, entry.Id);
        }
    }

    [Test]
    public void HelpIsRegisteredAsResources()
    {
        var resources = StorePulseResources.All();
        foreach (var id in CheckIds())
            foreach (var part in new[] { "Checks", "Causes", "Steps", "Ignore" })
                Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.{id}.Help.{part}"), id + " " + part);
    }

    [Test]
    public void EveryCheckLinksToAGuideSectionWithTracking()
    {
        foreach (var id in CheckIds())
        {
            var url = StorePulseLinks.Guide(id);
            Assert.That(url, Does.StartWith(StorePulseLinks.GuideBase), id);
            Assert.That(url, Does.Contain("utm_source=storepulse"), id);
            // The anchor comes after the query string, or browsers drop the tracking.
            Assert.That(url, Does.EndWith("#" + id.ToLowerInvariant()), id);
        }
        Assert.That(StorePulseLinks.Guide("UNKNOWN-1"), Is.Null);
    }
}
