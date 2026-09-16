using Microsoft.AspNetCore.SignalR;

namespace Harborline.App.Blazor.ReferenceHost.Transport;

public static class SelectedSessionHosting
{
    public static IServiceCollection AddSelectedSessionBrowser(this IServiceCollection services)
    {
        // Browser-owned catalogue JSON and exported bytes return through JS interop. Keep a
        // bounded allowance above the proxy's 10 MiB payload plus serialization/framing overhead;
        // SignalR's default 32 KiB would disconnect otherwise valid interactive requests.
        services.Configure<HubOptions>(options => options.MaximumReceiveMessageSize = 16L * 1024 * 1024);
        services.AddScoped<BrowserSelectedSessionTransport>();
        return services;
    }
}
