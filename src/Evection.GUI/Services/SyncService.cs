using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Evection.GUI.Services;

public sealed class SyncException(string message) : Exception(message);

/// <summary>
/// Optional sign-in so settings follow the player to other computers. Talks to services/sync-worker.
///
/// Sign-in flow (the standard one for desktop apps, RFC 8252): we listen on a random port on 127.0.0.1, open
/// the browser at the server's /auth/discord/start, the server does the Discord login and redirects back to our
/// local port with a session token. The token only grants access to this player's settings blob.
/// </summary>
public sealed class SyncService(SettingsStore settings)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Server baked into this build (see SyncServerUrl in Evection.GUI.csproj), unless overridden in Advanced settings.</summary>
    public string? ServerUrl
    {
        get
        {
            var url = settings.Current.SyncServerOverride;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = typeof(SyncService).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "SyncServerUrl")?.Value;
            }
            return string.IsNullOrWhiteSpace(url) ? null : url.TrimEnd('/');
        }
    }

    public bool IsAvailable => ServerUrl != null;
    public bool IsSignedIn => settings.Current.Account != null;

    public async Task<Account> SignInAsync(CancellationToken cancel)
    {
        var server = ServerUrl ?? throw new SyncException("sign-in isn't set up in this build.");

        var port = FreePort();
        var redirect = $"http://127.0.0.1:{port}/callback/";
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        Shell.Open($"{server}/auth/discord/start?redirect={Uri.EscapeDataString(redirect)}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            throw new SyncException(cancel.IsCancellationRequested ? "sign-in cancelled." : "sign-in timed out. try again.");
        }

        var query = context.Request.QueryString;
        var token = query["token"];
        var error = query["error"];
        await RespondAsync(context.Response, token != null);

        if (token == null)
            throw new SyncException(error ?? "sign-in didn't finish.");

        var account = new Account
        {
            Token = token,
            UserId = query["user"] ?? "",
            DisplayName = query["name"] ?? "Player",
        };
        settings.Current.Account = account;
        settings.SaveFromSync();

        // First sign-in on this PC: prefer what's already in the cloud; otherwise upload what we have.
        if (!await PullAsync())
            await PushAsync();
        return account;
    }

    public void SignOut()
    {
        settings.Current.Account = null;
        settings.Current.LastSyncedUtc = null;
        settings.SaveFromSync();
    }

    /// <summary>Downloads synced settings. Returns false if the account has nothing stored yet.</summary>
    public async Task<bool> PullAsync()
    {
        using var request = Request(HttpMethod.Get);
        using var response = await SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureOk(response);
        var json = await response.Content.ReadAsStringAsync();
        settings.Current.ApplySyncedJson(json);
        settings.Current.LastSyncedUtc = DateTime.UtcNow;
        settings.SaveFromSync();
        return true;
    }

    public async Task PushAsync()
    {
        using var request = Request(HttpMethod.Put);
        request.Content = new StringContent(settings.Current.ToSyncedJson(), Encoding.UTF8, "application/json");
        using var response = await SendAsync(request);
        await EnsureOk(response);
        settings.Current.LastSyncedUtc = DateTime.UtcNow;
        settings.SaveFromSync();
    }

    /// <summary>Deletes everything stored for this account on the server, then signs out.</summary>
    public async Task DeleteCloudDataAsync()
    {
        using var request = Request(HttpMethod.Delete);
        using var response = await SendAsync(request);
        await EnsureOk(response);
        SignOut();
    }

    private HttpRequestMessage Request(HttpMethod method)
    {
        var account = settings.Current.Account ?? throw new SyncException("you're not signed in.");
        var server = ServerUrl ?? throw new SyncException("sign-in isn't set up in this build.");
        var request = new HttpRequestMessage(method, $"{server}/v1/settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
        return request;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        try
        {
            return await Http.SendAsync(request);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new SyncException("couldn't reach the sync server.");
        }
    }

    private async Task EnsureOk(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            SignOut();
            throw new SyncException("your sign-in expired. sign in again.");
        }
        var body = await response.Content.ReadAsStringAsync();
        string? message = null;
        try { message = JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
        throw new SyncException(message ?? $"sync failed ({(int)response.StatusCode}).");
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static async Task RespondAsync(HttpListenerResponse response, bool ok)
    {
        var title = ok ? "signed in" : "sign-in didn't work";
        var text = ok ? "you can close this tab." : "close this tab and try again.";
        var html = $$"""
            <!doctype html><html><head><meta charset="utf-8"><title>Evection Hook</title>
            <style>body{margin:0;height:100vh;display:grid;place-items:center;background:#000;color:#fafafa;font-family:system-ui,sans-serif}
            div{text-align:center;padding:40px;border:1px solid #2e2e2e;border-radius:12px;background:#0a0a0a}
            h1{font-size:24px;letter-spacing:-.04em;margin:0 0 8px}p{color:#a1a1a1;margin:0}</style></head>
            <body><div><h1>{{title}}</h1><p>{{text}}</p></div></body></html>
            """;
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
}
