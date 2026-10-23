using MarkAi.Api.Infrastructura;

// The pipeline lives in AplicatieMarkAi so the integration checks boot exactly the same app.
var builder = WebApplication.CreateBuilder(args);
AplicatieMarkAi.AdaugaServicii(builder, ConfigurareObligatorie.Conexiune(builder.Configuration));
var app = builder.Build();
AplicatieMarkAi.Configureaza(app);
app.Run();
