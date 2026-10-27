using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using LinqToDB;
using Nop.Core.Domain;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
#if !NOP_500
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Forums;
using Nop.Core.Domain.News;
using Nop.Core.Domain.Polls;
#endif
using Nop.Data;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Services.Themes;

namespace Cartenvo.Plugin.Misc.StorePulse.Services;

public enum ReadinessStatus { ActionNeeded, Check, Ready, Unknown }

/// <summary>
/// One item of the "Before you upgrade to 5.00" list. <paramref name="MessageKey"/> is a resource key under
/// Plugins.Misc.StorePulse., formatted with <paramref name="Args"/>. <paramref name="Details"/> are names (templates,
/// plugins, themes) shown as a list.
/// </summary>
public sealed record ReadinessItem(string Id, ReadinessStatus Status, string MessageKey, object[] Args,
    IReadOnlyList<string> Details, bool MoreDetails, string AdminPath);

#if NOP_500
/// <summary>
/// The 5.00 build: the store is already on 5.00, so there's nothing to prepare and the page never shows the list.
/// The 4.80/4.90 rules read types that 5.00 removed (forums, news, polls, attribute PictureId).
/// </summary>
public class UpgradeReadiness
{
    public static readonly IReadOnlyList<string> CheckIds = [];

    public static bool AppliesTo(string targetVersion) => false;

