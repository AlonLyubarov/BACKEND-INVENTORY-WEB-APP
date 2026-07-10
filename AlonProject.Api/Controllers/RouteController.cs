using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private const string OsrmTableUrl = "https://router.project-osrm.org/table/v1/driving/";
    private const string OsrmRouteUrl = "https://router.project-osrm.org/route/v1/driving/";
    private const int MaxStops = 12;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IRouteOptimizerService _optimizer;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseAccessService _accessService;
    private readonly ILogger<RouteController> _logger;

    public RouteController(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IRouteOptimizerService optimizer,
        IWarehouseRepository warehouseRepository,
        IWarehouseAccessService accessService,
        ILogger<RouteController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _optimizer = optimizer;
        _warehouseRepository = warehouseRepository;
        _accessService = accessService;
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
    [EnableRateLimiting("geo")]
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

        // Key on the PARSED, normalized coordinates — not the raw query string —
        // so equivalent inputs share one entry and junk input can't grow the cache.
        var normalizedStops = string.Join(";",
            points.Select(p =>
                $"{p.Lat.ToString("F5", CultureInfo.InvariantCulture)},{p.Lng.ToString("F5", CultureInfo.InvariantCulture)}"));
        var cacheKey = $"route:{normalizedStops}";
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

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = CacheDuration
            });
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

    // ═════════════════════════════════════════════════════════════════════
    // Smart route optimization (weight-based fuel + inventory urgency)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// POST api/route/optimize
    /// Weight- and urgency-aware visiting order: the truck's weight changes at
    /// every stop, fuel burn depends on the current weight, and urgent stops
    /// are pushed earlier. Distances come from OSRM's table service (real
    /// roads); the ordering algorithm (exhaustive / NN+2-opt) is our own.
    /// </summary>
    /// <response code="200">Optimized plan + naive baseline for comparison</response>
    /// <response code="400">Invalid stops, missing coordinates, or no feasible order</response>
    /// <response code="403">A warehouse is outside the caller's tree</response>
    /// <response code="502">Routing provider unavailable</response>
    [HttpPost("optimize")]
    [EnableRateLimiting("geo")]
    public async Task<ActionResult<OptimizeRouteResultDto>> Optimize([FromBody] OptimizeRouteRequestDto request)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        int? userWarehouseId = null;
        var warehouseClaim = User.FindFirst("WarehouseId");
        if (warehouseClaim != null && int.TryParse(warehouseClaim.Value, out var parsedWarehouseId))
        {
            userWarehouseId = parsedWarehouseId;
        }

        var allIds = new List<int> { request.DepotWarehouseId };
        allIds.AddRange(request.Stops.Select(s => s.WarehouseId));
        if (allIds.Distinct().Count() != allIds.Count)
        {
            return BadRequest(new { error = "Each warehouse may appear only once (depot included)." });
        }

        // SECURITY + coordinates: every node must be accessible and pinned on the map
        var coordinatesById = new Dictionary<int, (double Lat, double Lng)>();
        foreach (var id in allIds)
        {
            if (!await _accessService.CanAccessWarehouseAsync(id, userId, role, userWarehouseId))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "One of the warehouses is outside your accessible tree." });
            }

            var warehouse = await _warehouseRepository.GetByIdAsync(id);
            if (warehouse == null)
            {
                return NotFound(new { error = $"Warehouse {id} not found." });
            }
            if (warehouse.Latitude == null || warehouse.Longitude == null)
            {
                return BadRequest(new
                {
                    error = $"Warehouse '{warehouse.Name}' has no map location. Edit it and pin it on the map first."
                });
            }
            coordinatesById[id] = (warehouse.Latitude.Value, warehouse.Longitude.Value);
        }

        try
        {
            // Matrix order: index 0 = depot, then stops in request order
            var orderedCoordinates = allIds.Select(id => coordinatesById[id]).ToList();
            var matrix = await FetchDistanceMatrixKmAsync(orderedCoordinates);

            var result = _optimizer.Optimize(request, matrix);

            // Road geometry for both plans (drawn on the map for comparison)
            result.Optimized.Geometry = await FetchRouteGeometryAsync(
                BuildPathCoordinates(request.DepotWarehouseId, result.Optimized.OrderedWarehouseIds, coordinatesById));
            result.Naive.Geometry = await FetchRouteGeometryAsync(
                BuildPathCoordinates(request.DepotWarehouseId, result.Naive.OrderedWarehouseIds, coordinatesById));

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // No feasible visiting order (capacity violated everywhere)
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Route optimization failed");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Route optimization is temporarily unavailable. Try again shortly." });
        }
    }

    private static List<(double Lat, double Lng)> BuildPathCoordinates(
        int depotId, IEnumerable<int> orderedStopIds, Dictionary<int, (double Lat, double Lng)> coordinatesById)
    {
        var path = new List<(double, double)> { coordinatesById[depotId] };
        path.AddRange(orderedStopIds.Select(id => coordinatesById[id]));
        return path;
    }

    /// <summary>
    /// Full road-distance matrix (km) between all points via OSRM's table
    /// service. Cached 30 minutes on the normalized coordinate list.
    /// </summary>
    private async Task<double[,]> FetchDistanceMatrixKmAsync(IReadOnlyList<(double Lat, double Lng)> points)
    {
        var normalized = string.Join(";",
            points.Select(p =>
                $"{p.Lat.ToString("F5", CultureInfo.InvariantCulture)},{p.Lng.ToString("F5", CultureInfo.InvariantCulture)}"));
        var cacheKey = $"matrix:{normalized}";
        if (_cache.TryGetValue(cacheKey, out double[,]? cachedMatrix) && cachedMatrix != null)
        {
            return cachedMatrix;
        }

        var coordinates = string.Join(";",
            points.Select(p =>
                $"{p.Lng.ToString(CultureInfo.InvariantCulture)},{p.Lat.ToString(CultureInfo.InvariantCulture)}"));
        var url = $"{OsrmTableUrl}{coordinates}?annotations=distance";

        var client = _httpClientFactory.CreateClient("osrm");
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;
        if (root.GetProperty("code").GetString() != "Ok")
        {
            throw new HttpRequestException("OSRM table service returned a non-Ok response.");
        }

        var distances = root.GetProperty("distances");
        var n = points.Count;
        var matrix = new double[n, n];
        var row = 0;
        foreach (var rowElement in distances.EnumerateArray())
        {
            var col = 0;
            foreach (var cell in rowElement.EnumerateArray())
            {
                if (cell.ValueKind == JsonValueKind.Null)
                {
                    throw new HttpRequestException("No drivable road between two of the warehouses.");
                }
                matrix[row, col] = cell.GetDouble() / 1000.0; // meters → km
                col++;
            }
            row++;
        }

        _cache.Set(cacheKey, matrix, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = CacheDuration
        });
        return matrix;
    }

    /// <summary>Road geometry ([lat, lng] pairs) for a fixed point sequence via OSRM route.</summary>
    private async Task<List<double[]>> FetchRouteGeometryAsync(IReadOnlyList<(double Lat, double Lng)> path)
    {
        var coordinates = string.Join(";",
            path.Select(p =>
                $"{p.Lng.ToString(CultureInfo.InvariantCulture)},{p.Lat.ToString(CultureInfo.InvariantCulture)}"));
        var url = $"{OsrmRouteUrl}{coordinates}?overview=full&geometries=geojson";

        var client = _httpClientFactory.CreateClient("osrm");
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;
        if (root.GetProperty("code").GetString() != "Ok")
        {
            throw new HttpRequestException("OSRM route service returned a non-Ok response.");
        }

        var geometry = new List<double[]>();
        foreach (var coordinate in root.GetProperty("routes")[0].GetProperty("geometry").GetProperty("coordinates").EnumerateArray())
        {
            geometry.Add(new[] { coordinate[1].GetDouble(), coordinate[0].GetDouble() });
        }
        return geometry;
    }
}
