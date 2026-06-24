using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="AuthService"/>, wired against real ASP.NET Core Identity and
/// an in-memory EF Core store (no PostgreSQL needed).
/// </summary>
[TestFixture]
public sealed class AuthServiceTests
{
    private const string Email = "user@example.com";
    private const string Password = "Str0ng!Passw0rd";

    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private IAuthService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase($"authsvc-{Guid.NewGuid()}"));
        services.Configure<AuthOptions>(_ => { });
        services
            .AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 12;
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IAuthService, AuthService>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _sut = _scope.ServiceProvider.GetRequiredService<IAuthService>();
    }

    [TearDown]
    public void TearDown()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    [Test]
    public async Task Register_ValidCredentials_Succeeds()
    {
        var result = await _sut.RegisterAsync(Email, Password);
        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task Register_WeakPassword_Fails()
    {
        var result = await _sut.RegisterAsync(Email, "short");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task Login_CorrectPassword_IssuesToken()
    {
        await _sut.RegisterAsync(Email, Password);

        var result = await _sut.LoginAsync(Email, Password, ip: null, userAgent: null);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Token, Is.Not.Null.And.Not.Empty);
        Assert.That(result.ExpiresAt, Is.Not.Null);
    }

    [Test]
    public async Task Login_WrongPassword_FailsWithGenericError()
    {
        await _sut.RegisterAsync(Email, Password);

        var result = await _sut.LoginAsync(Email, "WrongPassw0rd!", ip: null, userAgent: null);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Token, Is.Null);
        Assert.That(result.Error, Does.Contain("Invalid"));
    }

    [Test]
    public async Task Login_UnknownUser_FailsWithoutRevealingExistence()
    {
        var result = await _sut.LoginAsync("ghost@example.com", Password, ip: null, userAgent: null);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task ExternalLogin_NewEmail_ProvisionsUserAndIssuesToken()
    {
        var result = await _sut.ExternalLoginAsync("new.google.user@example.com", ip: null, userAgent: null);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Token, Is.Not.Null.And.Not.Empty);
        Assert.That(result.ExpiresAt, Is.Not.Null);
    }

    [Test]
    public async Task ExternalLogin_IssuedToken_ValidatesToTheSameUser()
    {
        var result = await _sut.ExternalLoginAsync(Email, ip: null, userAgent: null);

        var principal = await _sut.ValidateSessionAsync(result.Token!);

        Assert.That(principal, Is.Not.Null);
        Assert.That(principal!.Email, Is.EqualTo(Email));
    }

    [Test]
    public async Task ExternalLogin_ExistingEmail_ReusesAccountWithoutDuplicating()
    {
        // Account already exists from a prior password registration.
        await _sut.RegisterAsync(Email, Password);

        var first = await _sut.ExternalLoginAsync(Email, ip: null, userAgent: null);
        var second = await _sut.ExternalLoginAsync(Email, ip: null, userAgent: null);

        Assert.That(first.Succeeded, Is.True);
        Assert.That(second.Succeeded, Is.True);

        var users = _scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .Count(u => u.Email == Email);
        Assert.That(users, Is.EqualTo(1));
    }

    [Test]
    public async Task ValidateSession_AfterLogin_ReturnsPrincipal()
    {
        await _sut.RegisterAsync(Email, Password);
        var login = await _sut.LoginAsync(Email, Password, ip: null, userAgent: null);

        var principal = await _sut.ValidateSessionAsync(login.Token!);

        Assert.That(principal, Is.Not.Null);
        Assert.That(principal!.Email, Is.EqualTo(Email));
    }

    [Test]
    public async Task Logout_InvalidatesSession()
    {
        await _sut.RegisterAsync(Email, Password);
        var login = await _sut.LoginAsync(Email, Password, ip: null, userAgent: null);

        await _sut.LogoutAsync(login.Token!);

        Assert.That(await _sut.ValidateSessionAsync(login.Token!), Is.Null);
    }
}
