using Harborline.UIAdapters.Blazor;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.App.Blazor.ReferenceHost.Transport;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped<BrowserSelectedSessionTransport>();
// The platform UI package ships its own DI registration. Its app layout injects
// IMediaQueryObserver, so composing platform components without this throws at render.
builder.Services.AddHarborlineUiAdapters();

// Ticket 093. The local node's listener gate is gate-all-by-default, so an admin HTTP client that
// presents nothing gets 401 and the surface shows an error where data should be. This lane is
// server-rendered, so the credential stays in server configuration and never reaches the browser.
// Set LocalNode:SessionToken (env: LocalNode__SessionToken) to the same per-boot token the node was
// started with. Absent, the clients behave exactly as before - which is correct against a node
// running in un-enforced dev mode.
var localNodeSessionToken = builder.Configuration["LocalNode:SessionToken"];
void ConfigureNodeClient(HttpClient client, string baseUrl)
{
    client.BaseAddress = new Uri(baseUrl);
    if (!string.IsNullOrWhiteSpace(localNodeSessionToken))
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", localNodeSessionToken);
    }
}
var workshopBaseUrl = builder.Configuration["Workshop:BaseUrl"];
if (!string.IsNullOrWhiteSpace(workshopBaseUrl))
{
    builder.Services.AddSingleton(_ => new SelectedSessionProxy(new Uri(workshopBaseUrl)));
    builder.Services.AddHttpClient<IWorkshopCatalogueClient, HttpWorkshopCatalogueClient>(
        client => ConfigureNodeClient(client, workshopBaseUrl));
}
else
{
    throw new InvalidOperationException(
        "Workshop catalogue client is not configured: set Workshop:BaseUrl (env: Workshop__BaseUrl) to the local node origin.");
}
// Authorization writes are security-sensitive, so its fixture is an explicit opt-in.
// The HTTP client uses ConfigureNodeClient and therefore the existing server-side session
// credential; this surface introduces no second token source.
var authorizationAdminBaseUrl = builder.Configuration["AuthorizationAdmin:BaseUrl"];
if (!string.IsNullOrWhiteSpace(authorizationAdminBaseUrl))
{
    builder.Services.AddScoped<IPackNavigationClient, BrowserPackNavigationClient>();
    builder.Services.AddHttpClient<IAuthorizationAdminClient, HttpAuthorizationAdminClient>(
        client => ConfigureNodeClient(client, authorizationAdminBaseUrl));
}
else if (builder.Configuration.GetValue<bool>("AuthorizationAdmin:UseFixture"))
{
    builder.Services.AddSingleton<IAuthorizationAdminClient, FixtureAuthorizationAdminClient>();
    builder.Services.AddSingleton<IPackNavigationClient, FixturePackNavigationClient>();
}
else
{
    throw new InvalidOperationException(
        "Authorization admin client is not configured: set AuthorizationAdmin:BaseUrl (env: AuthorizationAdmin__BaseUrl) "
        + "to the local node origin, or set AuthorizationAdmin:UseFixture=true to explicitly opt in to "
        + "the serverless fixture client.");
}

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapMethods(SelectedSessionProxy.Route, ["GET", "POST", "PUT", "PATCH", "DELETE"],
    (HttpContext context, SelectedSessionProxy proxy) => proxy.ForwardAsync(context));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
