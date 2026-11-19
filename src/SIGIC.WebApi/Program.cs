using SIGIC.Infrastructure;
using SIGIC.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Servicios de controladores y serialización JSON
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient<SIGIC.Application.Analytics.Ports.IAuxiliarInteligenciaAnaliticaPort,
    SIGIC.Infrastructure.Adapters.Analytics.GeminiAnaliticaPort>(client =>
        client.MaxResponseContentBufferSize = 16 * 1024)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

// Registro del monolito modular de infraestructura y casos de uso de aplicación
builder.Services.AddSigicInfrastructure(builder.Configuration);

var app = builder.Build();

// Pipeline de middlewares de Clean Architecture y seguridad
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<UserContextMiddleware>();

app.UseRouting();
app.MapControllers();

app.Run();

public partial class Program { }
