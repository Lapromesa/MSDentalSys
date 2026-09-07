using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MSDentalSys.Data.Context;
using MSDentalSys.Data.Models;
using MSDentalSys.Web.Controllers;
using MSDentalSys.Web.Models.ViewModels;
using Xunit;

namespace MSDentalSys.Tests.Controllers;

public class AccountControllerTests
{
    private const string Password = "Test1234!";
    private const string TestCookieName = "Test.Identity.Application";

    [Fact]
    public async Task Login_ConCredencialesCorrectas_AutenticaYRedirigeAlDashboard()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("login.correcto@example.test");
        var controller = database.CreateController();

        var result = await controller.Login(new LoginViewModel
        {
            Email = "login.correcto@example.test",
            Password = Password,
            RememberMe = false
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        Assert.Empty(controller.ModelState.Values.SelectMany(entry => entry.Errors));
        Assert.Contains(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_ConPasswordIncorrecta_NoAutenticaYAgregaError()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("login.password.incorrecta@example.test");
        var controller = database.CreateController();

        var result = await controller.Login(new LoginViewModel
        {
            Email = "login.password.incorrecta@example.test",
            Password = "PasswordIncorrecta9!",
            RememberMe = false
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains("no son correctos", GetModelStateError(controller), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_ConUsuarioInexistente_NoAutenticaYNoCreaUsuario()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Login(new LoginViewModel
        {
            Email = "usuario.inexistente@example.test",
            Password = Password,
            RememberMe = false
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains("no son correctos", GetModelStateError(controller), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await database.Context.Users.CountAsync());
        Assert.DoesNotContain(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_ConUsuarioInactivo_RechazaAccesoYAgregaError()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = await database.CreateUserAsync("login.inactivo@example.test", false);
        var controller = database.CreateController();

        var result = await controller.Login(new LoginViewModel
        {
            Email = "login.inactivo@example.test",
            Password = Password,
            RememberMe = false
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains("inactiva", GetModelStateError(controller), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await database.UserManager.GetAccessFailedCountAsync(user));
        Assert.DoesNotContain(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_ConRememberMeFalse_EmiteCookieDeSesionNoPersistente()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("login.remember.false@example.test");
        var controller = database.CreateController();

        var result = await controller.Login(new LoginViewModel
        {
            Email = "login.remember.false@example.test",
            Password = Password,
            RememberMe = false
        });

        Assert.IsType<RedirectToActionResult>(result);
        var cookie = database.GetSetCookieHeaders().Single(header => header.Contains(TestCookieName, StringComparison.Ordinal));
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ConRememberMeTrue_EmiteCookiePersistente()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("login.remember.true@example.test");
        var controller = database.CreateController();

        var result = await controller.Login(new LoginViewModel
        {
            Email = "login.remember.true@example.test",
            Password = Password,
            RememberMe = true
        });

        Assert.IsType<RedirectToActionResult>(result);
        var cookie = database.GetSetCookieHeaders().Single(header => header.Contains(TestCookieName, StringComparison.Ordinal));
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_EjecutaSignOutYRedirigeAlLogin()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.Login), redirect.ActionName);
        Assert.Contains(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_CuatroFallos_NoBloquea()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = await database.CreateUserAsync("cuatro@example.test");
        await FallarAsync(database, user, 4);
        Assert.Equal(4, await database.UserManager.GetAccessFailedCountAsync(user));
        Assert.False(await database.UserManager.IsLockedOutAsync(user));
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Odontologo")]
    [InlineData("Recepcionista")]
    public async Task Login_QuintoFallo_BloqueaPorUnMinutoEnTodosLosRoles(string role)
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = await database.CreateUserAsync("quinto@example.test");
        var roleManager = database.RoleManager;
        Assert.True((await roleManager.CreateAsync(new IdentityRole(role))).Succeeded);
        Assert.True((await database.UserManager.AddToRoleAsync(user, role)).Succeeded);
        Assert.True(await database.UserManager.GetLockoutEnabledAsync(user));
        await FallarAsync(database, user, 4);
        var before = DateTimeOffset.UtcNow;
        await FallarAsync(database, user, 1);
        var after = DateTimeOffset.UtcNow;
        Assert.True(await database.UserManager.IsLockedOutAsync(user));
        var end = await database.UserManager.GetLockoutEndDateAsync(user);
        Assert.NotNull(end);
        Assert.InRange(end.Value, before.AddSeconds(60), after.AddSeconds(60));
        Assert.Equal(0, await database.UserManager.GetAccessFailedCountAsync(user));
    }

    [Fact]
    public async Task Login_ConPasswordCorrectaDuranteBloqueo_RechazaAcceso()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = await database.CreateUserAsync("bloqueado@example.test");
        await FallarAsync(database, user, 5);
        var controller = database.CreateController();
        var result = await controller.Login(new LoginViewModel { Email = user.Email!, Password = Password });
        Assert.IsType<ViewResult>(result);
        Assert.Equal("La cuenta está temporalmente bloqueada por varios intentos fallidos. Intenta nuevamente en un minuto.", GetModelStateError(controller));
        Assert.DoesNotContain(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_BloqueoExpirado_PermiteAcceso()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = await database.CreateUserAsync("expirado@example.test");
        await FallarAsync(database, user, 5);
        Assert.True((await database.UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-1))).Succeeded);
        var result = await database.CreateController().Login(new LoginViewModel { Email = user.Email!, Password = Password });
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
        Assert.False(await database.UserManager.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task Login_ExitoAntesDelLimite_ReiniciaContador()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = await database.CreateUserAsync("reinicio@example.test");
        await FallarAsync(database, user, 3);
        Assert.Equal(3, await database.UserManager.GetAccessFailedCountAsync(user));
        var result = await database.CreateController().Login(new LoginViewModel { Email = user.Email!, Password = Password });
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, await database.UserManager.GetAccessFailedCountAsync(user));
        await FallarAsync(database, user, 1);
        Assert.Equal(1, await database.UserManager.GetAccessFailedCountAsync(user));
    }

    [Fact]
    public async Task Login_IntentoDuranteBloqueo_NoProlongaPlazo()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = await database.CreateUserAsync("plazo@example.test");
        await FallarAsync(database, user, 5);
        var end = await database.UserManager.GetLockoutEndDateAsync(user);
        await FallarAsync(database, user, 1);
        Assert.Equal(end, await database.UserManager.GetLockoutEndDateAsync(user));
        Assert.Equal(0, await database.UserManager.GetAccessFailedCountAsync(user));
    }

    private static async Task FallarAsync(TestDatabase database, ApplicationUser user, int attempts)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var result = await database.CreateController().Login(new LoginViewModel
            {
                Email = user.Email!, Password = "Incorrecta123!"
            });
            Assert.IsType<ViewResult>(result);
            Assert.DoesNotContain(TestCookieName, database.GetSetCookieText(), StringComparison.Ordinal);
        }
    }

    private static string GetModelStateError(AccountController controller)
    {
        return string.Join(" ", controller.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private HttpContext? _currentHttpContext;

        private TestDatabase(SqliteConnection connection, ApplicationDbContext context, ServiceProvider services)
        {
            _connection = connection;
            Context = context;
            _services = services;
        }

        public ApplicationDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager => _services.GetRequiredService<UserManager<ApplicationUser>>();
        public RoleManager<IdentityRole> RoleManager => _services.GetRequiredService<RoleManager<IdentityRole>>();

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var services = new ServiceCollection()
                .AddSingleton(context)
                .AddLogging()
                .AddHttpContextAccessor();
            services.AddMvcCore();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                })
                .AddCookie(IdentityConstants.ApplicationScheme, options =>
                {
                    options.Cookie.Name = TestCookieName;
                });
            services.AddIdentityCore<ApplicationUser>(identityOptions =>
                {
                    identityOptions.Password.RequiredLength = 8;
                    identityOptions.Password.RequireDigit = true;
                    identityOptions.Password.RequireUppercase = true;
                    identityOptions.Password.RequireLowercase = true;
                    identityOptions.Password.RequireNonAlphanumeric = true;
                    identityOptions.SignIn.RequireConfirmedAccount = false;
                    identityOptions.Lockout.MaxFailedAccessAttempts = 5;
                    identityOptions.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(60);
                    identityOptions.Lockout.AllowedForNewUsers = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                ;
            var serviceProvider = services.BuildServiceProvider();

            return new TestDatabase(connection, context, serviceProvider);
        }

        public async Task<ApplicationUser> CreateUserAsync(string email, bool state = true)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                Nombre = "Usuario de Prueba",
                Apellido = "Autenticacion",
                Estado = state,
                FechaCreacion = DateTime.Now
            };
            var result = await UserManager.CreateAsync(user, Password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            }

            return user;
        }

        public AccountController CreateController()
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = _services
            };
            _currentHttpContext = httpContext;
            _services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

            var controller = new AccountController(
                _services.GetRequiredService<SignInManager<ApplicationUser>>(),
                UserManager)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext,
                    RouteData = new RouteData(),
                    ActionDescriptor = new ControllerActionDescriptor()
                }
            };
            controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
            return controller;
        }

        public IReadOnlyList<string> GetSetCookieHeaders()
        {
            if (_currentHttpContext is null || !_currentHttpContext.Response.Headers.TryGetValue("Set-Cookie", out var values))
            {
                return [];
            }

            return values.Where(value => value is not null).Select(value => value!).ToArray();
        }

        public string GetSetCookieText()
        {
            return string.Join("\n", GetSetCookieHeaders());
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
            _services.Dispose();
        }
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
