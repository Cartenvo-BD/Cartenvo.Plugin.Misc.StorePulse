using System.Reflection;
using Cartenvo.Plugin.Misc.StorePulse.Controllers;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nop.Core;
using Nop.Data;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Web.Framework.Mvc.Filters;
using NUnit.Framework;

namespace StorePulse.Adapter.Tests;

[NonParallelizable]
public class PermissionTests
{
    private sealed class DatabaseFlag : DataSettingsManager
    {
        public static bool? Swap(bool? value) { var previous = _databaseIsInstalled; _databaseIsInstalled = value; return previous; }
    }

    [TestCase("Run", StorePulsePermissions.Run)]
    [TestCase("Run", StorePulsePermissions.View)]
    [TestCase("Export", StorePulsePermissions.Export)]
    [TestCase("Export", StorePulsePermissions.View)]
    [TestCase("ClearHistory", StorePulsePermissions.Manage)]
    [TestCase("ClearHistory", StorePulsePermissions.View)]
    [TestCase("Configure", StorePulsePermissions.View)]
    [TestCase("Settings", StorePulsePermissions.Manage)]
    public async Task MissingRequiredPermissionIsDeniedByNativeFilter(string action, string denied)
        => await VerifyAsync(action, denied, true);

    [TestCase("Run")]
    [TestCase("Export")]
    [TestCase("ClearHistory")]
    [TestCase("Settings")]
    public async Task AllRequiredPermissionsAllowNativeFilter(string action)
        => await VerifyAsync(action, "", false);

    private static async Task VerifyAsync(string action, string denied, bool shouldDeny)
    {
        var previous = DatabaseFlag.Swap(true);
        try
        {
            var http = new DefaultHttpContext();
            http.Request.Method = action is "Run" or "ClearHistory" ? "POST" : "GET";
            http.Request.RouteValues["controller"] = "StorePulse";
            http.Request.RouteValues["action"] = action;
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(service => service.AuthorizeAsync(It.IsAny<string>()))
                .ReturnsAsync((string permission) => permission != denied);
            using var services = new ServiceCollection()
                .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http })
                .AddSingleton(permissions.Object).AddSingleton(Mock.Of<ILocalizationService>())
                .AddSingleton(Mock.Of<IWebHelper>()).BuildServiceProvider();
            var method = typeof(StorePulseController).GetMethods().First(method => method.Name == action);
            var filters = method.GetCustomAttributes<CheckPermissionAttribute>()
                .Select(attribute => attribute.CreateInstance(services)).ToList();
            Assert.That(filters, Is.Not.Empty);
            var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), filters);
            foreach (var filter in filters.Cast<IAsyncAuthorizationFilter>())
                await filter.OnAuthorizationAsync(context);
            if (shouldDeny)
            {
                Assert.That(context.Result, Is.TypeOf<RedirectToActionResult>());
                Assert.That(((RedirectToActionResult)context.Result).ActionName, Is.EqualTo("AccessDenied"));
            }
            else
                Assert.That(context.Result, Is.Null);
        }
        finally { DatabaseFlag.Swap(previous); }
    }
}
