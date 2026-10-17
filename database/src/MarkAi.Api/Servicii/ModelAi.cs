using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Beta.Messages;

namespace MarkAi.Api.Servicii;

/// <summary>What the model is used for; each provider picks its model and reasoning effort from it.</summary>
public enum ScopAi
{
    /// <summary>Quiz questions generated from a lesson (background job, quality over speed).</summary>
    Intrebari,
    /// <summary>A phone tutor turn (must answer within Twilio's webhook time limit).</summary>
    Telefon,
}

public record MesajAi(bool DeLaElev, string Text);

/// <summary>The model declined to answer (safety refusal).</summary>
public sealed class RefuzAi() : Exception("Modelul a refuzat cererea.");

/// <summary>A provider-agnostic call that returns JSON text matching <c>schema</c>.</summary>
public interface IModelAi
{
    string Nume { get; }

    Task<string> JsonAsync(ScopAi scop, string sistem, IReadOnlyList<MesajAi> mesaje, Dictionary<string, JsonElement> schema,
        int maxTokens, CancellationToken ct);
}

/// <summary>
/// Picks the AI provider from the keys set on the server: Claude when ANTHROPIC_API_KEY is set,
/// otherwise Google Gemini (free tier, for demos) when GEMINI_API_KEY is set, otherwise none.
/// </summary>
public sealed class ModelAi(IConfiguration cfg, IHttpClientFactory http)
{
    public IModelAi? Activ { get; } =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")) ? new ClaudeAi(cfg)
        : !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY")) ? new GeminiAi(cfg, http.CreateClient("gemini"))
        : null;

    public bool Configurat => Activ is not null;
}

public sealed class ClaudeAi(IConfiguration cfg) : IModelAi
{
    public string Nume => "Claude";

    public async Task<string> JsonAsync(ScopAi scop, string sistem, IReadOnlyList<MesajAi> mesaje, Dictionary<string, JsonElement> schema,
        int maxTokens, CancellationToken ct)
    {
        AnthropicClient client = new();
        // Server-side fallback: a declined request is re-run on a suitable fallback model within the same call.
        var raspuns = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = scop == ScopAi.Telefon
                ? cfg["Claude:ModelTelefon"] ?? cfg["Claude:Model"] ?? "claude-opus-5-5"
                : cfg["Claude:Model"] ?? "claude-opus-5-5",
            MaxTokens = maxTokens,
            System = sistem,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new BetaFallbacksParam(JsonSerializer.SerializeToElement("default")),
            // Low effort keeps phone replies fast; questions get more thought.
            OutputConfig = new BetaOutputConfig
            {
                Effort = scop == ScopAi.Telefon ? Effort.Low : Effort.Medium,
                Format = new BetaJsonOutputFormat { Schema = schema },
            },
            Messages = mesaje.Select(m => new BetaMessageParam { Role = m.DeLaElev ? Role.User : Role.Assistant, Content = m.Text }).ToList(),
        }, ct);

        if (raspuns.StopReason == BetaStopReason.Refusal) throw new RefuzAi();
        if (raspuns.StopReason == BetaStopReason.MaxTokens) throw new InvalidOperationException("Răspunsul a fost trunchiat.");
        return string.Concat(raspuns.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
    }
}

/// <summary>
/// Google Gemini over its REST API (generateContent with a JSON-schema response). Used as a free fallback;
/// note that Google may use free-tier requests to improve its models, so demo data should not be real student data.
/// </summary>
public sealed class GeminiAi(IConfiguration cfg, HttpClient http) : IModelAi
{
    public string Nume => "Gemini";

    public async Task<string> JsonAsync(ScopAi scop, string sistem, IReadOnlyList<MesajAi> mesaje, Dictionary<string, JsonElement> schema,
        int maxTokens, CancellationToken ct)
    {
        var model = scop == ScopAi.Telefon
            ? cfg["Gemini:ModelTelefon"] ?? cfg["Gemini:Model"] ?? "gemini-3.5-flash-lite"
            : cfg["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        var config = new JsonObject
        {
            ["responseMimeType"] = "application/json",
            ["responseJsonSchema"] = JsonSerializer.SerializeToNode(schema),
            ["maxOutputTokens"] = maxTokens,
        };
        // Low reasoning brings a phone turn down to ~2–3 s.
        if (scop == ScopAi.Telefon) config["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "low" };
        var corp = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = sistem }) },
            ["contents"] = new JsonArray(mesaje.Select(m => (JsonNode)new JsonObject
            {
                ["role"] = m.DeLaElev ? "user" : "model",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = m.Text }),
            }).ToArray()),
            ["generationConfig"] = config,
        };

        using var cerere = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
        { Content = JsonContent.Create(corp) };
        cerere.Headers.Add("x-goog-api-key", Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
        using var r = await http.SendAsync(cerere, ct);
        if (r.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("Limita gratuită Gemini a fost atinsă. Încearcă din nou peste un minut.");
        var json = await r.Content.ReadFromJsonAsync<JsonNode>(ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini a răspuns {(int)r.StatusCode}: {json?["error"]?["message"]}");

        if (json?["promptFeedback"]?["blockReason"] is not null) throw new RefuzAi();
        var candidat = json?["candidates"]?[0];
        var motiv = candidat?["finishReason"]?.GetValue<string>();
        if (motiv is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII") throw new RefuzAi();
        if (motiv == "MAX_TOKENS") throw new InvalidOperationException("Răspunsul a fost trunchiat.");
        // Thought summaries (if any) are marked "thought"; the answer is in the other parts.
        return string.Concat((candidat?["content"]?["parts"]?.AsArray() ?? [])
            .Where(p => p?["thought"]?.GetValue<bool>() != true)
            .Select(p => p?["text"]?.GetValue<string>() ?? ""));
    }
}
