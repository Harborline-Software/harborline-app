using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class CatalogueDetailTests : BunitContext
{
    private static JsonObject Fixture() => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalogue-detail.json")))!.AsObject();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("2147483648.0.0")]
    [InlineData("1.0.0-preview")]
    [InlineData("1.0.0\n")]
    public void Noncanonical_source_version_is_inert_without_a_projection_request(string version)
    {
        var data = Fixture();
        data["source"]!["version"] = version;
        var handler = Configure(data);
        var cut = Render<CatalogueDetail>(parameters => parameters.Add(component => component.Row, Row(data)));
        cut.WaitForAssertion(() => Assert.Single(handler.Paths));
        Assert.Empty(cut.FindAll("form,output,button"));
    }

    [Fact]
    public void Both_lanes_render_the_same_normalized_values_without_actions()
    {
        var data = Fixture();
        var handler = Configure(data);
        var cut = Render<CatalogueDetail>(parameters => parameters.Add(component => component.Row, Row(data)));
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("output").Count));
        var normalized = cut.FindAll(".hl-form-field").Select(node => new[] { node.QuerySelector("label")!.TextContent, node.QuerySelector("output")!.TextContent });
        Assert.Equal(data["normalized"]!.ToJsonString(), JsonSerializer.SerializeToNode(normalized)!.ToJsonString());
        Assert.Empty(cut.FindAll("input,textarea,select,button"));
        cut.Find("form").Submit();
        Assert.Equal(new[] { "/api/local-node/catalogue/definitions/FormDefinition/platform.detail.form?version=1.0.0",
            "/api/local-node/catalogue/details/platform.detail.form/1.0.0" }, handler.Paths);
        var requests = JsonNode.Parse(handler.Body!)!.AsArray();
        Assert.Equal(new[] { "formId", "title", "version", "cascadeLayer" }, requests.Select(item => item!["coordinate"]!["field"]!.GetValue<string>()));
        Assert.All(requests, item =>
        {
            Assert.Equal("1.0.0", item!["coordinate"]!["version"]!.GetValue<string>());
            Assert.Equal("platform.pack.author", item!["coordinate"]!["id"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(data["source"]!["catalogueFieldBinding"], item["sourceBinding"]));
        });
    }

    [Fact]
    public void Denied_field_is_absent_from_adapter_and_DOM_without_source_body_reads()
    {
        var data = Fixture();
        var projection = data["response"]!["projection"]!;
        projection["fieldsMeta"]!.AsObject().Remove("title");
        projection["overlay"]!["fields"]!.AsObject().Remove("title");
        // A transport regression returning denied data must not reintroduce it to the adapter.
        projection["values"]!["title"] = new JsonObject { ["unexpected"] = "sentinel-do-not-read" };
        var handler = Configure(data);
        var cut = Render<CatalogueDetail>(parameters => parameters.Add(component => component.Row, Row(data)));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("output").Count));
        Assert.DoesNotContain("sentinel-do-not-read", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Title", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, handler.Paths.Count);
        Assert.DoesNotContain(handler.Paths, path => path.Contains("/forms/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("refused")]
    [InlineData("mapping")]
    [InlineData("mapping-range")]
    [InlineData("revision")]
    [InlineData("kind")]
    [InlineData("binding")]
    [InlineData("value")]
    [InlineData("localized")]
    [InlineData("metadata")]
    public void Invalid_definition_or_projection_remains_inert(string mutation)
    {
        var data = Fixture();
        if (mutation == "mapping") data["definition"]!["body"]!["catalogueFieldSource"]!["coordinateSchemaVersion"] = 2;
        if (mutation == "mapping-range") data["definition"]!["body"]!["catalogueFieldSource"]!["coordinateSchemaVersion"] = 2147483648L;
        if (mutation == "revision") data["definition"]!["version"] = "2.0.0";
        if (mutation == "kind") data["definition"]!["renderPlan"]!["definitionKind"] = "ViewDefinition";
        if (mutation == "binding") data["response"]!["projection"]!["detailBinding"]!["definitionHash"] = "wrong";
        if (mutation == "value") data["response"]!["projection"]!["values"]!["formId"] = new JsonObject { ["unexpected"] = "must-not-render" };
        if (mutation == "localized") data["response"]!["projection"]!["values"]!["title"] = new JsonObject { ["defaultLocale"] = null, ["values"] = new JsonObject() };
        if (mutation == "metadata") data["response"]!["projection"]!.AsObject().Remove("fieldsMeta");
        var handler = Configure(data, mutation == "missing" ? HttpStatusCode.NotFound : mutation == "refused" ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
        var cut = Render<CatalogueDetail>(parameters => parameters.Add(component => component.Row, Row(data)));
        cut.WaitForAssertion(() => Assert.Equal(mutation is "binding" or "value" or "localized" or "metadata" ? 2 : 1, handler.Paths.Count));
        Assert.Empty(cut.FindAll("form,output,button"));
    }

    private Handler Configure(JsonObject fixture, HttpStatusCode status = HttpStatusCode.OK)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddHarborlineUiAdapters();
        var handler = new Handler(fixture, status);
        Services.AddSingleton<IWorkshopCatalogueClient>(new HttpWorkshopCatalogueClient(new HttpClient(handler) { BaseAddress = new Uri("https://node.test/") }));
        return handler;
    }

    private static ViewRuntimeRow Row(JsonObject fixture) => new("platform.pack.author@1.0.0",
        new Dictionary<string, object?> { ["catalogue"] = fixture["source"]!.Deserialize<WorkshopCatalogueEntry>(JsonOptions)! });

    private sealed class Handler(JsonObject fixture, HttpStatusCode status) : HttpMessageHandler
    {
        internal List<string> Paths { get; } = [];
        internal string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            if (request.Content is not null) Body = await request.Content.ReadAsStringAsync(cancellationToken);
            return new(status) { Content = new StringContent(fixture[request.Method == HttpMethod.Post ? "response" : "definition"]!.ToJsonString(), Encoding.UTF8, "application/json") };
        }
    }
}
