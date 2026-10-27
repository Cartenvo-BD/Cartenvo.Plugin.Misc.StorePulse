// The readiness rules exist only in the 4.80/4.90 builds; 5.00 removed the types they read.
#if !NOP_500
#pragma warning disable CS0618 // the tests set the obsolete PictureId to build old data
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Moq;
using Nop.Core.Domain;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Forums;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.News;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Polls;
using Nop.Data;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Services.Themes;
using NUnit.Framework;

namespace StorePulse.Adapter.Tests;

public class UpgradeReadinessTests
{
    private static IRepository<T> Failing<T>() where T : Nop.Core.BaseEntity
    {
        var repository = new Mock<IRepository<T>>();
        repository.SetupGet(item => item.Table).Throws(new Exception("Server=db;Password=secret Order 'Secret name'"));
        return repository.Object;
    }

    private static IRepository<T> With<T>(params T[] rows) where T : Nop.Core.BaseEntity
    {
        var repository = new Mock<IRepository<T>>();
        repository.SetupGet(item => item.Table).Returns(rows.AsQueryable());
        return repository.Object;
    }

    private static ISettingService Settings(bool forums = false, bool news = false, params string[] payments)
    {
        var settings = new Mock<ISettingService>();
        settings.Setup(service => service.LoadSettingAsync<ForumSettings>(It.IsAny<int>())).ReturnsAsync(new ForumSettings { ForumsEnabled = forums });
        settings.Setup(service => service.LoadSettingAsync<NewsSettings>(It.IsAny<int>())).ReturnsAsync(new NewsSettings { Enabled = news });
        settings.Setup(service => service.LoadSettingAsync<PaymentSettings>(It.IsAny<int>()))
            .ReturnsAsync(new PaymentSettings { ActivePaymentMethodSystemNames = payments.ToList() });
        settings.Setup(service => service.LoadSettingAsync<StoreInformationSettings>(It.IsAny<int>()))
            .ReturnsAsync(new StoreInformationSettings { DefaultStoreTheme = UpgradeReadiness.DefaultTheme });
        return settings.Object;
    }

    private static IThemeProvider Themes(params string[] names)
    {
        var provider = new Mock<IThemeProvider>();
        provider.Setup(item => item.GetThemesAsync()).ReturnsAsync(names
            .Select(name => new ThemeDescriptor { SystemName = name, FriendlyName = name }).ToList());
        return provider.Object;
    }

    private static string Text(string key) =>
        StorePulseResources.All().TryGetValue("Plugins.Misc.StorePulse." + key, out var text) ? text : null;

    private static void AssertTexts(ReadinessItem item)
    {
        Assert.That(Text(item.MessageKey), Is.Not.Null, item.MessageKey);
        Assert.DoesNotThrow(() => string.Format(Text(item.MessageKey), item.Args), item.MessageKey);
    }

    [Test]
    public async Task FailingDataIsUnknownAndNeverLeaksDetails()
    {
        var readiness = new UpgradeReadiness(Failing<ForumTopic>(), Failing<NewsItem>(), Failing<Poll>(), Failing<Order>(),
            Failing<MessageTemplate>(), Failing<LocalizedProperty>(), Failing<ProductAttributeValue>(),
            Failing<ProductAttributeCombination>(), Failing<ProductAttributeValuePicture>(),
            Failing<ProductAttributeCombinationPicture>(), Failing<Picture>(), Settings(), Themes(UpgradeReadiness.DefaultTheme));
        var items = await readiness.RunAsync(1, [], CancellationToken.None);

        Assert.That(items.Select(item => item.Id), Is.EqualTo(UpgradeReadiness.CheckIds));
        foreach (var id in new[] { "UPG-001", "UPG-002", "UPG-003", "UPG-005", "UPG-006", "UPG-010" })
            Assert.That(items.Single(item => item.Id == id).Status, Is.EqualTo(ReadinessStatus.Unknown), id);
        Assert.That(items.Single(item => item.Id == "UPG-004").Status, Is.EqualTo(ReadinessStatus.Ready));
        foreach (var item in items)
        {
            AssertTexts(item);
            foreach (var suffix in new[] { "Title", "Why", "Action" })
                Assert.That(Text($"{item.Id}.{suffix}"), Is.Not.Null, $"{item.Id}.{suffix}");
            Assert.That(Text($"Upgrade.Readiness.Status.{item.Status}"), Is.Not.Null);
        }
        var json = System.Text.Json.JsonSerializer.Serialize(items);
        Assert.That(json, Does.Not.Contain("secret").And.Not.Contain("Secret name"));
    }

