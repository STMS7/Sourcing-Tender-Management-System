using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Services.OfferExtras;

// Same namespace as the framework's own AddXxx methods, so Program.cs needs a single line and no extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class OfferExtrasServiceCollectionExtensions
{
    /// <summary>Registers sample pictures and bank guarantees for supplier offers, plus the public verification page.</summary>
    public static IServiceCollection AddOfferExtras(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<OfferExtrasStore>();
        services.AddScoped<OfferExtrasFiles>();
        services.AddHostedService<OfferExtrasSchemaInitializer>();
        return services;
    }
}

/// <summary>
/// Creates the two tables of this feature when they are missing. They live in their own schema ("offerx") and are not
/// part of the shared EF model or its migrations, so "dotnet ef" and the team's migrations are not affected.
/// </summary>
public sealed class OfferExtrasSchemaInitializer(IServiceScopeFactory scopes, ILogger<OfferExtrasSchemaInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.ExecuteSqlRawAsync(
                "IF SCHEMA_ID(N'offerx') IS NULL EXEC(N'CREATE SCHEMA [offerx]');", cancellationToken);

            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[offerx].[BidSamples]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [offerx].[BidSamples] (
                        [Id] bigint IDENTITY(1,1) NOT NULL,
                        [BidId] int NOT NULL,
                        [StoredName] nvarchar(80) NOT NULL,
                        [OriginalName] nvarchar(255) NOT NULL,
                        [ContentType] nvarchar(60) NOT NULL,
                        [SizeBytes] bigint NOT NULL,
                        [Caption] nvarchar(200) NULL,
                        [UploadedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_BidSamples] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_BidSamples_Bids_BidId] FOREIGN KEY ([BidId])
                            REFERENCES [dbo].[Bids] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_BidSamples_BidId] ON [offerx].[BidSamples] ([BidId]);
                END
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[offerx].[BidGuarantees]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [offerx].[BidGuarantees] (
                        [Id] bigint IDENTITY(1,1) NOT NULL,
                        [BidId] int NOT NULL,
                        [Kind] nvarchar(10) NOT NULL,
                        [Purpose] nvarchar(40) NOT NULL,
                        [GuaranteeNumber] nvarchar(40) NOT NULL,
                        [IssuerName] nvarchar(150) NOT NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [Currency] nvarchar(10) NOT NULL,
                        [DurationMonths] int NOT NULL,
                        [IssuedOn] date NOT NULL,
                        [ExpiresOn] date NOT NULL,
                        [StoredName] nvarchar(80) NOT NULL,
                        [OriginalName] nvarchar(255) NOT NULL,
                        [VerifyCode] nvarchar(20) NOT NULL,
                        [VerifyCount] int NOT NULL CONSTRAINT [DF_BidGuarantees_VerifyCount] DEFAULT 0,
                        [LastVerifiedAt] datetime2 NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_BidGuarantees] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_BidGuarantees_Bids_BidId] FOREIGN KEY ([BidId])
                            REFERENCES [dbo].[Bids] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_BidGuarantees_BidId] ON [offerx].[BidGuarantees] ([BidId]);
                    CREATE UNIQUE INDEX [UX_BidGuarantees_Number] ON [offerx].[BidGuarantees] ([GuaranteeNumber]);
                END
                """, cancellationToken);
        }
        catch (Exception ex)
        {
            // Most likely the shared migrations are not applied yet; the feature works after the next start.
            logger.LogWarning(ex, "Could not prepare the offerx tables (sample pictures and guarantees).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