    public Task<IReadOnlyList<ReadinessItem>> RunAsync(int storeId, IEnumerable<PluginDescriptor> plugins,
        CancellationToken token) => Task.FromResult<IReadOnlyList<ReadinessItem>>([]);
}
#else
/// <summary>
/// Things in this store that need work before or after an upgrade to nopCommerce 5.00. Read live when the Upgrade
/// readiness page opens; nothing is stored or changed, and the health score isn't affected.
/// See docs/storepulse-upgrade-readiness.md for the source of each rule.
/// </summary>
public class UpgradeReadiness(
    IRepository<ForumTopic> forumTopics,
    IRepository<NewsItem> newsItems,
    IRepository<Poll> polls,
    IRepository<Order> orders,
    IRepository<MessageTemplate> messageTemplates,
    IRepository<LocalizedProperty> localizedProperties,
    IRepository<ProductAttributeValue> attributeValues,
    IRepository<ProductAttributeCombination> attributeCombinations,
    IRepository<ProductAttributeValuePicture> valuePictures,
    IRepository<ProductAttributeCombinationPicture> combinationPictures,
    IRepository<Picture> pictures,
    ISettingService settingService,
    IThemeProvider themeProvider)
{
    public const string ManualPaymentSystemName = "Payments.Manual";
    public const string DefaultTheme = "DefaultClean";
    public const string OldCustomValuesToken = "%Order.CustomValues%";
    public const int ListLimit = 50;

    /// <summary>Counts stop here, so a huge table is never counted in full.</summary>
    public const int CountLimit = 100_000;

    /// <summary>5.00.0's release notes require this ASP.NET Core runtime.</summary>
    public static readonly Version RequiredRuntime = new(10, 0, 12);

    public static readonly TimeSpan RuleTimeout = TimeSpan.FromSeconds(10);

    public static readonly IReadOnlyList<string> CheckIds =
        ["UPG-001", "UPG-002", "UPG-003", "UPG-004", "UPG-005", "UPG-006", "UPG-007", "UPG-008", "UPG-009", "UPG-010"];

    /// <summary>The list is shown for targets from 5.00 on.</summary>
    public static bool AppliesTo(string targetVersion) =>
        Version.TryParse(targetVersion, out var target) && target.Major >= 5;

    private sealed record Context(int StoreId, IReadOnlyList<PluginDescriptor> Plugins, CancellationToken Token);

    /// <summary>Runs every rule. A rule that fails or times out is reported as Unknown; the others still run.</summary>
    public async Task<IReadOnlyList<ReadinessItem>> RunAsync(int storeId, IEnumerable<PluginDescriptor> plugins,
        CancellationToken token)
    {
        var installed = (plugins ?? []).Where(plugin => plugin.Installed).ToList();
        var rules = new (string Id, Func<Context, Task<ReadinessItem>> Run)[]
        {
            ("UPG-001", ForumsAsync), ("UPG-002", NewsAsync), ("UPG-003", PollsAsync), ("UPG-004", ManualPaymentAsync),
            ("UPG-005", StoredCardsAsync), ("UPG-006", TemplatesAsync), ("UPG-007", PluginCodeAsync),
            ("UPG-008", ThemesAsync), ("UPG-009", RuntimeAsync), ("UPG-010", AttributePicturesAsync)
        };
        var results = new List<ReadinessItem>();
        foreach (var (id, run) in rules)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(RuleTimeout);
            try
            {
                results.Add(await run(new Context(storeId, installed, timeout.Token)));
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                // Exception text can contain connection strings or data, so it's never shown.
                results.Add(Unknown(id, "Upgrade.Readiness.Failed"));
            }
        }
        return results;
    }

    private static ReadinessItem Item(string id, ReadinessStatus status, string message, params object[] args) =>
        new(id, status, message, args, [], false, Links(id));

    private static ReadinessItem Unknown(string id, string message) =>
        new(id, ReadinessStatus.Unknown, message, [], [], false, Links(id));

    private static ReadinessItem WithList(string id, ReadinessStatus status, string message, IReadOnlyList<string> names,
        params object[] args) =>
        new(id, status, message, args, names.Take(ListLimit).ToArray(), names.Count > ListLimit, Links(id));

    public static string Links(string id) => id switch
    {
        "UPG-001" => "Admin/Setting/Forum",
        "UPG-002" => "Admin/News/NewsItems",
        "UPG-003" => "Admin/Poll/List",
        "UPG-004" => "Admin/Payment/Methods",
        "UPG-006" => "Admin/MessageTemplate/List",
        "UPG-007" => "Admin/Plugin/List",
        "UPG-008" => "Admin/Setting/GeneralCommon",
        _ => null
    };

    private static Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken token) =>
        query.Take(CountLimit).CountAsync(token);

    // UPG-001 to UPG-003: 5.00 moves forums, news and polls into plugins. The tables stay, so nothing is lost,
    // but the content only shows again once the plugin is installed.
    private async Task<ReadinessItem> ForumsAsync(Context context)
    {
        var topics = await CountAsync(forumTopics.Table, context.Token);
        var enabled = (await settingService.LoadSettingAsync<ForumSettings>(context.StoreId)).ForumsEnabled;
        return MovedFeature("UPG-001", topics, enabled);
    }

    private async Task<ReadinessItem> NewsAsync(Context context)
    {
        var items = await CountAsync(newsItems.Table, context.Token);
        var enabled = (await settingService.LoadSettingAsync<NewsSettings>(context.StoreId)).Enabled;
        return MovedFeature("UPG-002", items, enabled);
    }

    private async Task<ReadinessItem> PollsAsync(Context context) =>
        MovedFeature("UPG-003", await CountAsync(polls.Table, context.Token), null);

    /// <summary>Content or a switched-on feature means the plugin is needed after the upgrade.</summary>
    public static ReadinessItem MovedFeature(string id, int rows, bool? enabled)
    {
        if (rows > 0)
            return Item(id, ReadinessStatus.ActionNeeded, enabled == true ? $"{id}.FoundOn" : $"{id}.Found", rows);
        return enabled == true
            ? Item(id, ReadinessStatus.ActionNeeded, $"{id}.OnEmpty")
            : Item(id, ReadinessStatus.Ready, $"{id}.None");
    }

    // UPG-004: Payments.Manual doesn't ship with 5.00.
    private async Task<ReadinessItem> ManualPaymentAsync(Context context)
    {
        var plugin = context.Plugins.FirstOrDefault(item =>
            string.Equals(item.SystemName, ManualPaymentSystemName, StringComparison.OrdinalIgnoreCase));
        var active = (await settingService.LoadSettingAsync<PaymentSettings>(context.StoreId)).ActivePaymentMethodSystemNames
            .Any(name => string.Equals(name, ManualPaymentSystemName, StringComparison.OrdinalIgnoreCase));
        return ManualPayment(plugin != null, active);
    }

    public static ReadinessItem ManualPayment(bool installed, bool active) => !installed
        ? Item("UPG-004", ReadinessStatus.Ready, "UPG-004.None")
        : active
            ? Item("UPG-004", ReadinessStatus.ActionNeeded, "UPG-004.Active")
            : Item("UPG-004", ReadinessStatus.Check, "UPG-004.Installed");

    // UPG-005: counts only; card values are never read.
    private async Task<ReadinessItem> StoredCardsAsync(Context context)
    {
        var withNumber = await CountAsync(orders.Table.Where(order => !order.Deleted &&
            order.CardNumber != null && order.CardNumber != ""), context.Token);
        var withCvv = await CountAsync(orders.Table.Where(order => !order.Deleted &&
            order.CardCvv2 != null && order.CardCvv2 != ""), context.Token);
        return StoredCards(withNumber, withCvv);
    }

    public static ReadinessItem StoredCards(int withNumber, int withCvv) => withCvv > 0
        ? Item("UPG-005", ReadinessStatus.Check, "UPG-005.FoundCvv", withNumber, withCvv)
        : withNumber > 0
            ? Item("UPG-005", ReadinessStatus.Check, "UPG-005.Found", withNumber)
            : Item("UPG-005", ReadinessStatus.Ready, "UPG-005.None");

    // UPG-006: the database filter has no "%", so LIKE wildcards can't change the result; the exact token is
    // matched in memory.
    private async Task<ReadinessItem> TemplatesAsync(Context context)
    {
        const string probe = "Order.CustomValues";
        var templates = await messageTemplates.Table
            .Where(template => template.Body.Contains(probe) || template.Subject.Contains(probe))
            .Select(template => new { template.Id, template.Name, template.Subject, template.Body })
            .Take(500).ToListAsync(context.Token);
        var names = templates.Where(template => UsesOldToken(template.Subject) || UsesOldToken(template.Body))
            .ToDictionary(template => template.Id, template => template.Name);

        var localized = await localizedProperties.Table
            .Where(property => property.LocaleKeyGroup == nameof(MessageTemplate) &&
                (property.LocaleKey == nameof(MessageTemplate.Body) || property.LocaleKey == nameof(MessageTemplate.Subject)) &&
                property.LocaleValue.Contains(probe))
            .Select(property => new { property.EntityId, property.LanguageId, property.LocaleValue })
            .Take(2000).ToListAsync(context.Token);
        var translated = localized.Where(property => UsesOldToken(property.LocaleValue))
            .Select(property => property.EntityId).Distinct().Where(id => !names.ContainsKey(id)).ToArray();
        if (translated.Length > 0)
        {
            foreach (var template in await messageTemplates.Table.Where(template => translated.Contains(template.Id))
                         .Select(template => new { template.Id, template.Name }).ToListAsync(context.Token))
                names[template.Id] = template.Name;
        }

        var list = names.Values.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        return list.Count == 0
            ? Item("UPG-006", ReadinessStatus.Ready, "UPG-006.None")
            : WithList("UPG-006", ReadinessStatus.ActionNeeded, "UPG-006.Found", list, list.Count);
    }

    public static bool UsesOldToken(string text) =>
        !string.IsNullOrEmpty(text) && text.Contains(OldCustomValuesToken, StringComparison.Ordinal);

    // UPG-007: plugin DLLs are read as metadata only; nothing is loaded or run.
    private Task<ReadinessItem> PluginCodeAsync(Context context)
    {
        var hits = new List<string>();
        var unreadable = 0;
        var scanned = 0;
        foreach (var plugin in context.Plugins.Where(plugin => !IsBundled(plugin) && !IsSelf(plugin))
                     .OrderBy(plugin => plugin.FriendlyName, StringComparer.OrdinalIgnoreCase).Take(300))
        {
            context.Token.ThrowIfCancellationRequested();
            var uses = PluginApiScanner.Scan(plugin.OriginalAssemblyFile);
            if (uses == null)
            {
                unreadable++;
                continue;
            }
            scanned++;
            if (uses.Count > 0)
                hits.Add($"{plugin.FriendlyName} ({plugin.SystemName}): {string.Join(", ", uses)}");
        }
        ReadinessItem result = hits.Count > 0
            ? WithList("UPG-007", ReadinessStatus.ActionNeeded, "UPG-007.Found", hits, hits.Count, scanned)
            : unreadable > 0
                ? Item("UPG-007", ReadinessStatus.Unknown, "UPG-007.Unreadable", unreadable, scanned)
                : Item("UPG-007", ReadinessStatus.Ready, "UPG-007.None", scanned);
        return Task.FromResult(result);
    }

    public static bool IsBundled(PluginDescriptor plugin) =>
        (plugin.Author ?? "").Contains("nopCommerce team", StringComparison.OrdinalIgnoreCase);

    private static bool IsSelf(PluginDescriptor plugin) =>
        string.Equals(plugin.SystemName, StorePulsePlugin.SystemName, StringComparison.OrdinalIgnoreCase);

    // UPG-008: themes have no supported-version field, so any theme besides DefaultClean needs the vendor's word.
    private async Task<ReadinessItem> ThemesAsync(Context context)
    {
        var current = (await settingService.LoadSettingAsync<StoreInformationSettings>(context.StoreId)).DefaultStoreTheme;
        var others = (await themeProvider.GetThemesAsync())
            .Where(theme => !string.Equals(theme.SystemName, DefaultTheme, StringComparison.OrdinalIgnoreCase))
            .Select(theme => string.Equals(theme.SystemName, current, StringComparison.OrdinalIgnoreCase)
                ? $"{theme.FriendlyName} ({theme.SystemName}) · default"
                : $"{theme.FriendlyName} ({theme.SystemName})")
            .ToList();
        return Themes(current, others);
    }

    public static ReadinessItem Themes(string currentTheme, IReadOnlyList<string> otherThemes)
    {
        var usesCustom = !string.IsNullOrEmpty(currentTheme) &&
            !string.Equals(currentTheme, DefaultTheme, StringComparison.OrdinalIgnoreCase);
        if (usesCustom)
            return WithList("UPG-008", ReadinessStatus.Check, "UPG-008.Custom", otherThemes, currentTheme);
        return otherThemes.Count > 0
            ? WithList("UPG-008", ReadinessStatus.Check, "UPG-008.Installed", otherThemes, otherThemes.Count)
            : Item("UPG-008", ReadinessStatus.Ready, "UPG-008.None");
    }

    // UPG-009: looks for ASP.NET Core 10 next to the runtime the store runs on.
    private Task<ReadinessItem> RuntimeAsync(Context context) =>
        Task.FromResult(Runtime(RuntimeFinder.AspNetCoreVersions(RuntimeEnvironment.GetRuntimeDirectory())));

    public static ReadinessItem Runtime(IReadOnlyList<Version> installed)
    {
        if (installed == null)
            return Item("UPG-009", ReadinessStatus.Unknown, "UPG-009.Unknown");
        var latest10 = installed.Where(version => version.Major == 10).DefaultIfEmpty().Max();
        if (latest10 == null)
            return Item("UPG-009", ReadinessStatus.ActionNeeded, "UPG-009.Missing", RequiredRuntime.ToString(3));
        return latest10 >= RequiredRuntime
            ? Item("UPG-009", ReadinessStatus.Ready, "UPG-009.Found", latest10.ToString(3))
            : Item("UPG-009", ReadinessStatus.Check, "UPG-009.Old", latest10.ToString(3), RequiredRuntime.ToString(3));
    }

    // UPG-010: since 4.70 attribute pictures live in ProductAttributeValuePicture / ProductAttributeCombinationPicture.
    // The 4.70 upgrade copied the old PictureId there but cleared it with an UpdateEntitiesAsync call it never awaited,
    // so some stores still have it. 5.00 (MultiplePicturesMigration, #8270) copies every PictureId that points to an
    // existing picture again, without checking the new tables, and then drops the column.