    [Test]
    public async Task FindsStoreDataThatNeedsWork()
    {
        var templates = new[]
        {
            new MessageTemplate { Id = 1, Name = "OrderPlaced.CustomerNotification", Subject = "Order", Body = "<p>%Order.CustomValues%</p>" },
            new MessageTemplate { Id = 2, Name = "OrderPaid.StoreOwnerNotification", Subject = "Paid", Body = "%Order.CustomValues.Payment%" },
            new MessageTemplate { Id = 3, Name = "Translated.Only", Subject = "x", Body = "y" }
        };
        var translations = new[]
        {
            new LocalizedProperty { EntityId = 3, LanguageId = 2, LocaleKeyGroup = nameof(MessageTemplate), LocaleKey = "Body", LocaleValue = "%Order.CustomValues%" },
            new LocalizedProperty { EntityId = 2, LanguageId = 2, LocaleKeyGroup = nameof(Product), LocaleKey = "Body", LocaleValue = "%Order.CustomValues%" }
        };
        var readiness = new UpgradeReadiness(With(new ForumTopic { Id = 1 }, new ForumTopic { Id = 2 }), With<NewsItem>(), With<Poll>(),
            With(new Order { Id = 1, CardNumber = "enc" }, new Order { Id = 2, CardNumber = "enc", CardCvv2 = "enc" },
                new Order { Id = 3, CardNumber = "enc", Deleted = true }, new Order { Id = 4 }),
            With(templates), With(translations),
#pragma warning disable CS0618
            With(new ProductAttributeValue { Id = 1, PictureId = 5 }, new ProductAttributeValue { Id = 2, PictureId = 0 }),
#pragma warning restore CS0618
            With(new ProductAttributeCombination { Id = 1, PictureId = 9 }),
            With(new ProductAttributeValuePicture { ProductAttributeValueId = 1, PictureId = 5 }),
            With<ProductAttributeCombinationPicture>(),
            With(new Picture { Id = 5 }),
            Settings(forums: true, news: true, payments: UpgradeReadiness.ManualPaymentSystemName),
            Themes(UpgradeReadiness.DefaultTheme, "Pavilion"));
        var manual = new PluginDescriptor { SystemName = UpgradeReadiness.ManualPaymentSystemName, Installed = true, Author = "nopCommerce team" };
        var items = (await readiness.RunAsync(1, [manual], CancellationToken.None)).ToDictionary(item => item.Id);

        Assert.That(items["UPG-001"].Status, Is.EqualTo(ReadinessStatus.ActionNeeded));
        Assert.That(items["UPG-001"].MessageKey, Is.EqualTo("UPG-001.FoundOn"));
        Assert.That(items["UPG-001"].Args, Is.EqualTo(new object[] { 2 }));
        Assert.That(items["UPG-002"].MessageKey, Is.EqualTo("UPG-002.OnEmpty"));
        Assert.That(items["UPG-003"].Status, Is.EqualTo(ReadinessStatus.Ready));
        Assert.That(items["UPG-004"].Status, Is.EqualTo(ReadinessStatus.ActionNeeded));
        Assert.That(items["UPG-005"].MessageKey, Is.EqualTo("UPG-005.FoundCvv"));
        Assert.That(items["UPG-005"].Args, Is.EqualTo(new object[] { 2, 1 }));
        Assert.That(items["UPG-006"].Status, Is.EqualTo(ReadinessStatus.ActionNeeded));
        Assert.That(items["UPG-006"].Details, Is.EqualTo(new[] { "OrderPlaced.CustomerNotification", "Translated.Only" }));
        Assert.That(items["UPG-008"].MessageKey, Is.EqualTo("UPG-008.Installed"));
        Assert.That(items["UPG-008"].Details, Has.Count.EqualTo(1));
        // Value 1 still points to picture 5, which it already has in the new table; combination 1 points to a
        // picture that no longer exists, so the 5.00 upgrade skips it.
        Assert.That(items["UPG-010"].Status, Is.EqualTo(ReadinessStatus.Check));
        Assert.That(items["UPG-010"].Args, Is.EqualTo(new object[] { 1, 0, 1 }));
        foreach (var item in items.Values)
            AssertTexts(item);
    }

