using MarkAi.Api.Endpoints;
using MarkAi.Api.Hubs;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Servicii;
using MarkAi.Api.Setari;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var conexiune = Environment.GetEnvironmentVariable("MARKAI_CONNECTION_STRING") ?? cfg.GetConnectionString("MarkAi")
    ?? throw new InvalidOperationException("Set MARKAI_CONNECTION_STRING.");
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
builder.Services.AddHostedService<GeneratorIntrebari>();
builder.Services.AddSingleton<Prezenta>();
builder.Services.AddSignalR();
builder.Services.AdaugaSetariCont();

ConfigurareAutentificare.Adauga(builder.Services, ConfigurareAutentificare.Optiuni(cfg, builder.Environment));

var origini = cfg.GetSection("Frontend:Origini").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origini).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<TratareErori>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Mark AI API"));
}

AutentificareEndpoints.Mapeaza(app);
app.MapeazaSetariCont();
ContinutEndpoints.Mapeaza(app);
ElevEndpoints.Mapeaza(app);
ProfesoriEndpoints.Mapeaza(app);
LiniaAiEndpoints.Mapeaza(app);
app.MapHub<JocHub>("/hub/joc");

app.Run();
