namespace AlonProject.Application.DTOs;

/// <summary>
/// A computed driving trip through a set of stops (best visiting order).
/// </summary>
public class RouteTripDto
{
    /// <summary>Total driving distance in meters.</summary>
    public double DistanceMeters { get; set; }

    /// <summary>Total driving time in seconds.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Route geometry to draw on the map: ordered [latitude, longitude] pairs.
    /// </summary>
    public List<double[]> Geometry { get; set; } = new();

    /// <summary>
    /// Visit order per input stop: VisitOrder[i] is the position (0-based)
    /// at which input stop i is visited in the optimized trip.
    /// </summary>
    public List<int> VisitOrder { get; set; } = new();
}