    [Test]
    public void StatusRules()
    {
        Assert.That(UpgradeReadiness.MovedFeature("UPG-003", 0, null).Status, Is.EqualTo(ReadinessStatus.Ready));
        Assert.That(UpgradeReadiness.MovedFeature("UPG-001", 3, false).MessageKey, Is.EqualTo("UPG-001.Found"));
        Assert.That(UpgradeReadiness.ManualPayment(false, true).Status, Is.EqualTo(ReadinessStatus.Ready));
        Assert.That(UpgradeReadiness.ManualPayment(true, false).Status, Is.EqualTo(ReadinessStatus.Check));
        Assert.That(UpgradeReadiness.StoredCards(4, 0).MessageKey, Is.EqualTo("UPG-005.Found"));
        Assert.That(UpgradeReadiness.StoredCards(0, 0).Status, Is.EqualTo(ReadinessStatus.Ready));
        Assert.That(UpgradeReadiness.Themes("Pavilion", ["Pavilion (Pavilion) · default"]).MessageKey, Is.EqualTo("UPG-008.Custom"));
        Assert.That(UpgradeReadiness.Themes("DefaultClean", []).Status, Is.EqualTo(ReadinessStatus.Ready));
        Assert.That(UpgradeReadiness.AttributePictures(0, 0, 0).Status, Is.EqualTo(ReadinessStatus.Ready));
        Assert.That(UpgradeReadiness.AttributePictures(0, 2, 0).Status, Is.EqualTo(ReadinessStatus.Check));

        Assert.That(UpgradeReadiness.Runtime(null).Status, Is.EqualTo(ReadinessStatus.Unknown));
        Assert.That(UpgradeReadiness.Runtime([new Version(9, 0, 16)]).Status, Is.EqualTo(ReadinessStatus.ActionNeeded));
        Assert.That(UpgradeReadiness.Runtime([new Version(10, 0, 3)]).Status, Is.EqualTo(ReadinessStatus.Check));
        var ready = UpgradeReadiness.Runtime([new Version(9, 0, 16), new Version(10, 0, 13)]);
        Assert.That(ready.Status, Is.EqualTo(ReadinessStatus.Ready));
        Assert.That(ready.Args, Is.EqualTo(new object[] { "10.0.13" }));

        Assert.That(UpgradeReadiness.UsesOldToken("a %Order.CustomValues% b"), Is.True);
        Assert.That(UpgradeReadiness.UsesOldToken("%Order.CustomValues.Shipping%"), Is.False);
        Assert.That(UpgradeReadiness.UsesOldToken(null), Is.False);

        Assert.That(UpgradeReadiness.AppliesTo("5.00"), Is.True);
        Assert.That(UpgradeReadiness.AppliesTo("5.10"), Is.True);
        Assert.That(UpgradeReadiness.AppliesTo("4.90"), Is.False);
        Assert.That(UpgradeReadiness.AppliesTo("x"), Is.False);
    }

    [Test]
    public void ScannerFindsChangedApisInPluginCode()
    {
        var uses = PluginApiScanner.Scan(typeof(OldStylePluginCode).Assembly.Location);
        Assert.That(uses, Is.SupersetOf(new[] { "AutoMapper", "RemotePost.Post", "ProductAttributeValue.PictureId" }));
        Assert.That(PluginApiScanner.Scan(typeof(global::StorePulse.Core.HealthScoring).Assembly.Location), Is.Empty);
        Assert.That(PluginApiScanner.Scan(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".dll")), Is.Null);

        var notAssembly = Path.GetTempFileName();
        File.WriteAllText(notAssembly, "not a dll");
        try { Assert.That(PluginApiScanner.Scan(notAssembly), Is.Null); }
        finally { File.Delete(notAssembly); }
    }

    [Test]
    public void RuntimeFinderReadsSharedInstall()
    {
        var root = Path.Combine(Path.GetTempPath(), "sp-runtime-" + Guid.NewGuid());
        try
        {
            var core = Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.NETCore.App", "9.0.16")).FullName;
            foreach (var version in new[] { "9.0.16", "10.0.13", "10.0.0-rc.1.25451.107", "not-a-version" })
                Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.AspNetCore.App", version));

            var found = RuntimeFinder.AspNetCoreVersions(core + Path.DirectorySeparatorChar);
            Assert.That(found, Is.EqualTo(new[] { new Version(9, 0, 16), new Version(10, 0, 0), new Version(10, 0, 13) }));
            Assert.That(RuntimeFinder.AspNetCoreVersions(root), Is.Null, "not a shared install");
            Assert.That(RuntimeFinder.AspNetCoreVersions(null), Is.Null);

            Directory.Delete(Path.Combine(root, "shared", "Microsoft.AspNetCore.App"), true);
            Assert.That(RuntimeFinder.AspNetCoreVersions(core), Is.Empty);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

/// <summary>Never run. Its references let the scanner test find APIs that change in 5.00.</summary>
#pragma warning disable CS0618 // PictureId is obsolete; referencing it is the point
internal static class OldStylePluginCode
{
    public static Type Mapper() => typeof(AutoMapper.Profile);
    public static void Post() => new Nop.Web.Framework.RemotePost(null, null).Post();
    public static int? Picture(ProductAttributeValue value) => value.PictureId;
}
#pragma warning restore CS0618
#else
using Cartenvo.Plugin.Misc.StorePulse.Services;
using NUnit.Framework;

namespace StorePulse.Adapter.Tests;

public class UpgradeReadinessTests
{
    [Test]
    public async Task NothingToPrepareOn500()
    {
        Assert.That(UpgradeReadiness.AppliesTo("5.10"), Is.False);
        Assert.That(await new UpgradeReadiness().RunAsync(1, [], CancellationToken.None), Is.Empty);
        Assert.That(PluginApiScanner.Scan(typeof(global::StorePulse.Core.HealthScoring).Assembly.Location), Is.Empty);
    }
}
#endif
