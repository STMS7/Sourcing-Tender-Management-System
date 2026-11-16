namespace Sourcing_Tender_Management_System.Services;

/// <summary>
/// Settings for the footer location map. The single source for Irbid's coordinates.
/// A browser Maps API key is visible to every visitor: restrict it by HTTP referrer and API.
/// Keep real keys out of source control (user-secrets or environment: GoogleMaps__ApiKey).
/// </summary>
public class GoogleMapsOptions
{
    public string ApiKey { get; set; } = "";
    /// <summary>Optional cloud-based map style ID. When empty, the built-in Zaytouna style array is used.</summary>
    public string MapId { get; set; } = "";
    public double Latitude { get; set; } = 32.5556;
    public double Longitude { get; set; } = 35.85;
    public int Zoom { get; set; } = 12;
    /// <summary>Maps JavaScript API release channel. Google's 3D Maps quick start loads v=beta; change to "weekly" once 3D is GA in your channel.</summary>
    public string Version { get; set; } = "beta";
    public string LocationName { get; set; } = "Irbid, Jordan";
    public string LocationNameAr { get; set; } = "إربد، الأردن";
    public string ExternalMapUrl { get; set; } = "https://www.google.com/maps/search/?api=1&query=Irbid%2C%20Jordan";
    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
}
