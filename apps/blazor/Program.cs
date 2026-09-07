using Harborline.UIAdapters.Blazor;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Forms;
using Harborline.App.Blazor.ReferenceHost.Admin.Reports;
using Harborline.App.Blazor.ReferenceHost.Admin.Views;
using Harborline.App.Blazor.ReferenceHost.Admin.DataExchange;
using Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
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
// Ticket 153 (L1351): the Forms fixture is an EXPLICIT opt-in (FormsAdmin:UseFixture), never
// a silent fallback — a fixture that mints successful "writes" with no server must be
// impossible to ship unnoticed. appsettings.Development.json opts in for standalone dev runs;
// a non-Development host without a BaseUrl fails loudly at startup.
var formsAdminBaseUrl = builder.Configuration["FormsAdmin:BaseUrl"];
if (!string.IsNullOrWhiteSpace(formsAdminBaseUrl))
{
    builder.Services.AddHttpClient<IFormsAdminClient, HttpFormsAdminClient>(
        client => ConfigureNodeClient(client, formsAdminBaseUrl));
}
else if (builder.Configuration.GetValue<bool>("FormsAdmin:UseFixture"))
{
    builder.Services.AddSingleton<IFormsAdminClient, FixtureFormsAdminClient>();
}
else
{
    throw new InvalidOperationException(
        "Forms admin client is not configured: set FormsAdmin:BaseUrl (env: FormsAdmin__BaseUrl) "
        + "to the local node origin, or set FormsAdmin:UseFixture=true to explicitly opt in to "
        + "the serverless fixture client.");
}

// A configured base URL activates the HTTP client; the fixture singleton is the standalone default.
var reportsAdminBaseUrl = builder.Configuration["ReportsAdmin:BaseUrl"];
if (!string.IsNullOrWhiteSpace(reportsAdminBaseUrl))
{
    builder.Services.AddHttpClient<IReportsAdminClient, HttpReportsAdminClient>(
        client => ConfigureNodeClient(client, reportsAdminBaseUrl));
}
else
{
    builder.Services.AddSingleton<IReportsAdminClient, FixtureReportsAdminClient>();
}

// A configured base URL activates the HTTP client; the fixture singleton is the standalone default.
var viewsAdminBaseUrl = builder.Configuration["ViewsAdmin:BaseUrl"];
if (!string.IsNullOrWhiteSpace(viewsAdminBaseUrl))
{
    builder.Services.AddHttpClient<IViewsAdminClient, HttpViewsAdminClient>(
        client => ConfigureNodeClient(client, viewsAdminBaseUrl));
}
else
{
    builder.Services.AddSingleton<IViewsAdminClient, FixtureViewsAdminClient>();
}

// A configured base URL activates the HTTP client; the fixture singleton is the standalone default.
var dataExchangeAdminBaseUrl = builder.Configuration["DataExchangeAdmin:BaseUrl"];
if (!string.IsNullOrWhiteSpace(dataExchangeAdminBaseUrl))
{
    builder.Services.AddHttpClient<IDataExchangeAdminClient, HttpDataExchangeAdminClient>(
        client => ConfigureNodeClient(client, dataExchangeAdminBaseUrl));
}
else
{
    builder.Services.AddSingleton<IDataExchangeAdminClient, FixtureDataExchangeAdminClient>();
}

// A configured base URL activates the HTTP client; the fixture singleton is the standalone default.
var schedulingAdminBaseUrl = builder.Configuration["SchedulingAdmin:BaseUrl"];
if (!string.IsNullOrWhiteSpace(schedulingAdminBaseUrl))
{
    builder.Services.AddHttpClient<ISchedulingAdminClient, HttpSchedulingAdminClient>(
        client => ConfigureNodeClient(client, schedulingAdminBaseUrl));
}
else
{
    builder.Services.AddSingleton<ISchedulingAdminClient, FixtureSchedulingAdminClient>();
}

// Authorization writes are security-sensitive, so its fixture is an explicit opt-in just like
// Forms. The HTTP client uses ConfigureNodeClient and therefore the existing server-side session
// credential; this surface introduces no second token source.
var authorizationAdminBaseUrl = builder.Configuration["AuthorizationAdmin:BaseUrl"];
if (!string.IsNullOrWhiteSpace(authorizationAdminBaseUrl))
{
    builder.Services.AddHttpClient<IPackNavigationClient, HttpPackNavigationClient>(
        client => ConfigureNodeClient(client, authorizationAdminBaseUrl));
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
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
