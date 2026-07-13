using AlonProject.Application.DTOs;
using AlonProject.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlonProject.Tests.Unit;

/// <summary>
/// Unit tests for the weight- and urgency-aware route optimizer.
/// The optimizer is pure logic (request + distance matrix in, plan out), so every
/// expected value below is hand-computed from the documented cost model:
///   fuel    = Σ legDistance × (0.25 + 0.04 × grossTons)
///   penalty = Σ urgency(stop) × cumulativeKmAtArrival
///   score   = fuel + λ × penalty
/// </summary>
public class RouteOptimizerServiceTests
{
    private static RouteOptimizerService NewService() =>
        new(NullLogger<RouteOptimizerService>.Instance);

    [Fact]
    public void Optimize_DeliversHeavyLoadFirst_ToSaveFuelOnTheLongLeg()
    {
        // depot(0)—A(1) short (1 km), depot—B(2) short (1 km), A—B long (10 km).
        // Truck starts full; A is a heavy drop (900 kg), B a light drop (100 kg).
        // Shedding the heavy cargo BEFORE the long 10 km leg is cheaper, so the
        // optimum is [A, B] even though the naive order sends [B, A].
        var matrix = new double[,]
        {
            { 0, 1, 1 },
            { 1, 0, 10 },
            { 1, 10, 0 },
        };
        var request = new OptimizeRouteRequestDto
        {
            DepotWarehouseId = 100,
            UrgencyWeight = 0, // isolate the weight/fuel effect
            Truck = new TruckDto { TareWeightKg = 3500, CurrentLoadKg = 1000, MaxCapacityKg = 1000 },
            Stops = new()
            {
                new SmartRouteStopDto { WarehouseId = 2, UrgencyScore = 1, LoadDeltaKg = -100 }, // B first (naive)
                new SmartRouteStopDto { WarehouseId = 1, UrgencyScore = 1, LoadDeltaKg = -900 }, // A second
            }
        };

        var result = NewService().Optimize(request, matrix);

        // Optimizer reorders to deliver the heavy stop (A = id 1) first
        Assert.Equal(new[] { 1, 2 }, result.Optimized.OrderedWarehouseIds);
        // And it is strictly cheaper on fuel than the naive [B, A] order
        Assert.True(result.Optimized.TotalFuelLiters < result.Naive.TotalFuelLiters);
        Assert.True(result.FuelSavedLiters > 0);
        // Hand-computed: [A,B] = 0.43 + 3.94 = 4.37 L
        Assert.Equal(4.37, result.Optimized.TotalFuelLiters, precision: 2);
    }

    [Fact]
    public void Optimize_VisitsMoreUrgentStopFirst_WhenDistancesAreEqual()
    {
        // Equilateral: every leg is 5 km and there is no load change, so fuel is
        // identical for both orders. Only the urgency penalty breaks the tie, and
        // reaching the high-urgency stop earlier (smaller cumulative km) wins.
        var matrix = new double[,]
        {
            { 0, 5, 5 },
            { 5, 0, 5 },
            { 5, 5, 0 },
        };
        var request = new OptimizeRouteRequestDto
        {
            DepotWarehouseId = 100,
            UrgencyWeight = 0.5,
            Truck = new TruckDto { TareWeightKg = 3500, CurrentLoadKg = 0, MaxCapacityKg = 1000 },
            Stops = new()
            {
                new SmartRouteStopDto { WarehouseId = 2, UrgencyScore = 1, LoadDeltaKg = 0 },  // low urgency, sent first
                new SmartRouteStopDto { WarehouseId = 1, UrgencyScore = 10, LoadDeltaKg = 0 }, // high urgency
            }
        };

        var result = NewService().Optimize(request, matrix);

        // The urgent stop (id 1) is pulled to the front
        Assert.Equal(1, result.Optimized.OrderedWarehouseIds[0]);
        Assert.True(result.Optimized.TotalScore < result.Naive.TotalScore);
    }

    [Fact]
    public void Optimize_UsesExhaustiveSearch_ForEightOrFewerStops()
    {
        var result = NewService().Optimize(BuildRequest(4), UniformMatrix(5, 3));
        Assert.Equal("Exhaustive", result.Method);
    }

    [Fact]
    public void Optimize_UsesHeuristic_ForMoreThanEightStops()
    {
        var result = NewService().Optimize(BuildRequest(9), UniformMatrix(10, 3));
        Assert.Equal("NearestNeighbor2Opt", result.Method);
    }

    [Fact]
    public void Optimize_NeverWorseThanNaive()
    {
        // Invariant: whatever the search returns, its score must be ≤ the naive baseline.
        var result = NewService().Optimize(BuildRequest(6), StaircaseMatrix(7));
        Assert.True(result.Optimized.TotalScore <= result.Naive.TotalScore + 1e-6);
        Assert.True(result.ScoreImprovedPercent >= 0);
    }

    [Fact]
    public void Optimize_Throws_WhenTruckStartsOverCapacity()
    {
        var request = new OptimizeRouteRequestDto
        {
            DepotWarehouseId = 100,
            Truck = new TruckDto { TareWeightKg = 3500, CurrentLoadKg = 5000, MaxCapacityKg = 1000 },
            Stops = new()
            {
                new SmartRouteStopDto { WarehouseId = 1, LoadDeltaKg = -100 },
                new SmartRouteStopDto { WarehouseId = 2, LoadDeltaKg = -100 },
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => NewService().Optimize(request, UniformMatrix(3, 5)));
        Assert.Contains("capacity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static OptimizeRouteRequestDto BuildRequest(int stopCount)
    {
        var request = new OptimizeRouteRequestDto
        {
            DepotWarehouseId = 100,
            UrgencyWeight = 0.02,
            Truck = new TruckDto { TareWeightKg = 3500, CurrentLoadKg = 300, MaxCapacityKg = 5000 }
        };
        for (var i = 0; i < stopCount; i++)
        {
            request.Stops.Add(new SmartRouteStopDto
            {
                WarehouseId = i + 1,
                UrgencyScore = (i % 10) + 1,
                LoadDeltaKg = i % 2 == 0 ? -50 : 20
            });
        }
        return request;
    }

    /// <summary>Symmetric matrix of size n with a constant off-diagonal distance.</summary>
    private static double[,] UniformMatrix(int n, double distance)
    {
        var m = new double[n, n];
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                m[i, j] = i == j ? 0 : distance;
        return m;
    }

    /// <summary>Asymmetric-by-index distances so different orders yield different costs.</summary>
    private static double[,] StaircaseMatrix(int n)
    {
        var m = new double[n, n];
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                m[i, j] = i == j ? 0 : Math.Abs(i - j) * 2 + 1;
        return m;
    }
}
