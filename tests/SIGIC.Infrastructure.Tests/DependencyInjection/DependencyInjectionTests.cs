using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using SIGIC.Infrastructure;
using Xunit;

namespace SIGIC.Infrastructure.Tests.DependencyInjection;

public class DependencyInjectionTests
{
    private static IConfiguration CreateValidConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", "Host=localhost;Port=54329;Database=sigic_test;Username=sigic_app;Password=secret;" },
            { "ConnectionStrings:PosConnection", "Host=localhost;Port=6432;Database=sigic_pos_pool;Username=sigic_app;Password=secret;" },
            { "ConnectionStrings:WebConnection", "Host=localhost;Port=6432;Database=sigic_web_pool;Username=sigic_app;Password=secret;" },
            { "JwtSettings:PublicKeyPem", Fixtures.PostgreSqlTestFixture.TestPublicKeyPem }
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public void AddSigicInfrastructure_RegistersAndResolvesAllPortsAndHandlers()
    {
        var services = new ServiceCollection();
        var configuration = CreateValidConfiguration();

        services.AddSigicInfrastructure(configuration);

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        // Inventory
        Assert.NotNull(sp.GetRequiredService<IInventoryStockPort>());
        Assert.NotNull(sp.GetRequiredService<IInventoryAdjustmentRepository>());
        Assert.NotNull(sp.GetRequiredService<IKardexPort>());
        Assert.NotNull(sp.GetRequiredService<IWarehouseConfigurationPort>());

        // SalesPOS
        Assert.NotNull(sp.GetRequiredService<ISalesPOSRepository>());
        Assert.NotNull(sp.GetRequiredService<ICashShiftPort>());
        Assert.NotNull(sp.GetRequiredService<IDigitalPaymentVerificationPort>());

        // OrdersWeb
        Assert.NotNull(sp.GetRequiredService<IWebOrderRepository>());

        // CashManagement
        Assert.NotNull(sp.GetRequiredService<ICashRegisterRepository>());
        Assert.NotNull(sp.GetRequiredService<IVoucherStoragePort>());
        Assert.NotNull(sp.GetRequiredService<ITransferGuidePort>());
        Assert.NotNull(sp.GetRequiredService<ICashAlertNotificationPort>());

        // PDP & Pricing
        Assert.NotNull(sp.GetRequiredService<IProductQueryPort>());
        Assert.NotNull(sp.GetRequiredService<ITariffRepository>());

        // Security
        Assert.NotNull(sp.GetRequiredService<IAuditLogPort>());
        Assert.NotNull(sp.GetRequiredService<IAccountLockoutRepository>());
        Assert.NotNull(sp.GetRequiredService<IPasswordHasher>());
        Assert.NotNull(sp.GetRequiredService<IJwtTokenService>());
        Assert.NotNull(sp.GetRequiredService<ICurrentUserContext>());
        Assert.NotNull(sp.GetRequiredService<IAuthorizationService>());

        // Shared / Common
        Assert.NotNull(sp.GetRequiredService<ICustomerProfilePort>());
        Assert.NotNull(sp.GetRequiredService<IDateTimeProvider>());
        Assert.NotNull(sp.GetRequiredService<IUnitOfWork>());

        // Handlers
        Assert.NotNull(sp.GetRequiredService<AjustarInventarioCommandHandler>());
        Assert.NotNull(sp.GetRequiredService<RegistrarVentaPOSCommandHandler>());
        Assert.NotNull(sp.GetRequiredService<ReservarPedidoWebCommandHandler>());
        Assert.NotNull(sp.GetRequiredService<ExpirarReservasWebCommandHandler>());
        Assert.NotNull(sp.GetRequiredService<RegistrarArqueoCiegoCommandHandler>());
        Assert.NotNull(sp.GetRequiredService<ValidarVoucherPagoCommandHandler>());
        Assert.NotNull(sp.GetRequiredService<CalcularPDPYDescuentoQueryHandler>());
    }

    [Fact]
    public void Architecture_InfrastructureProjectReferences_OnlyDomainAndApplication()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null && !File.Exists(Path.Combine(current, "SIGIC.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        Assert.NotNull(current);
        string projectPath = Path.Combine(current, "src", "SIGIC.Infrastructure", "SIGIC.Infrastructure.csproj");
        Assert.True(File.Exists(projectPath), $"El archivo de proyecto debe existir en: {projectPath}");

        var doc = XDocument.Load(projectPath);
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(pr => (string?)pr.Attribute("Include"))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        Assert.Equal(2, projectReferences.Count);
        Assert.Contains(projectReferences, r => r!.EndsWith("SIGIC.Domain.csproj"));
        Assert.Contains(projectReferences, r => r!.EndsWith("SIGIC.Application.csproj"));
    }

    [Fact]
    public void AddSigicInfrastructure_PreRegisteredCurrentUserContext_IsNotOverwritten()
    {
        var services = new ServiceCollection();
        var configuration = CreateValidConfiguration();

        var customUserId = Guid.NewGuid();
        var customContext = new CurrentUserContext(
            customUserId, "custom_operator", new[] { "Cajero", "Tesoreria" }, "CUSTOM-SCOPE", "192.168.1.100");

        // Registrar previamente ICurrentUserContext antes de AddSigicInfrastructure
        services.AddScoped<ICurrentUserContext>(_ => customContext);

        services.AddSigicInfrastructure(configuration);

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var scope = serviceProvider.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<ICurrentUserContext>();

        Assert.Same(customContext, resolved);
        Assert.Equal(customUserId, resolved.UserId);
        Assert.Equal("custom_operator", resolved.Username);
        Assert.Contains("Tesoreria", resolved.Roles);
    }

    [Fact]
    public void AddSigicInfrastructure_MissingPosOrWebConnection_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var missingPosConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=sigic;" },
                { "ConnectionStrings:WebConnection", "Host=localhost;Database=sigic_web;" }
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => services.AddSigicInfrastructure(missingPosConfig));

        var missingWebConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=sigic;" },
                { "ConnectionStrings:PosConnection", "Host=localhost;Database=sigic_pos;" }
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => services.AddSigicInfrastructure(missingWebConfig));
    }

    [Fact]
    public void AddSigicInfrastructure_MissingJwtKeys_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var configWithoutJwt = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=sigic;" },
                { "ConnectionStrings:PosConnection", "Host=localhost;Database=sigic_pos;" },
                { "ConnectionStrings:WebConnection", "Host=localhost;Database=sigic_web;" }
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddSigicInfrastructure(configWithoutJwt));
        Assert.Contains("JwtSettings", ex.Message);
    }
}
