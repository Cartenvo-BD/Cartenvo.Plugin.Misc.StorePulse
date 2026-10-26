using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Services;

namespace Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

public class StorePulseStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ScanHistory>();
        services.AddScoped<ExtendedDiagnostics>();
        services.AddScoped<StorePulseScanner>();
        services.AddScoped<StorePulseSchedule>();
    }
    public void Configure(IApplicationBuilder application) { }
    public int Order => 3000;
}
