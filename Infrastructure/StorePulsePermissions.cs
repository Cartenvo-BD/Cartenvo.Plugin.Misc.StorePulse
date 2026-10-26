using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

public class StorePulsePermissions : IPermissionConfigManager
{
    public const string View = "StorePulse.View";
    public const string Run = "StorePulse.Run";
    public const string Export = "StorePulse.Export";
    public const string Manage = "StorePulse.Manage";

    public IList<PermissionConfig> AllConfigs =>
    [
        new("Admin area. View StorePulse", View, "StorePulse", NopCustomerDefaults.AdministratorsRoleName),
        new("Admin area. Run StorePulse scans", Run, "StorePulse", NopCustomerDefaults.AdministratorsRoleName),
        new("Admin area. Export StorePulse reports", Export, "StorePulse", NopCustomerDefaults.AdministratorsRoleName),
        new("Admin area. Manage StorePulse", Manage, "StorePulse", NopCustomerDefaults.AdministratorsRoleName)
    ];
}
