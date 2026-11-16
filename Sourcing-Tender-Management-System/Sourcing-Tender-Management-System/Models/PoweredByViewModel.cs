namespace Sourcing_Tender_Management_System.Models;

/// <summary>One person shown on the public "Powered by" page.</summary>
public sealed record TeamMember(
    string Id,
    string FullName,
    string? Role,
    string? Email,
    string? LinkedInUrl,
    string? GitHubUrl,
    string? PhotoPath = null,
    string? PhotoObjectPosition = null)
{
    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            return parts.Length == 1
                ? parts[0][..1].ToUpperInvariant()
                : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }
}

/// <summary>Static team list for the page. Edit here; there is no database table for it.</summary>
public sealed class PoweredByViewModel
{
    public IReadOnlyList<TeamMember> Members { get; init; } = Array.Empty<TeamMember>();

    // Photos: set PhotoPath to a local file such as "/img/team/lujain.jpg" once supplied.
    public static PoweredByViewModel Create() => new()
    {
        Members = new[]
        {
            new TeamMember("lujain", "Lujain Emad Okour", "Scrum Master", "okourlujain@gmail.com",
                "https://www.linkedin.com/in/lujain-okour-06903b313/", "https://github.com/lujainokour", "/img/team/lujain.jpg"),
            new TeamMember("raghad", "Raghad Mahmoud Alkdeara", "Product owner", "raghadalkdearat@gmail.com",
                "https://www.linkedin.com/in/raghad-alkdearat-6394a4356/", "https://github.com/raghadalkdearat-prog", "/img/team/raghad.jpg"),
            new TeamMember("roaa", "Roa\u2019a Basil Ajlouni", "Quality Assurance", "roaaajlouni365@gmail.com",
                "https://www.linkedin.com/in/roa%E2%80%99a-ajlouni-414970328?utm_source=share_via&utm_content=profile&utm_medium=member_ios",
                "https://github.com/roaaajlouni365-a11y", "/img/team/roaa.jpg", "50% 27%"),
            new TeamMember("osama", "Osama Syfuan Banihani", "Developer", "osamabanihani124@gmail.com",
                "https://www.linkedin.com/in/osama-banihani-77896b289/", "https://github.com/osamabanihani", "/img/team/osama.jpg"),
            new TeamMember("abdallah", "Abdallah Ahmad Mdalal", "Developer", "abdallahmdalal@gmail.com",
                "https://www.linkedin.com/in/abdallah-mdalal", "https://github.com/abd3348", "/img/team/abdallah.jpg"),
            new TeamMember("ahmad", "Ahmad Ali Smairat", "Developer", "a7madbenali@gmail.com",
                "https://www.linkedin.com/in/ahmad-smairat-101479223/", "https://github.com/ahmadbenali", "/img/team/ahmad.jpg"),
            new TeamMember("fatima", "Fatima ehab tawalbeh", "Developer", "fatimatawalbeh9@gmail.com",
                "https://www.linkedin.com/in/fatima-tawalbeh", "https://github.com/fatimaT2023", "/img/team/fatima.jpg"),
        }
    };
}
