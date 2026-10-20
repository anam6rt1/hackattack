using MarkAi.Api.Endpoints;
using MarkAi.Api.Hubs;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Api.Setari;
using MarkAi.Data;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Infrastructura;

/// <summary>
/// The whole API pipeline (services, middleware, endpoints) in one place, so Program.cs and the integration
/// checks run exactly the same app. Tests register their fakes after <see cref="AdaugaServicii"/>; the last
/// registration of a service wins.
/// </summary>
public static class AplicatieMarkAi
{
    /// <param name="conexiune">The PostgreSQL connection string; passed explicitly so a test can never pick up the real one.</param>
    public static void AdaugaServicii(WebApplicationBuilder builder, string conexiune)
    {
        var cfg = builder.Configuration;
        ConfigurareObligatorie.Verifica(cfg, builder.Environment, conexiune);
        Securitate.AdaugaServicii(builder);

        builder.Services.AddDbContext<MarkAiDbContext>(o => o.UseNpgsql(conexiune,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", "mark_ai")));
        builder.Services.AddScoped<ProgresService>();
        builder.Services.AddScoped<RezervareService>();
        builder.Services.AddScoped<TutorTelefon>();
        builder.Services.AddHttpClient("gemini", c => c.Timeout = TimeSpan.FromSeconds(90));
        builder.Services.AddSingleton<ModelAi>();
        builder.Services.AddHttpClient("twilio", c => c.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddSingleton<TwilioApeluri>();
        builder.Services.AddSingleton<Stocare>();
        builder.Services.AddSingleton<CoadaIntrebari>();
        // Background jobs are singletons as well as hosted services, so the checks can run them on demand (RuleazaOdataAsync).
        builder.Services.AddSingleton<GeneratorIntrebari>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<GeneratorIntrebari>());
        builder.Services.AddSingleton<Servicii.Triviador.MotorTriviador>();
        // Faster than the defaults (15 s / 30 s), so a player whose phone drops off is seen as gone within ~15 s.
        AdaugaMesagerie(builder);
        builder.Services.AddScoped<CoduriVerificare>();
        builder.Services.AddScoped<StergereCont>();
        builder.Services.AddScoped<ServiciuOrganizatii>();
        builder.Services.AddSingleton<CuratareConturi>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<CuratareConturi>());
        builder.Services.AddSignalR(o => { o.KeepAliveInterval = TimeSpan.FromSeconds(5); o.ClientTimeoutInterval = TimeSpan.FromSeconds(15); });
        builder.Services.AdaugaSetariCont();

        ConfigurareAutentificare.Adauga(builder.Services, ConfigurareAutentificare.Optiuni(cfg, builder.Environment));

        var origini = cfg.GetSection("Frontend:Origini").Get<string[]>() ?? ["http://localhost:3000"];
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origini).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        builder.Services.AddOpenApi();
    }

    /// <summary>
    /// Email and SMS providers, chosen in configuration so a provider can be replaced without touching the rest of the app:
    /// <c>Email:Furnizor</c> = smtp | jurnal (default: smtp when SetariEmail:Host is set), <c>Sms:Furnizor</c> = twilio | jurnal (default: jurnal).
    /// </summary>
    private static void AdaugaMesagerie(WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;
        builder.Services.AddSingleton<ExpeditorJurnal>();
        var email = cfg["Email:Furnizor"] ?? (string.IsNullOrWhiteSpace(cfg["SetariEmail:Host"]) ? "jurnal" : "smtp");
        switch (email)
        {
            case "smtp": builder.Services.AddSingleton<IExpeditorEmail, ExpeditorEmailSmtp>(); break;
            case "jurnal": builder.Services.AddSingleton<IExpeditorEmail>(sp => sp.GetRequiredService<ExpeditorJurnal>()); break;
            default: throw new InvalidOperationException($"Unknown Email:Furnizor '{email}' (use smtp or jurnal).");
        }
        var sms = cfg["Sms:Furnizor"] ?? "jurnal";
        switch (sms)
        {
            case "twilio": builder.Services.AddSingleton<IExpeditorSms, ExpeditorSmsTwilio>(); break;
            case "jurnal": builder.Services.AddSingleton<IExpeditorSms>(sp => sp.GetRequiredService<ExpeditorJurnal>()); break;
            default: throw new InvalidOperationException($"Unknown Sms:Furnizor '{sms}' (use twilio or jurnal).");
        }
        builder.Services.AddScoped<Posta>();
        builder.Services.AddSingleton<ProcesorMesaje>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcesorMesaje>());
    }

    public static void Configureaza(WebApplication app)
    {
        foreach (var avertisment in ConfigurareObligatorie.Avertismente(app.Configuration)) app.Logger.LogWarning("{Avertisment}", avertisment);
        if (app.Services.GetRequiredService<IExpeditorEmail>().Nume == "jurnal")
            app.Logger.LogWarning("Emails are not sent (Email:Furnizor = jurnal): codes appear on the admin page \"Mesaje trimise\".");
        if (app.Services.GetRequiredService<IExpeditorSms>().Nume == "jurnal")
            app.Logger.LogWarning("SMS are not sent (Sms:Furnizor = jurnal): codes appear on the admin page \"Mesaje trimise\".");
        if (!app.Services.GetRequiredService<ModelAi>().Configurat)
            app.Logger.LogWarning("No AI key (GEMINI_API_KEY or ANTHROPIC_API_KEY): question generation and the phone tutor are off.");

        Securitate.FoloseteInainteDeAutentificare(app);
        app.UseMiddleware<TratareErori>();
        // Twilio webhooks fail silently on the caller's side ("could not reach your TwiML server"), so log every one with its outcome.
        app.Use(async (ctx, next) =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/twilio")) { await next(); return; }
            var ceas = System.Diagnostics.Stopwatch.StartNew();
            await next();
            // The query string holds the call's key, so it is never logged; the session id is enough to follow a call.
            app.Logger.LogInformation("Webhook Twilio {Metoda} {Cale} (sesiune {Sesiune}, {Tip}, semnătură: {Semnat}, {Agent}) -> {Status} în {Ms} ms",
                ctx.Request.Method, ctx.Request.Path, ctx.Request.Query["s"].ToString(), ctx.Request.ContentType,
                ctx.Request.Headers.ContainsKey("X-Twilio-Signature"), ctx.Request.Headers.UserAgent.ToString(), ctx.Response.StatusCode, ceas.ElapsedMilliseconds);
        });
        app.UseCors();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Mark AI API"));
        }

        AutentificareEndpoints.Mapeaza(app);
        app.MapeazaSetariCont();
        ContinutEndpoints.Mapeaza(app);
        QuizEndpoints.Mapeaza(app);
        AdminEndpoints.Mapeaza(app);
        OrganizatiiEndpoints.Mapeaza(app);
        ElevEndpoints.Mapeaza(app);
        ProfesoriEndpoints.Mapeaza(app);
        LiniaAiEndpoints.Mapeaza(app);
        app.MapHub<JocHub>("/hub/joc");
    }
}
