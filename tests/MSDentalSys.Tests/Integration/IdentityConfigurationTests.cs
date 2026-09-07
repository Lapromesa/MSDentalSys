using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MSDentalSys.Tests.Integration;

public class IdentityConfigurationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory factory;

    public IdentityConfigurationTests(CustomWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void Lockout_ConfiguracionRealDeWeb_CincoFallosYUnMinuto()
    {
        var options = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromSeconds(60), options.Lockout.DefaultLockoutTimeSpan);
        Assert.True(options.Lockout.AllowedForNewUsers);
    }
}
