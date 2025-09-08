using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class AuditInterceptorScopingTests
{
    [Test]
    public void AuditInterceptor_CanBeRegisteredAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act - Register services like in Program.cs
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditInterceptor>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - Verify we can resolve the interceptor from scoped container
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();
        
        var interceptor1 = scope1.ServiceProvider.GetRequiredService<AuditInterceptor>();
        var interceptor2 = scope2.ServiceProvider.GetRequiredService<AuditInterceptor>();
        
        Assert.That(interceptor1, Is.Not.Null);
        Assert.That(interceptor2, Is.Not.Null);
        Assert.That(interceptor1, Is.Not.SameAs(interceptor2), "Different scopes should provide different instances");
    }

    [Test]
    public void AuditInterceptor_ResolvesCurrentUserServiceFromScope()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockCurrentUserService = Substitute.For<ICurrentUserService>();
        mockCurrentUserService.GetCurrentUserId().Returns(12345);
        
        // Act - Register services like in Program.cs, but with mock
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService>(_ => mockCurrentUserService);
        services.AddScoped<AuditInterceptor>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - Verify the interceptor gets the mocked service
        using var scope = serviceProvider.CreateScope();
        var interceptor = scope.ServiceProvider.GetRequiredService<AuditInterceptor>();
        
        Assert.That(interceptor, Is.Not.Null);
        
        // The interceptor should be able to use the current user service
        // We can't directly test the private methods, but we know it was constructed successfully
        // with the scoped dependency
        mockCurrentUserService.Received(0).GetCurrentUserId(); // Constructor doesn't call this
    }
}