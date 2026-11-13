namespace Sourcing_Tender_Management_System.Services.TopCommittee;

public static class TopCommitteeServiceCollectionExtensions
{
    /// <summary>Registers everything the Top Committee module needs (one line in Program.cs).</summary>
    public static IServiceCollection AddTopCommittee(this IServiceCollection services)
    {
        services.AddScoped<ITopCommitteeService, TopCommitteeService>();
        services.AddScoped<ITopCommitteeVotingService, TopCommitteeVotingService>();
        services.AddScoped<ITopCommitteeStatisticsService, TopCommitteeStatisticsService>();
        return services;
    }
}
