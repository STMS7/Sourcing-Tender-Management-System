using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models.Biometric;
using Sourcing_Tender_Management_System.Services.Biometric;

// Same namespace as the framework's own AddXxx methods, so Program.cs needs a single line and no extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class BiometricServiceCollectionExtensions
{
    /// <summary>Registers biometric sign-in (fingerprint passkeys and face recognition).</summary>
    public static IServiceCollection AddBiometrics(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BiometricOptions>(configuration.GetSection("Biometric"));
        services.AddMemoryCache();
        services.AddDataProtection();

        services.AddSingleton<ChallengeStore>();
        services.AddSingleton<BiometricThrottle>();
        services.AddScoped<PasskeyService>();
        services.AddScoped<FaceService>();
        services.AddScoped<BiometricSignInService>();
        services.AddHostedService<FaceSchemaInitializer>();
        return services;
    }
}

/// <summary>
/// Creates the face-template table of this module if it is missing. It lives in its own schema
/// ("biometric") and is not part of the shared EF model or its migrations, so the team's
/// migrations and "dotnet ef" commands are not affected.
/// </summary>
public sealed class FaceSchemaInitializer(IServiceScopeFactory scopes, ILogger<FaceSchemaInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.ExecuteSqlRawAsync(
                "IF SCHEMA_ID(N'biometric') IS NULL EXEC(N'CREATE SCHEMA [biometric]');", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[biometric].[FaceTemplates]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [biometric].[FaceTemplates] (
                        [Id] bigint IDENTITY(1,1) NOT NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        [ProtectedTemplate] varbinary(max) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [LastUsedAt] datetime2 NULL,
                        CONSTRAINT [PK_FaceTemplates] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_FaceTemplates_AspNetUsers_UserId] FOREIGN KEY ([UserId])
                            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_FaceTemplates_UserId] ON [biometric].[FaceTemplates] ([UserId]);
                END
                """, cancellationToken);
        }
        catch (Exception ex)
        {
            // Most likely the shared migrations are not applied yet; face sign-in works after the next start.
            logger.LogWarning(ex, "Could not prepare the biometric.FaceTemplates table.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
