using FinRiskLensAI.Data.DI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinRiskLensAI.Data.DbContextEDMX
{
    /// <summary>
    /// Used only by the EF tools (dotnet ef migrations …). Reads the same
    /// Database:ConnectionStrings:PostgreSQL the app does, and configures the provider
    /// through <see cref="PostgresSetup"/> so migrations match the running model.
    /// <para>
    /// Neon: point migrations at the direct endpoint (drop "-pooler" from the host) if
    /// the pooled one ever misbehaves — override with the environment variable
    /// Database__ConnectionStrings__PostgreSQL rather than editing the settings file.
    /// </para>
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var configuration = BuildConfiguration();

            var connectionString = configuration["Database:ConnectionStrings:PostgreSQL"]
                ?? throw new InvalidOperationException(
                    "Database:ConnectionStrings:PostgreSQL not found in appsettings.Development.json");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            PostgresSetup.Configure(optionsBuilder, connectionString);
            return new ApplicationDbContext(optionsBuilder.Options);
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            var basePath = ResolveStartupProjectPath();
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();
        }

        private static string ResolveStartupProjectPath()
        {
            var candidate = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(candidate, "appsettings.Development.json")))
                return candidate;

            var dir = new DirectoryInfo(candidate);
            while (dir != null && dir.GetFiles("*.sln").Length == 0)
                dir = dir.Parent;

            // The startup project is whichever top-level folder carries the settings file.
            // Found by content, not by name — the folder has been renamed once already.
            var startupProject = dir?.GetDirectories()
                .FirstOrDefault(d => File.Exists(Path.Combine(d.FullName, "appsettings.Development.json")));
            if (startupProject != null)
                return startupProject.FullName;

            return candidate;
        }
    }
}
