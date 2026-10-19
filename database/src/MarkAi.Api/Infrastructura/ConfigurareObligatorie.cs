namespace MarkAi.Api.Infrastructura;

/// <summary>
/// Startup check of the configuration. Missing required settings stop the API with one message naming all of them;
/// half-configured optional features (AI, Twilio, SMTP) only log a warning, since the app runs without them.
/// The JWT key itself is validated by <see cref="ConfigurareAutentificare.Optiuni"/>.
/// </summary>
public static class ConfigurareObligatorie
{
    public static string Conexiune(IConfiguration cfg) =>
        Environment.GetEnvironmentVariable("MARKAI_CONNECTION_STRING") ?? cfg.GetConnectionString("MarkAi") ?? "";

    /// <summary>Required settings that are missing, by the name to set in markai.env.</summary>
    public static IReadOnlyList<string> Lipsa(IConfiguration cfg, IHostEnvironment env, string conexiune)
    {
        var lipsa = new List<string>();
        if (string.IsNullOrWhiteSpace(conexiune)) lipsa.Add("MARKAI_CONNECTION_STRING");
        if (!env.IsDevelopment() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MARKAI_JWT_KEY") ?? cfg["Autentificare:CheieJwt"]))
            lipsa.Add("MARKAI_JWT_KEY");
        return lipsa;
    }

    /// <summary>Optional features that are partly configured, so they would fail at the first use. (A missing AI key is
    /// reported by <see cref="AplicatieMarkAi.Configureaza"/>, from the provider actually registered.)</summary>
    public static IReadOnlyList<string> Avertismente(IConfiguration cfg)
    {
        static bool Are(string? v) => !string.IsNullOrWhiteSpace(v);
        var avertismente = new List<string>();

        var twilio = new Dictionary<string, string?>
        {
            ["TWILIO_ACCOUNT_SID"] = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID"),
            ["TWILIO_AUTH_TOKEN"] = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN"),
            ["Twilio__Numar"] = cfg["Twilio:Numar"],
        };
        if (twilio.Values.Any(Are) && !twilio.Values.All(Are))
            avertismente.Add("Twilio is partly configured, missing: " + string.Join(", ", twilio.Where(x => !Are(x.Value)).Select(x => x.Key)) + ".");

        var smtp = new Dictionary<string, string?> { ["SetariEmail__Host"] = cfg["SetariEmail:Host"], ["SetariEmail__Expeditor"] = cfg["SetariEmail:Expeditor"] };
        if (smtp.Values.Any(Are) && !smtp.Values.All(Are))
            avertismente.Add("SMTP is partly configured, missing: " + string.Join(", ", smtp.Where(x => !Are(x.Value)).Select(x => x.Key)) + ".");
        return avertismente;
    }

    public static void Verifica(IConfiguration cfg, IHostEnvironment env, string conexiune)
    {
        var lipsa = Lipsa(cfg, env, conexiune);
        if (lipsa.Count > 0)
            throw new InvalidOperationException("Missing required settings (put them in ~/.config/markai/markai.env): " + string.Join(", ", lipsa) + ".");
    }
}
