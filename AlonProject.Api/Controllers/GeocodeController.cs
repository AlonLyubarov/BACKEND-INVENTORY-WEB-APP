using System.Globalization;
using System.Text.Json;
using AlonProject.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AlonProject.Api.Controllers;

/// <summary>
/// Server-side geocoding proxy (address name → real coordinates).
/// Calls OpenStreetMap's Nominatim API from the backend so that:
/// - the proper User-Agent required by Nominatim's usage policy is sent,
/// - repeated queries are cached centrally (respecting their 1 req/sec limit),
/// - a paid provider with an API key can replace it later without touching the frontend.
/// Anonymous: the registration screen needs geocoding before a user exists.
/// Route: /api/geocode
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class GeocodeController : ControllerBase
{
    private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeocodeController> _logger;

    public GeocodeController(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<GeocodeController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// GET api/geocode?query=address
    /// Returns up to 5 coordinate matches for the given address text.
    /// </summary>
    /// <response code="200">Matches returned (possibly empty)</response>
    /// <response code="400">Query too short</response>
    /// <response code="502">Geocoding provider unavailable</response>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GeocodeResultDto>>> Search([FromQuery] string query)
    {
        var term = (query ?? string.Empty).Trim();
        if (term.Length < 3)
        {
            return BadRequest(new { error = "Query must be at least 3 characters." });
        }

        var cacheKey = $"geocode:{term.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out List<GeocodeResultDto>? cached) && cached != null)
        {
            _logger.LogDebug("Geocode cache hit for '{Query}'", term);
            return Ok(cached);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("nominatim");
            var url = $"{NominatimUrl}?q={Uri.EscapeDataString(term)}&format=jsonv2&limit=5&accept-language=en";
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);

            var results = new List<GeocodeResultDto>();
            foreach (var element in json.RootElement.EnumerateArray())
            {
                if (double.TryParse(element.GetProperty("lat").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(element.GetProperty("lon").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                {
                    results.Add(new GeocodeResultDto
                    {
                        Latitude = lat,
                        Longitude = lon,
                        DisplayName = element.GetProperty("display_name").GetString() ?? string.Empty
                    });
                }
            }

            _cache.Set(cacheKey, results, CacheDuration);
            _logger.LogInformation("Geocoded '{Query}' via Nominatim: {Count} results", term, results.Count);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geocoding failed for query '{Query}'", term);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Address lookup is temporarily unavailable. Try again shortly." });
        }
    }
}
