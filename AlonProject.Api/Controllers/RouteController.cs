using System.Globalization;
using System.Text;
using System.Text.Json;
using AlonProject.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AlonProject.Api.Controllers;

/// <summary>
/// Driving-route proxy backed by OSRM (OpenStreetMap's routing engine).
/// Given a set of warehouse coordinates it returns the BEST visiting order
/// (OSRM's trip service solves the traveling-salesman ordering with real
/// road driving times), the route geometry to draw on the map, and the total
/// driving distance / duration by car.
/// Route: /api/route
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RouteController : ControllerBase
{
    private const string OsrmTripUrl = "https://router.project-osrm.org/trip/v1/driving/";
    private const int MaxStops = 12;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RouteController> _logger;

    public RouteController(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<RouteController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// GET api/route/trip?stops=lat,lng;lat,lng;...
    /// Computes the optimal driving trip through all stops, starting at the
    /// first one. Returns geometry, visit order, total distance and duration.
    /// </summary>
    /// <response code="200">Trip computed</response>
    /// <response code="400">Fewer than 2 stops / malformed coordinates / too many stops</response>
    /// <response code="502">Routing provider unavailable</response>
    [HttpGet("trip")]
    public async Task<ActionResult<RouteTripDto>> Trip([FromQuery] string stops)
    {
        var points = ParseStops(stops);
        if (points == null)
        {
            return BadRequest(new { error = "Stops must be 'lat,lng;lat,lng;…' with valid coordinates." });
        }
        if (points.Count < 2)
        {
            return BadRequest(new { error = "At least 2 stops are required to compute a route." });
        }
        if (points.Count > MaxStops)
        {
            return BadRequest(new { error = $"At most {MaxStops} stops are supported per trip." });
        }

        var cacheKey = $"route:{stops}";
        if (_cache.TryGetValue(cacheKey, out RouteTripDto? cached) && cached != null)
        {
            return Ok(cached);
        }

        try
        {
            // OSRM expects lng,lat pairs. Start the trip at the first stop.
            var coordinates = new StringBuilder();
            foreach (var (lat, lng) in points)
            {
                if (coordinates.Length > 0)
                {
                    coordinates.Append(';');
                }
                coordinates.Append(lng.ToString(CultureInfo.InvariantCulture));
                coordinates.Append(',');
                coordinates.Append(lat.ToString(CultureInfo.InvariantCulture));
            }

            var url = $"{OsrmTripUrl}{coordinates}?roundtrip=false&source=first&destination=any&overview=full&geometries=geojson";
            var client = _httpClientFactory.CreateClient("osrm");
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var root = json.RootElement;

            if (root.GetProperty("code").GetString() != "Ok")
            {
                _logger.LogWarning("OSRM returned non-Ok code for stops: {Stops}", stops);
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { error = "No drivable route found between these warehouses." });
            }

            var trip = root.GetProperty("trips")[0];
            var result = new RouteTripDto
            {
                DistanceMeters = trip.GetProperty("distance").GetDouble(),
                DurationSeconds = trip.GetProperty("duration").GetDouble()
            };

            foreach (var coordinate in trip.GetProperty("geometry").GetProperty("coordinates").EnumerateArray())
            {
                // GeoJSON is [lng, lat] — flip to [lat, lng] for the map
                result.Geometry.Add(new[] { coordinate[1].GetDouble(), coordinate[0].GetDouble() });
            }

            foreach (var waypoint in root.GetProperty("waypoints").EnumerateArray())
            {
                result.VisitOrder.Add(waypoint.GetProperty("waypoint_index").GetInt32());
            }

            _cache.Set(cacheKey, result, CacheDuration);
            _logger.LogInformation(
                "Trip computed for {StopCount} stops: {DistanceKm:F1} km, {DurationMin:F0} min",
                points.Count, result.DistanceMeters / 1000, result.DurationSeconds / 60);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Routing failed for stops: {Stops}", stops);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Route calculation is temporarily unavailable. Try again shortly." });
        }
    }

    private static List<(double Lat, double Lng)>? ParseStops(string? stops)
    {
        if (string.IsNullOrWhiteSpace(stops))
        {
            return null;
        }

        var points = new List<(double, double)>();
        foreach (var pair in stops.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(',');
            if (parts.Length != 2 ||
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) ||
                lat is < -90 or > 90 || lng is < -180 or > 180)
            {
                return null;
            }
            points.Add((lat, lng));
        }
        return points;
    }
}
