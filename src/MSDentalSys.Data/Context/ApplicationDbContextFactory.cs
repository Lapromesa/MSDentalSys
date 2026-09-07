using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;

namespace MSDentalSys.Data.Context
{
    public class ApplicationDbContextFactory
        : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Los proveedores estándar interpretan los argumentos enviados después de -- en EF CLI.
            var hostConfiguration = new ConfigurationBuilder()
                .AddEnvironmentVariables("ASPNETCORE_")
                .AddEnvironmentVariables("DOTNET_")
                .AddCommandLine(args)
                .Build();
            using var hostConfigurationLifetime = hostConfiguration as IDisposable;
            var environment = hostConfiguration["environment"] ?? "Production";
            var webDirectory = FindWebDirectory(hostConfiguration["contentRoot"]);
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(webDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);

            if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            {
                // Leemos el identificador de Web sin duplicarlo ni crear una referencia Data -> Web.
                var project = XDocument.Load(Path.Combine(webDirectory, "MSDentalSys.Web.csproj"));
                var secretsId = project.Descendants()
                    .FirstOrDefault(element => element.Name.LocalName == "UserSecretsId")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(secretsId))
                {
                    throw new InvalidOperationException("No se encontró UserSecretsId en MSDentalSys.Web.csproj para el entorno Development.");
                }
                configurationBuilder.AddUserSecrets(secretsId, reloadOnChange: false);
            }

            var configuration = configurationBuilder
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            using var configurationLifetime = configuration as IDisposable;
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No se encontró la cadena de conexión 'DefaultConnection'. Configure ConnectionStrings:DefaultConnection en la configuración de MSDentalSys.Web, User Secrets (Development) o ConnectionStrings__DefaultConnection.");
            }

            var optionsBuilder =
                new DbContextOptionsBuilder<ApplicationDbContext>();

            optionsBuilder.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

            return new ApplicationDbContext(optionsBuilder.Options);
        }

        private static string FindWebDirectory(string? contentRoot)
        {
            if (!string.IsNullOrWhiteSpace(contentRoot))
            {
                var directory = Path.GetFullPath(contentRoot);
                if (File.Exists(Path.Combine(directory, "MSDentalSys.Web.csproj")))
                    return directory;
                throw new InvalidOperationException("El contentRoot indicado debe contener MSDentalSys.Web.csproj.");
            }

            // También buscamos desde el ensamblado para no depender solo del directorio actual.
            foreach (var origin in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory,
                Path.GetDirectoryName(typeof(ApplicationDbContextFactory).Assembly.Location)! })
            {
                for (var directory = new DirectoryInfo(origin); directory != null; directory = directory.Parent)
                {
                    foreach (var candidate in new[] { directory.FullName,
                        Path.Combine(directory.FullName, "MSDentalSys.Web"),
                        Path.Combine(directory.FullName, "src", "MSDentalSys.Web") })
                    {
                        if (File.Exists(Path.Combine(candidate, "MSDentalSys.Web.csproj")))
                            return candidate;
                    }
                }
            }
            throw new InvalidOperationException("No se pudo localizar MSDentalSys.Web. Indique su carpeta mediante -- --contentRoot RUTA --environment Development.");
        }
    }
}
