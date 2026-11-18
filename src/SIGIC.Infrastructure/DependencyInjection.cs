using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SIGIC.Application.CashManagement.Handlers;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Handlers;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Handlers;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.ProductDetailsPricing.Services;
using SIGIC.Application.SalesPOS.Handlers;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Security.Services;
using SIGIC.Infrastructure.Adapters.CashManagement;
using SIGIC.Infrastructure.Adapters.Common;
using SIGIC.Infrastructure.Adapters.Inventory;
using SIGIC.Infrastructure.Adapters.OrdersWeb;
using SIGIC.Infrastructure.Adapters.ProductDetailsPricing;
using SIGIC.Infrastructure.Adapters.SalesPOS;
using SIGIC.Infrastructure.Adapters.Security;
using SIGIC.Infrastructure.BackgroundServices;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Security;

namespace SIGIC.Infrastructure;

public record DatabasePoolOptions(string DefaultConnection, string PosConnection, string WebConnection);

public static class DependencyInjection
{
    public static IServiceCollection AddSigicInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
    {
        string defaultConn = configuration.GetConnectionString(connectionStringName)
            ?? configuration["DatabaseSettings:ConnectionString"]
            ?? Environment.GetEnvironmentVariable("SIGIC_DB_CONNECTION_STRING")
            ?? BuildApplicationConnectionString(configuration);

        string posConn = configuration.GetConnectionString("PosConnection")
            ?? configuration["DatabaseSettings:PosConnection"]
            ?? Environment.GetEnvironmentVariable("SIGIC_POS_CONNECTION_STRING")
            ?? throw new InvalidOperationException("La cadena de conexión 'PosConnection' (o SIGIC_POS_CONNECTION_STRING) es requerida y debe estar configurada explícitamente sin fallback a DefaultConnection.");

        string webConn = configuration.GetConnectionString("WebConnection")
            ?? configuration["DatabaseSettings:WebConnection"]
            ?? Environment.GetEnvironmentVariable("SIGIC_WEB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("La cadena de conexión 'WebConnection' (o SIGIC_WEB_CONNECTION_STRING) es requerida y debe estar configurada explícitamente sin fallback a DefaultConnection.");

        services.AddSingleton(new DatabasePoolOptions(defaultConn, posConn, webConn));

        // Segregación contextual por canal (pos_pool vs web_pool)
        services.AddScoped<IOperationChannelContext, OperationChannelContext>();

        // Persistencia y DbContext sobre PostgreSQL 16 con selección dinámica de pool
        services.AddDbContext<SigicDbContext>((sp, options) =>
        {
            var channelContext = sp.GetRequiredService<IOperationChannelContext>();
            options.UseNpgsql(channelContext.ConnectionString);
        });

        // Unidad de Trabajo Scoped con soporte para PgBouncer
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Adaptadores de Inventario y Almacén
        services.AddScoped<IInventoryStockPort, PostgresInventoryStockPort>();
        services.AddScoped<IInventoryAdjustmentRepository, PostgresInventoryAdjustmentRepository>();
        services.AddScoped<IKardexPort, PostgresKardexPort>();
        services.AddScoped<IWarehouseConfigurationPort, ConfigurationWarehousePort>();

        // Adaptadores de Ventas Mostrador POS
        services.AddScoped<ISalesPOSRepository, PostgresSalesPOSRepository>();
        services.AddScoped<ICashShiftPort, PostgresCashShiftPort>();
        services.AddScoped<IDigitalPaymentVerificationPort, PostgresDigitalPaymentVerificationPort>();

        // Adaptadores de Pedidos Web
        services.AddScoped<IWebOrderRepository, PostgresWebOrderRepository>();

        // Adaptadores de Caja y Tesorería
        services.AddScoped<ICashRegisterRepository, PostgresCashRegisterRepository>();
        services.AddScoped<IVoucherStoragePort, LocalVoucherStoragePort>();
        services.AddScoped<ITransferGuidePort, SystemTransferGuidePort>();
        services.AddScoped<ICashAlertNotificationPort, OutboxCashAlertNotificationPort>();

        // Adaptadores de PDP, Mediciones y Descuentos
        services.AddScoped<IProductQueryPort, PostgresProductQueryPort>();
        services.AddScoped<ITariffRepository, PostgresTariffRepository>();

        // Seguridad y Auditoría
        services.AddScoped<IAuditLogPort, PostgresAuditLogPort>();
        services.AddScoped<IAccountLockoutRepository, PostgresAccountLockoutRepository>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        // Criptografía JWT RS256 con modos explícitos: Emisión (Privada) y Validación (Pública sola)
        ConfigureJwtService(services, configuration);

        // Servicios Compartidos
        services.AddScoped<ICustomerProfilePort, PostgresCustomerProfilePort>();
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Contexto de Usuario: TryAddScoped garantiza que NO se sobrescribe un contexto registrado
        services.TryAddScoped<ICurrentUserContext>(sp => CurrentUserContextAccessor.Current ?? new CurrentUserContext(
            Guid.Empty, "system", new[] { "System" }, "LAN-POS", "127.0.0.1"));
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        // Handlers de Casos de Uso de Application
        services.AddScoped<AjustarInventarioCommandHandler>();
        services.AddScoped<RegistrarVentaPOSCommandHandler>();
        services.AddScoped<ReservarPedidoWebCommandHandler>();
        services.AddScoped<ExpirarReservasWebCommandHandler>();
        services.AddScoped<RegistrarArqueoCiegoCommandHandler>();
        services.AddScoped<ValidarVoucherPagoCommandHandler>();
        services.AddScoped<CalcularPDPYDescuentoQueryHandler>();

        // Background Service de Expiración de Reservas Web
        services.AddHostedService<ReservationExpirationBackgroundService>();

        return services;
    }

    private static void ConfigureJwtService(IServiceCollection services, IConfiguration configuration)
    {
        string? privateKeyPem = configuration["JwtSettings:PrivateKeyPem"]
            ?? Environment.GetEnvironmentVariable("SIGIC_JWT_PRIVATE_KEY_PEM");
        string? publicKeyPem = configuration["JwtSettings:PublicKeyPem"]
            ?? Environment.GetEnvironmentVariable("SIGIC_JWT_PUBLIC_KEY_PEM");

        RsaSecurityKey? signingKey = null;
        RsaSecurityKey validationKey;

        if (!string.IsNullOrWhiteSpace(privateKeyPem))
        {
            var rsaPrivate = RSA.Create();
            rsaPrivate.ImportFromPem(privateKeyPem);
            signingKey = new RsaSecurityKey(rsaPrivate) { KeyId = "sigic-core-signing-key" };

            if (!string.IsNullOrWhiteSpace(publicKeyPem))
            {
                var rsaPub = RSA.Create();
                rsaPub.ImportFromPem(publicKeyPem);
                validationKey = new RsaSecurityKey(rsaPub) { KeyId = "sigic-core-signing-key" };
            }
            else
            {
                var rsaPub = RSA.Create(rsaPrivate.ExportParameters(false));
                validationKey = new RsaSecurityKey(rsaPub) { KeyId = "sigic-core-signing-key" };
            }
        }
        else if (!string.IsNullOrWhiteSpace(publicKeyPem))
        {
            // Modo exclusivo de validación con clave pública sola
            var rsaPub = RSA.Create();
            rsaPub.ImportFromPem(publicKeyPem);
            validationKey = new RsaSecurityKey(rsaPub) { KeyId = "sigic-core-signing-key" };
        }
        else
        {
            throw new InvalidOperationException("Se requiere configurar JwtSettings:PrivateKeyPem o JwtSettings:PublicKeyPem (o las variables de entorno SIGIC_JWT_PRIVATE_KEY_PEM / SIGIC_JWT_PUBLIC_KEY_PEM) para inicializar IJwtTokenService.");
        }

        services.AddSingleton<IJwtTokenService>(new RsaJwtTokenService(signingKey, validationKey, "SIGIC_Backend"));
    }

    private static string BuildApplicationConnectionString(IConfiguration configuration)
    {
        string host = configuration["DatabaseSettings:Host"]
            ?? Environment.GetEnvironmentVariable("SIGIC_DB_HOST")
            ?? "localhost";
        string port = configuration["DatabaseSettings:Port"]
            ?? Environment.GetEnvironmentVariable("SIGIC_DB_PORT")
            ?? "54329";
        string database = configuration["DatabaseSettings:Database"]
            ?? Environment.GetEnvironmentVariable("SIGIC_DB_NAME")
            ?? "postgres";
        string username = configuration["DatabaseSettings:Username"]
            ?? Environment.GetEnvironmentVariable("SIGIC_DB_USER")
            ?? "sigic_app";
        string password = configuration["DatabaseSettings:Password"]
            ?? Environment.GetEnvironmentVariable("SIGIC_DB_PASSWORD")
            ?? throw new InvalidOperationException("La contraseña de base de datos es obligatoria (configure DatabaseSettings:Password o SIGIC_DB_PASSWORD).");

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};";
    }
}