#pragma warning disable CS0618 // PictureId is obsolete since 4.70; finding the leftovers is the point of this rule
    private async Task<ReadinessItem> AttributePicturesAsync(Context context)
    {
        var leftoverValues = attributeValues.Table.Where(value => value.PictureId > 0 &&
            pictures.Table.Any(picture => picture.Id == value.PictureId));
        var leftoverCombinations = attributeCombinations.Table.Where(item => item.PictureId > 0 &&
            pictures.Table.Any(picture => picture.Id == item.PictureId));
        var values = await CountAsync(leftoverValues, context.Token);
        var combinations = await CountAsync(leftoverCombinations, context.Token);
        if (values + combinations == 0)
            return AttributePictures(0, 0, 0);
        var duplicates = await CountAsync(leftoverValues.Where(value => valuePictures.Table.Any(mapping =>
                mapping.ProductAttributeValueId == value.Id && mapping.PictureId == value.PictureId)), context.Token)
            + await CountAsync(leftoverCombinations.Where(item => combinationPictures.Table.Any(mapping =>
                mapping.ProductAttributeCombinationId == item.Id && mapping.PictureId == item.PictureId)), context.Token);
        return AttributePictures(values, combinations, duplicates);
    }
#pragma warning restore CS0618

    public static ReadinessItem AttributePictures(int values, int combinations, int duplicates) => values + combinations > 0
        ? Item("UPG-010", ReadinessStatus.Check, "UPG-010.Found", values, combinations, duplicates)
        : Item("UPG-010", ReadinessStatus.Ready, "UPG-010.None");
}
#endif

