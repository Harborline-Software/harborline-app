using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Transport;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class WorkshopAudienceTests
{
    [Theory]
    [InlineData("export")]
    [InlineData("validate")]
    [InlineData("verify")]
    [InlineData("trace")]
    public async Task Proven_desktop_operations_use_only_the_server_client(string operation)
    {
        var browser = new Browser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        using var handler = new DesktopHandler();
        using var http = CreateDesktopHttp(handler);
        IWorkshopCatalogueClient client = new WorkshopAudienceClient(new(transport), new(http));
        var candidate = JsonSerializer.SerializeToElement(new { key = "example" });
        byte[] artifact = [0, 255, 128, 192, 13, 10];
        switch (operation)
        {
            case "export": Assert.Equal(artifact, await client.ExportAsync(candidate)); break;
            case "validate": await client.PostJsonAsync("api/local-node/packs/export?validateOnly=true", candidate); break;
            case "verify": await client.PostArtifactAsync("/api/local-node/packs/verify", artifact); break;
            default: await client.ReadJsonAsync("api/local-node/authorization/traces/audit%3A123"); break;
        }
        Assert.Equal(0, browser.Imports);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer server-only-bootstrap", request.Authorization);
        Assert.Equal(operation == "trace" ? "GET" : "POST", request.Method);
        Assert.Equal(operation switch
        {
            "export" => "/api/local-node/packs/export", "validate" => "/api/local-node/packs/export?validateOnly=true",
            "verify" => "/api/local-node/packs/verify", _ => "/api/local-node/authorization/traces/audit%3A123",
        }, request.Path);
        if (operation == "verify") Assert.Equal(artifact, request.Body);
        else if (operation is "export" or "validate") Assert.Equal("""{"key":"example"}""", Encoding.UTF8.GetString(request.Body));
    }

    [Theory]
    [InlineData("read", "api/local-node/catalogue/definitions")]
    [InlineData("read", "api/local-node/navigation/workspaces")]
    [InlineData("read", "api/local-node/asset-registry/entities/one")]
    [InlineData("read", "api/local-node/authorization/traces/")]
    [InlineData("read", "api/local-node/authorization/traces/../grants")]
    [InlineData("read", "api/local-node/authorization/traces/%2e%2e")]
    [InlineData("read", "api/local-node/authorization/traces/a%2fb")]
    [InlineData("read", "api/local-node/authorization/traces/a?other=true")]
    [InlineData("read", "api/local-node/packs/export")]
    [InlineData("json", "api/local-node/packs/activate")]
    [InlineData("json", "api/local-node/catalogue/details/platform.detail.form/1.0.0")]
    [InlineData("json", "api/local-node/packs/export?validateOnly=true&other=true")]
    [InlineData("artifact", "api/local-node/packs/install")]
    [InlineData("artifact", "api/local-node/packs/verify/extra")]
    public async Task Selected_operations_and_near_match_paths_never_fall_back_after_refusal(string operation, string path)
    {
        var browser = new Browser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        using var handler = new DesktopHandler();
        using var http = CreateDesktopHttp(handler);
        IWorkshopCatalogueClient client = new WorkshopAudienceClient(new(transport), new(http));
        var refusal = await Assert.ThrowsAsync<WorkshopRequestException>(async () =>
        {
            if (operation == "read") await client.ReadJsonAsync(path);
            else if (operation == "json") await client.PostJsonAsync(path, new { });
            else await client.PostArtifactAsync(path, [0, 255]);
        });
        Assert.Equal(HttpStatusCode.Forbidden, refusal.StatusCode);
        Assert.Equal("selected refusal", refusal.ResponseBody);
        Assert.Empty(handler.Requests);
        var args = Assert.Single(browser.Requests);
        Assert.Equal("/" + path, args[1]);
        Assert.DoesNotContain(args, arg => arg is string text && text.Contains("server-only-bootstrap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Desktop_export_refusal_is_preserved_without_browser_fallback()
    {
        var browser = new Browser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        using var handler = new DesktopHandler { Refuse = true };
        using var http = CreateDesktopHttp(handler);
        IWorkshopCatalogueClient client = new WorkshopAudienceClient(new(transport), new(http));
        var refusal = await Assert.ThrowsAsync<WorkshopRequestException>(() => client.ExportAsync(JsonSerializer.SerializeToElement(new { })));
        Assert.Equal(HttpStatusCode.Forbidden, refusal.StatusCode);
        Assert.Equal("desktop refusal", refusal.ResponseBody);
        Assert.Single(handler.Requests);
        Assert.Equal(0, browser.Imports);
    }

    private static HttpClient CreateDesktopHttp(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5123/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "server-only-bootstrap");
        return http;
    }

    private sealed record Request(string Method, string Path, string? Authorization, byte[] Body);
    private sealed class DesktopHandler : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];
        public bool Refuse { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString(),
                request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken)));
            return new HttpResponseMessage(Refuse ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
            {
                Content = Refuse ? new StringContent("desktop refusal")
                    : request.RequestUri.PathAndQuery == "/api/local-node/packs/export" ? new ByteArrayContent([0, 255, 128, 192, 13, 10])
                    : new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class Browser : IJSRuntime, IJSObjectReference
    {
        public int Imports { get; private set; }
        public List<object?[]> Requests { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import") { Imports++; return ValueTask.FromResult((TValue)(object)this); }
            Requests.Add(args!);
            return ValueTask.FromResult((TValue)(identifier == "sendBytesForCaller"
                ? (object)new SelectedSessionBytesResponse(403, "selected refusal", null, null, null)
                : new SelectedSessionResponse(403, "selected refusal", null)));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
