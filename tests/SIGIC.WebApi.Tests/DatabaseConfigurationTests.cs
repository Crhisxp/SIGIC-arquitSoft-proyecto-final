using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Security.Services;
using SIGIC.Infrastructure;
using SIGIC.Infrastructure.Persistence;
using Xunit;

namespace SIGIC.WebApi.Tests;

public class DatabaseConfigurationTests
{
    [Theory]
    [InlineData("LAN-POS", OperationChannel.Pos, "sigic_pos_pool")]
    [InlineData("WAN-WEB", OperationChannel.Web, "sigic_web_pool")]
    [InlineData("WORKER", OperationChannel.Worker, "sigic_pos_pool")]
    public async Task Host_UsesFixtureConnectionsInRegisteredOptionsAndDbContext(
        string terminalScope, OperationChannel expectedChannel, string expectedPool)
    {
        // No InitializeAsync: this regression inspects the real host without database I/O.
        await using var factory = new CustomWebApplicationFactory();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.Replace(ServiceDescriptor.Scoped<ICurrentUserContext>(_ =>
                new CurrentUserContext(Guid.Empty, "test", Array.Empty<string>(), terminalScope, "127.0.0.1")));
        }));

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var pools = Assert.Single(host.Services.GetServices<DatabasePoolOptions>());
        var fixture = factory.Fixture;

        // Boolean assertions keep credentials out of failure output.
        Assert.True(configuration.GetConnectionString("DefaultConnection") == fixture.AppConnectionString);
        Assert.True(configuration.GetConnectionString("PosConnection") == fixture.PosPoolConnectionString);
        Assert.True(configuration.GetConnectionString("WebConnection") == fixture.WebPoolConnectionString);
        Assert.True(pools.DefaultConnection == fixture.AppConnectionString, "Default pool must use fixture credentials.");
        Assert.True(pools.PosConnection == fixture.PosPoolConnectionString, "POS pool must use fixture credentials.");
        Assert.True(pools.WebConnection == fixture.WebPoolConnectionString, "Web pool must use fixture credentials.");

        using var scope = host.Services.CreateScope();
        var channel = scope.ServiceProvider.GetRequiredService<IOperationChannelContext>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigicDbContext>();
        string expectedConnection = expectedChannel == OperationChannel.Web
            ? fixture.WebPoolConnectionString : fixture.PosPoolConnectionString;

        Assert.Equal(expectedChannel, channel.Channel);
        Assert.Equal(expectedPool, channel.PoolName);
        Assert.True(channel.ConnectionString == expectedConnection, "Channel must retain its fixture pool.");
        Assert.True(dbContext.Database.GetConnectionString() == expectedConnection, "EF must use the channel pool.");
    }
}