/// <summary>Finds APIs a plugin uses that changed or were removed in nopCommerce 5.00, from the DLL's metadata.</summary>
public static class PluginApiScanner
{
    public const long MaxFileBytes = 64L * 1024 * 1024;

    private static readonly HashSet<string> MovedNamespaces =
    [
        "Nop.Core.Domain.Forums", "Nop.Core.Domain.News", "Nop.Core.Domain.Polls",
        "Nop.Services.Forums", "Nop.Services.News", "Nop.Services.Polls"
    ];

    /// <summary>
    /// What the assembly uses, as short labels in a fixed order. Empty when nothing was found; null when the file is
    /// missing, too large or not a .NET assembly.
    /// </summary>
    public static IReadOnlyList<string> Scan(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || new FileInfo(path).Length > MaxFileBytes)
                return null;
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                return null;
            return Scan(pe.GetMetadataReader());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException
                                              or InvalidOperationException)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> Scan(MetadataReader reader)
    {
        var found = new HashSet<string>();
        foreach (var handle in reader.AssemblyReferences)
        {
            if (reader.GetString(reader.GetAssemblyReference(handle).Name) == "AutoMapper")
                found.Add("AutoMapper");
        }
        foreach (var handle in reader.TypeReferences)
        {
            var (ns, name) = Name(reader, handle);
            if (ns == "Nop.Core.Infrastructure.Mapper" && name == "AutoMapperConfiguration")
                found.Add("AutoMapperConfiguration");
            if (MovedNamespaces.Contains(ns))
                found.Add("Forums/News/Polls");
        }
        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference)
                continue;
            var (ns, type) = Name(reader, (TypeReferenceHandle)member.Parent);
            var name = reader.GetString(member.Name);
            if (ns == "Nop.Web.Framework" && type == "RemotePost" && name is "Post" or "get_Method" or "set_Method")
                found.Add("RemotePost.Post");
            if (ns == "Nop.Core.Domain.Catalog" && type is "ProductAttributeValue" or "ProductAttributeCombination" &&
                name is "get_PictureId" or "set_PictureId")
                found.Add($"{type}.PictureId");
        }
        string[] order = ["AutoMapper", "AutoMapperConfiguration", "RemotePost.Post", "ProductAttributeValue.PictureId",
            "ProductAttributeCombination.PictureId", "Forums/News/Polls"];
        return order.Where(found.Contains).ToArray();
    }

    private static (string Namespace, string Name) Name(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        return (reader.GetString(type.Namespace), reader.GetString(type.Name));
    }
}

/// <summary>Lists the ASP.NET Core runtimes installed next to the .NET runtime the store runs on.</summary>
public static class RuntimeFinder
{
    /// <summary>
    /// <paramref name="runtimeDirectory"/> is …/shared/Microsoft.NETCore.App/&lt;version&gt;/. Returns null when the
    /// layout isn't a shared install (for example a self-contained app), so the result is unknown rather than missing.
    /// </summary>
    public static IReadOnlyList<Version> AspNetCoreVersions(string runtimeDirectory)
    {
        try
        {
            if (string.IsNullOrEmpty(runtimeDirectory))
                return null;
            var versionDirectory = new DirectoryInfo(runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var framework = versionDirectory.Parent;
            if (framework == null || !string.Equals(framework.Name, "Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) ||
                framework.Parent == null)
                return null;
            var aspNet = new DirectoryInfo(Path.Combine(framework.Parent.FullName, "Microsoft.AspNetCore.App"));
            if (!aspNet.Exists)
                return [];
            return aspNet.GetDirectories()
                .Select(directory => Version.TryParse(directory.Name.Split('-')[0], out var version) ? version : null)
                .Where(version => version != null).OrderBy(version => version).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
