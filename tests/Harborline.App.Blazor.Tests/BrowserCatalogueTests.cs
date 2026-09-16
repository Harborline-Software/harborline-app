using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.App.Blazor.ReferenceHost.Transport;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Harborline.App.Blazor.Tests;

public sealed class BrowserCatalogueTests : BunitContext
{
    [Fact]
    public void Host_registration_bounds_selected_session_responses_above_the_default_32KiB_limit()
    {
        var services = new ServiceCollection();
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddSelectedSessionBrowser();
        using var provider = services.BuildServiceProvider();
        Assert.Equal(16L * 1024 * 1024, provider.GetRequiredService<IOptions<HubOptions>>().Value.MaximumReceiveMessageSize);
        var registeredHubOptions = services.Where(descriptor => descriptor.ServiceType.IsGenericType)
            .SelectMany(descriptor => descriptor.ServiceType.GenericTypeArguments)
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HubOptions<>)).Distinct().ToArray();
        Assert.NotEmpty(registeredHubOptions);
        foreach (var hubOptions in registeredHubOptions)
        {
            var optionsInterface = typeof(IOptions<>).MakeGenericType(hubOptions);
            var value = Assert.IsAssignableFrom<HubOptions>(optionsInterface.GetProperty("Value")!.GetValue(provider.GetRequiredService(optionsInterface)));
            Assert.Equal(16L * 1024 * 1024, value.MaximumReceiveMessageSize);
        }
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(BrowserSelectedSessionTransport)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task Explicit_retry_after_module_import_timeout_does_not_reuse_the_cancelled_import()
    {
        var browser = new Browser(new SelectedSessionResponse(200, "{}", null)) { CancelFirstImport = true };
        await using var transport = new BrowserSelectedSessionTransport(browser);
        var client = new BrowserWorkshopCatalogueClient(transport);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadJsonAsync("api/local-node/catalogue/definitions"));
        Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
        Assert.Empty(browser.Requests);
        await client.ReadJsonAsync("api/local-node/catalogue/definitions");
        Assert.Equal(2, browser.Imports);
        Assert.Single(browser.Requests);
    }

    [Fact]
    public async Task Explicit_retry_after_failed_module_import_can_recover_without_replaying_a_dispatched_request()
    {
        var browser = new Browser(new SelectedSessionResponse(200, "{}", null)) { FailFirstImport = true };
        await using var transport = new BrowserSelectedSessionTransport(browser);
        var client = new BrowserWorkshopCatalogueClient(transport);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadJsonAsync("api/local-node/catalogue/definitions"));
        Assert.Empty(browser.Requests);
        await client.ReadJsonAsync("api/local-node/catalogue/definitions");
        Assert.Equal(2, browser.Imports);
        Assert.Single(browser.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interactive_detail_uses_the_selected_browser_receipts_and_keeps_refused_data_inert(bool refused)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalogue-detail.json")));
        var source = fixture.RootElement.GetProperty("source").Deserialize<WorkshopCatalogueEntry>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var browser = new Browser(
            new SelectedSessionResponse(200, fixture.RootElement.GetProperty("definition").GetRawText(), null),
            new SelectedSessionResponse(refused ? 403 : 200, refused ? "selected principal refused" : fixture.RootElement.GetProperty("response").GetRawText(), "detail-audit"));
        await using var transport = new BrowserSelectedSessionTransport(browser);
        Services.AddSingleton<IWorkshopCatalogueClient>(new BrowserWorkshopCatalogueClient(transport));
        Services.AddHarborlineUiAdapters();
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = new ViewRuntimeRow("selected", new Dictionary<string, object?> { ["catalogue"] = source });
        var cut = Render<CatalogueDetail>(parameters => parameters.Add(detail => detail.Row, row));
        cut.WaitForAssertion(() => Assert.Equal(2, browser.Requests.Count));
        if (refused) Assert.Empty(cut.FindAll("form,output,button"));
        else cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("output").Count));
        Assert.Equal("GET", browser.Requests[0][1]);
        Assert.Equal("POST", browser.Requests[1][1]);
        Assert.Equal("/api/local-node/catalogue/details/platform.detail.form/1.0.0", browser.Requests[1][0]);
        Assert.Empty(cut.FindAll("input,textarea,select,button"));
        cut.Render(parameters => parameters.Add(detail => detail.Row, row));
        Assert.Equal(2, browser.Requests.Count);
    }

    [Fact]
    public async Task Binary_transport_preserves_non_UTF8_bytes()
    {
        byte[] artifact = [0, 255, 128, 192, 13, 10];
        var browser = new Browser(new SelectedSessionBytesResponse(200, "", "export-audit", null, artifact));
        await using var transport = new BrowserSelectedSessionTransport(browser);
        Assert.Equal(artifact, (await transport.SendBytesAsync("/api/local-node/packs/export", "POST", """{"key":"example"}""")).Bytes);
        var request = Assert.Single(browser.Requests);
        Assert.Equal("/api/local-node/packs/export", request[0]);
        Assert.Equal("POST", request[1]);
        Assert.Equal("""{"key":"example"}""", request[2]);
        Assert.Equal("application/json", request[3]);
    }

    [Fact]
    public async Task Binary_transport_refusal_preserves_original_body_and_does_not_return_error_bytes()
    {
        var browser = new Browser(new SelectedSessionBytesResponse(403, "CSRF refused", "original", null, null));
        await using var transport = new BrowserSelectedSessionTransport(browser);
        var failure = await transport.SendBytesAsync("/api/local-node/packs/export", "POST", "{}");
        Assert.Equal("CSRF refused", failure.Body);
        Assert.Equal(403, failure.Status);
        Assert.Null(failure.Bytes);
        Assert.Single(browser.Requests);
    }

    [Fact]
    public async Task Already_cancelled_request_never_imports_or_dispatches_to_the_browser()
    {
        var browser = new Browser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        var client = new BrowserWorkshopCatalogueClient(transport);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PostJsonAsync("api/local-node/packs/activate", new { }, cancellation.Token));
        Assert.Equal(0, browser.Imports);
        Assert.Empty(browser.Requests);
    }

    [Fact]
    public async Task Detail_and_actions_send_wire_JSON_and_exact_artifact_bytes_through_the_selected_protocol()
    {
        var browser = new Browser(new SelectedSessionResponse(200, "{}", "projection-audit"), new SelectedSessionResponse(200, "{}", "install-audit"));
        await using var transport = new BrowserSelectedSessionTransport(browser);
        IWorkshopCatalogueClient client = new BrowserWorkshopCatalogueClient(transport);
        await client.PostJsonAsync("api/local-node/catalogue/details/platform.detail.form/1.0.0", new[] { new { Coordinate = new { Field = "title" } } });
        byte[] artifact = [0, 255, 128, 13, 10];
        await client.PostArtifactAsync("api/local-node/packs/install", artifact);
        Assert.Equal("/api/local-node/catalogue/details/platform.detail.form/1.0.0", browser.Requests[0][0]);
        Assert.Equal("POST", browser.Requests[0][1]);
        Assert.Equal("""[{"coordinate":{"field":"title"}}]""", browser.Requests[0][2]);
        Assert.Equal("application/json", browser.Requests[0][3]);
        Assert.Equal("/api/local-node/packs/install", browser.Requests[1][0]);
        Assert.Equal("POST", browser.Requests[1][1]);
        Assert.Equal(artifact, Assert.IsType<byte[]>(browser.Requests[1][2]));
        Assert.Equal("application/octet-stream", browser.Requests[1][3]);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("list")]
    [InlineData("detail")]
    [InlineData("artifact")]
    public async Task Selected_refusal_preserves_status_and_original_body_without_retry(string operation)
    {
        const string body = "selected session refused; audit:original";
        var browser = new Browser(new SelectedSessionResponse(403, body, "original"));
        await using var transport = new BrowserSelectedSessionTransport(browser);
        IWorkshopCatalogueClient client = new BrowserWorkshopCatalogueClient(transport);
        var failure = await Assert.ThrowsAsync<WorkshopRequestException>(async () =>
        {
            switch (operation)
            {
                case "read": await client.ReadJsonAsync("api/local-node/catalogue/definitions"); break;
                case "list": await client.ListAsync("FormDefinition"); break;
                case "detail": await client.PostJsonAsync("api/local-node/catalogue/details/platform.detail.form/1.0.0", Array.Empty<object>()); break;
                default: await client.PostArtifactAsync("api/local-node/packs/install", [0, 255]); break;
            }
        });
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, failure.StatusCode);
        Assert.Equal(body, failure.ResponseBody);
        Assert.Single(browser.Requests);
    }

    [Fact]
    public async Task Browser_catalogue_preserves_coordinates_availability_and_compiled_bindings_without_server_authority()
    {
        var browser = new Browser(new SelectedSessionResponse(200,
            """{"id":"platform.health.forms","version":"1.0.0","status":"Published","renderPlan":{"bindings":{"parameters":{"actions":[{"id":"inspect"}]}}}}""", null),
            new SelectedSessionResponse(200, """{"entries":[],"kindsUnavailable":[0]}""", null),
            new SelectedSessionResponse(200, """{"id":"platform.detail.form","version":"1.0.0","status":"Published"}""", null));
        await using var transport = new BrowserSelectedSessionTransport(browser);
        IWorkshopCatalogueClient client = new BrowserWorkshopCatalogueClient(transport);
        var view = await client.ReadViewAsync("platform.health.forms");
        Assert.Equal("inspect", view.CompiledBindings.GetProperty("parameters").GetProperty("actions")[0].GetProperty("id").GetString());
        Assert.Equal(0, Assert.Single((await client.ListAsync("FormDefinition")).KindsUnavailable).GetInt32());
        Assert.Equal("platform.detail.form", (await client.ReadFormAsync("platform.detail.form", "1.0.0")).Id);
        Assert.Equal(new[] { "/api/local-node/catalogue/definitions/ViewDefinition/platform.health.forms",
            "/api/local-node/catalogue/definitions?kind=FormDefinition",
            "/api/local-node/catalogue/definitions/FormDefinition/platform.detail.form?version=1.0.0" }, browser.Requests.Select(args => args[0]));
        Assert.All(browser.Requests, args => { Assert.Equal("GET", args[1]); Assert.Null(args[2]); Assert.Equal(4, args.Length); });
    }

    private sealed class Browser(params object[] responses) : IJSRuntime, IJSObjectReference
    {
        private readonly Queue<object> responses = new(responses);
        public List<object?[]> Requests { get; } = [];
        public int Imports { get; private set; }
        public bool FailFirstImport { get; init; }
        public bool CancelFirstImport { get; init; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identifier == "import")
            {
                Imports++;
                if (FailFirstImport && Imports == 1) return ValueTask.FromException<TValue>(new JSException("Module fetch failed."));
                if (CancelFirstImport && Imports == 1) return ValueTask.FromCanceled<TValue>(new CancellationToken(true));
                Assert.Equal("./selected-session-transport.mjs", Assert.Single(args!));
                return ValueTask.FromResult((TValue)(object)this);
            }
            Assert.Equal(responses.Peek() is SelectedSessionBytesResponse ? "sendBytesForCaller" : "sendForCaller", identifier);
            Assert.True(Assert.IsType<DotNetObjectReference<SelectedSessionDispatchGuard>>(args![0]).Value.CanDispatch());
            Requests.Add(args[1..]);
            return ValueTask.FromResult((TValue)(object)responses.Dequeue());
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Static_prerender_does_not_read_the_catalogue_before_the_selected_browser_exists()
    {
        var client = new UnavailableClient();
        Services.AddSingleton<IWorkshopCatalogueClient>(client);
        await using var renderer = new HtmlRenderer(Services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<SeededListPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SeededListPage.ItemId)] = "forms" }))).ToHtmlString());
        Assert.Equal(0, client.Requests);
        Assert.Contains("Loading Workshop list", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Static_prerender_does_not_project_detail_before_the_selected_browser_exists()
    {
        var client = new UnavailableClient();
        Services.AddSingleton<IWorkshopCatalogueClient>(client);
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalogue-detail.json")));
        var entry = fixture.RootElement.GetProperty("source").Deserialize<WorkshopCatalogueEntry>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var row = new ViewRuntimeRow("selected", new Dictionary<string, object?> { ["catalogue"] = entry });
        await using var renderer = new HtmlRenderer(Services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<CatalogueDetail>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CatalogueDetail.Row)] = row }))).ToHtmlString());
        Assert.Equal(0, client.Requests);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
    }

    private sealed class UnavailableClient : IWorkshopCatalogueClient
    {
        public int Requests { get; private set; }
        private Task<T> Refuse<T>() { Requests++; return Task.FromException<T>(new HttpRequestException("No selected browser during prerender.")); }
        public Task<WorkshopCatalogueEntry> ReadViewAsync(string viewId, CancellationToken cancellationToken = default) => Refuse<WorkshopCatalogueEntry>();
        public Task<WorkshopCatalogueList> ListAsync(string kind, CancellationToken cancellationToken = default) => Refuse<WorkshopCatalogueList>();
        public Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default) => Refuse<WorkshopCatalogueEntry>();
        public Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default) => Refuse<JsonElement>();
        public Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default) => Refuse<JsonElement>();
        public Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default) => Refuse<JsonElement>();
        public Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default) => Refuse<byte[]>();
    }
}
