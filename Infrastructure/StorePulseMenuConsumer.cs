using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

public class StorePulseMenuConsumer(IPluginManager<IPlugin> pluginManager,
    IPermissionService permissions, ILocalizationService localization)
    : BaseAdminMenuCreatedEventConsumer(pluginManager)
{
    protected override string PluginSystemName => "Misc.StorePulse";
    protected override Task<bool> CheckAccessAsync() => permissions.AuthorizeAsync(StorePulsePermissions.View);

    protected override async Task<AdminMenuItem> GetAdminMenuItemAsync(IPlugin plugin) => new()
    {
        SystemName = PluginSystemName,
        Title = await localization.GetResourceAsync("Plugins.Misc.StorePulse.Title"),
        IconClass = "fas fa-heartbeat",
        Visible = true,
        Url = plugin.GetConfigurationPageUrl()
    };

    // The base consumer checks installation and permission before adding the item.
    // Insert before the native System information entry within the System menu.
    protected override string BeforeMenuSystemName => "System information";
}
