using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;

namespace Harborline.App.Blazor.ReferenceHost.Transport;

/// <summary>Browser-cookie-only forwarding, isolated from every bootstrap HttpClient.</summary>
public sealed class SelectedSessionProxy : IDisposable
{
    public const string Route = "/api/selected-node/{**path}";
    private const string Prefix = "/api/selected-node/";
    private const string CookieName = "__Host-hl-selected";
    private const string AntiforgeryHeader = "X-Harborline-Antiforgery";
    private const int MaximumBodyBytes = 10 * 1024 * 1024;
    private static readonly Regex Token = new("^[A-Za-z0-9_-]{1,256}$", RegexOptions.CultureInvariant);
    private static readonly Regex RequestId = new("^[A-Za-z0-9._:-]{1,128}$", RegexOptions.CultureInvariant);
    private static readonly Regex PathEscape = new(@"[\\#\x00-\x20]|%2e|%2f|%5c", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] ResponseHeaders = [AntiforgeryHeader, "X-Harborline-Audit-Id", "X-Harborline-Audit-Correlation", "X-Correlation-Id"];
    private readonly Uri _origin;
    private readonly HttpMessageInvoker _sender;

    public SelectedSessionProxy(Uri origin) : this(origin, new HttpMessageInvoker(new SocketsHttpHandler
    {
        UseCookies = false,
        AllowAutoRedirect = false,
    })) { }

    public SelectedSessionProxy(Uri origin, HttpMessageInvoker sender)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(sender);
        if (!origin.IsAbsoluteUri || (origin.Scheme != "http" && origin.Scheme != "https")
            || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new ArgumentException("A fixed local-node origin is required.", nameof(origin));
        _origin = origin;
        _sender = sender;
    }

    public async Task ForwardAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        if (string.IsNullOrEmpty(raw)) raw = request.Path.Value + request.QueryString.Value;
        var path = raw.StartsWith(Prefix, StringComparison.Ordinal) ? raw[Prefix.Length..] : string.Empty;
        var pathname = path.Split('?', 2)[0];
        if ((!pathname.StartsWith("local-node/", StringComparison.Ordinal) && !pathname.StartsWith("session/", StringComparison.Ordinal))
            || PathEscape.IsMatch(pathname) || pathname.Contains("//", StringComparison.Ordinal)
            || pathname.Split('/').Any(part => part is "." or ".."))
        {
            await RefuseAsync(context, 400, "selected_session_path_invalid");
            return;
        }
        if (request.Method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
        {
            await RefuseAsync(context, 405, "selected_session_method_invalid");
            return;
        }
        var read = request.Method == "GET";
        var origin = request.Headers.Origin.ToString();
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        var expectedOrigin = request.Scheme + "://" + request.Host;
        if ((origin.Length != 0 && !string.Equals(origin, expectedOrigin, StringComparison.Ordinal))
            || (fetchSite.Length != 0 && fetchSite != "same-origin")
            || (origin.Length == 0 && (!read || fetchSite != "same-origin")))
        {
            await RefuseAsync(context, 403, "selected_session_origin_invalid");
            return;
        }
        var cookies = request.Headers.Cookie.ToString().Split(';').Select(part => part.Trim())
            .Where(part => part.StartsWith(CookieName + "=", StringComparison.Ordinal)).ToArray();
        if (cookies.Length != 1 || !Token.IsMatch(cookies[0][(CookieName.Length + 1)..]))
        {
            await RefuseAsync(context, 401, "selected_session_required");
            return;
        }
        var token = request.Headers[AntiforgeryHeader].ToString();
        if (!read && !Token.IsMatch(token))
        {
            await RefuseAsync(context, 403, "selected_session_antiforgery_required");
            return;
        }

        var requestIds = request.Headers["Idempotency-Key"];
        if (requestIds.Count > 1 || (requestIds.Count == 1 && !RequestId.IsMatch(requestIds.ToString())))
        {
            await RefuseAsync(context, 400, "selected_session_headers_invalid");
            return;
        }
        using var outgoing = new HttpRequestMessage(new HttpMethod(request.Method), new Uri(_origin, "/api/" + path));
        if (requestIds.Count == 1) outgoing.Headers.Add("Idempotency-Key", requestIds.ToString());
        outgoing.Headers.Add("Cookie", cookies[0]);
        outgoing.Headers.Add("Accept", "application/json");
        if (!read)
        {
            outgoing.Headers.Add(AntiforgeryHeader, token);
            using var body = new MemoryStream();
            var buffer = new byte[16384];
            int bytes;
            while ((bytes = await request.Body.ReadAsync(buffer, context.RequestAborted)) != 0)
            {
                if (body.Length + bytes > MaximumBodyBytes)
                {
                    await RefuseAsync(context, 413, "selected_session_body_too_large");
                    return;
                }
                body.Write(buffer, 0, bytes);
            }
            outgoing.Content = new ByteArrayContent(body.ToArray());
            if (request.ContentType is not null) outgoing.Content.Headers.TryAddWithoutValidation("Content-Type", request.ContentType);
        }
        try
        {
            using var upstream = await _sender.SendAsync(outgoing, context.RequestAborted);
            if ((int)upstream.StatusCode is >= 300 and < 400)
            {
                await RefuseAsync(context, 502, "selected_session_redirect_refused");
                return;
            }
            context.Response.StatusCode = (int)upstream.StatusCode;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.ContentType = upstream.Content.Headers.ContentType?.ToString();
            foreach (var name in ResponseHeaders)
                if (upstream.Headers.TryGetValues(name, out var values)) context.Response.Headers[name] = values.ToArray();
            if (upstream.Headers.TryGetValues("Set-Cookie", out var setCookies))
                context.Response.Headers.SetCookie = setCookies.Where(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal)).ToArray();
            await upstream.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (HttpRequestException)
        {
            if (!context.Response.HasStarted) await RefuseAsync(context, 502, "selected_session_upstream_unavailable");
            else context.Abort();
        }
    }

    private static Task RefuseAsync(HttpContext context, int status, string error)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new { error }, context.RequestAborted);
    }

    public void Dispose() => _sender.Dispose();
}
