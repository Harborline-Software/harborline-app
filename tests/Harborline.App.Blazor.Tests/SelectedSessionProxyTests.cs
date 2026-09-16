using System.Net;
using System.Text;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Transport;
using Microsoft.AspNetCore.Http;

namespace Harborline.App.Blazor.Tests;

public sealed class SelectedSessionProxyTests
{
    [Fact]
    public async Task Concurrent_browser_cookies_cannot_inherit_bootstrap_or_another_users_principal()
    {
        using var proxy = new SelectedSessionProxy(new Uri("https://node.example"), new HttpMessageInvoker(new NodeHandler()));
        var requests = Enumerable.Range(0, 12).Select(index => Context(index % 2 == 0 ? "alice" : "bob")).ToArray();
        await Task.WhenAll(requests.Select(proxy.ForwardAsync));
        for (var index = 0; index < requests.Length; index++)
        {
            var context = requests[index];
            Assert.Equal(index % 2 == 0 ? 200 : 403, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            using var json = await JsonDocument.ParseAsync(context.Response.Body);
            Assert.Equal(index % 2 == 0 ? "__Host-hl-selected=alice" : "__Host-hl-selected=bob",
                json.RootElement.GetProperty("principal").GetString());
            Assert.Equal("server-audit-42", json.RootElement.GetProperty("auditId").GetString());
            Assert.Equal("server-audit-42", context.Response.Headers["X-Harborline-Audit-Id"].ToString());
            Assert.False(context.Response.Headers.ContainsKey("Authorization"));
        }
    }

    [Theory]
    [InlineData("", "https://app.example", "GET", "", 401)]
    [InlineData("alice", "https://hostile.example", "GET", "", 403)]
    [InlineData("alice", "https://app.example", "POST", "", 403)]
    [InlineData("alice", "null", "POST", "one-time-token", 403)]
    public async Task Missing_cookie_origin_or_antiforgery_refuses_before_upstream(
        string selected, string origin, string method, string token, int expected)
    {
        using var proxy = new SelectedSessionProxy(new Uri("https://node.example"), new HttpMessageInvoker(new NeverSendHandler()));
        var context = Context(selected);
        context.Request.Headers.Origin = origin;
        context.Request.Method = method;
        context.Request.Headers["X-Harborline-Antiforgery"] = token;
        await proxy.ForwardAsync(context);
        Assert.Equal(expected, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("../elsewhere")]
    [InlineData("%2e%2e/elsewhere")]
    [InlineData("local-node%2fsecret")]
    [InlineData("/hostile.example/path")]
    [InlineData("local-node\\secret")]
    [InlineData("https://hostile.example")]
    public async Task Raw_path_escapes_cannot_select_another_route_or_target(string path)
    {
        using var proxy = new SelectedSessionProxy(new Uri("https://node.example"), new HttpMessageInvoker(new NeverSendHandler()));
        var context = Context("alice");
        context.Request.Path = "/api/selected-node/" + path;
        await proxy.ForwardAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task Safe_idempotency_header_is_forwarded_without_any_credential_override()
    {
        using var proxy = new SelectedSessionProxy(new Uri("https://node.example"), new HttpMessageInvoker(new NodeHandler("example-submit-v1")));
        var context = Context("alice");
        context.Request.Method = "POST";
        context.Request.Headers["X-Harborline-Antiforgery"] = "server-token";
        context.Request.Headers["Idempotency-Key"] = "example-submit-v1";
        await proxy.ForwardAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_idempotency_headers_refuse_before_upstream()
    {
        using var proxy = new SelectedSessionProxy(new Uri("https://node.example"), new HttpMessageInvoker(new NeverSendHandler()));
        var context = Context("alice");
        context.Request.Headers["Idempotency-Key"] = new Microsoft.Extensions.Primitives.StringValues(["first", "second"]);
        await proxy.ForwardAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
    }

    private static DefaultHttpContext Context(string selected)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.example");
        context.Request.Method = "GET";
        context.Request.Path = "/api/selected-node/local-node/records/entities/example";
        context.Request.Headers.Origin = "https://app.example";
        context.Request.Headers.Authorization = "Bearer bootstrap-secret";
        context.Request.Headers.Cookie = $"__Host-web_session=legacy-admin; unrelated=secret; __Host-hl-selected={selected}";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class NeverSendHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Refused requests must not contact the node.");
    }

    private sealed class NodeHandler(string? expectedRequestId = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://node.example/api/local-node/records/entities/example", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("X-Untrusted-Actor"));
            if (expectedRequestId is not null) Assert.Equal(expectedRequestId, Assert.Single(request.Headers.GetValues("Idempotency-Key")));
            var cookie = Assert.Single(request.Headers.GetValues("Cookie"));
            Assert.DoesNotContain("legacy-admin", cookie, StringComparison.Ordinal);
            await Task.Delay(cookie.Contains("alice", StringComparison.Ordinal) ? 15 : 1, cancellationToken);
            var response = new HttpResponseMessage(cookie.Contains("bob", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { principal = cookie, auditId = "server-audit-42" }), Encoding.UTF8, "application/json")
            };
            response.Headers.Add("X-Harborline-Audit-Id", "server-audit-42");
            response.Headers.Add("Authorization", "must-not-reach-browser");
            return response;
        }
    }
}
