using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using WebPush;

namespace MarkAi.Api.Servicii;

public interface IExpeditorPush
{
    bool Configurat { get; }
    Task TrimiteAsync(AbonamentPush abonament, string url, CancellationToken ct);
}
public sealed class AbonamentExpirat : Exception;
public sealed class ExpeditorPush(IConfiguration cfg, IHttpClientFactory clients) : IExpeditorPush
{
    private record Chei(string Subject, string PublicKey, string PrivateKey);
    private readonly Chei? chei = Citeste(cfg);
    private static Chei? Citeste(IConfiguration cfg)
    {
        var cale = cfg["WebPush:CheiFisier"];
        if (string.IsNullOrWhiteSpace(cale)) return null;
        var c = JsonSerializer.Deserialize<Chei>(File.ReadAllText(cale), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return c is not null && !string.IsNullOrWhiteSpace(c.Subject) && !string.IsNullOrWhiteSpace(c.PublicKey) && !string.IsNullOrWhiteSpace(c.PrivateKey) ? c : throw new InvalidOperationException("Invalid WebPush key configuration.");
    }
    public bool Configurat => chei != null;
    public string? CheiePublica => chei?.PublicKey;
    public async Task TrimiteAsync(AbonamentPush a, string url, CancellationToken ct)
    {
        if (chei is null) throw new InvalidOperationException("WebPush is not configured.");
        using var client = new WebPushClient(clients.CreateClient("webpush"));
        try
        {
            await client.SendNotificationAsync(new PushSubscription(a.Endpoint, a.P256dh, a.Auth),
                JsonSerializer.Serialize(new { title = "TeleBac", body = "Ai o notificare nouă în aplicație.", url }),
                new Dictionary<string, object> { ["vapidDetails"] = new VapidDetails(chei.Subject, chei.PublicKey, chei.PrivateKey), ["TTL"] = 3600 }, ct);
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound) { throw new AbonamentExpirat(); }
    }
    public static bool EndpointPermis(string endpoint)
    {
        if (endpoint.Length > 2048 || endpoint.Any(c => c < '!' || c > '~') || !Uri.TryCreate(endpoint, UriKind.Absolute, out var u) || u.Scheme != "https" || u.Port != 443 || u.UserInfo.Length > 0 || u.Fragment.Length > 0) return false;
        return u.Host is "fcm.googleapis.com" or "updates.push.services.mozilla.com"
            || u.Host.EndsWith(".push.apple.com", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase);
    }
}
