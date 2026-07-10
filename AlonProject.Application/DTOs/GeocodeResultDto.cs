namespace AlonProject.Application.DTOs;

/// <summary>
/// A single address→coordinates match returned by the geocoding proxy.
/// </summary>
public class GeocodeResultDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string DisplayName { get; set; } = null!;
}
