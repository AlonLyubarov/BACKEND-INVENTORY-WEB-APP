using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Weight- and urgency-aware route optimizer.
///
/// Cost model per route:
///   fuel      = Σ legDistance × (BaseLitersPerKm + ExtraLitersPerKmPerTon × grossTons)
///   penalty   = Σ urgency(stop) × cumulativeKmAtArrival(stop)
///   score     = fuel + λ × penalty            (lower is better)
///
/// The truck's weight CHANGES at every stop (LoadDeltaKg), so the cost is
/// order-dependent: delivering heavy cargo early makes the rest of the trip
/// cheaper. Search strategy:
///   - n ≤ 8  → exhaustive permutation search (verified optimum, ≤ 40,320 evals)
///   - n > 8  → greedy nearest-by-marginal-score + 2-opt improvement
///     (each 2-opt move re-evaluates the full route because the cost is
///      state-dependent — the classic O(1) delta does not apply).
/// </summary>
public class RouteOptimizerService : IRouteOptimizerService
{
    private const int ExactSearchLimit = 8;
    private const double BaseLitersPerKm = 0.25;
    private const double ExtraLitersPerKmPerTon = 0.04;

    private readonly ILogger<RouteOptimizerService> _logger;

    public RouteOptimizerService(ILogger<RouteOptimizerService> logger)
    {
        _logger = logger;
    }

    public OptimizeRouteResultDto Optimize(OptimizeRouteRequestDto request, double[,] distanceKmMatrix)
    {
        var stopCount = request.Stops.Count;
        var evaluated = 0;

        // Naive baseline: visit stops in the order they were sent
        var naiveOrder = Enumerable.Range(0, stopCount).ToArray();
        var naive = Evaluate(request, distanceKmMatrix, naiveOrder);

        int[] bestOrder;
        Evaluation best;
        string method;

        if (stopCount <= ExactSearchLimit)
        {
            method = "Exhaustive";
            (bestOrder, best, evaluated) = ExhaustiveSearch(request, distanceKmMatrix);
        }
        else
        {
            method = "NearestNeighbor2Opt";
            (bestOrder, best, evaluated) = GreedyPlusTwoOpt(request, distanceKmMatrix);
        }

        if (!best.IsFeasible)
        {
            throw new InvalidOperationException(
                "No feasible route exists: the truck's capacity is exceeded (or cargo goes negative) in every visiting order. Adjust the load plan or capacity.");
        }

        _logger.LogInformation(
            "Route optimized ({Method}): {Evaluated} orders evaluated, score {Score:F2} vs naive {NaiveScore:F2}",
            method, evaluated, best.TotalScore, naive.TotalScore);

        var optimizedPlan = ToPlan(request, best, bestOrder);
        var naivePlan = ToPlan(request, naive, naiveOrder);

        var fuelSaved = naive.IsFeasible ? naive.TotalFuelLiters - best.TotalFuelLiters : 0;
        return new OptimizeRouteResultDto
        {
            Method = method,
            RoutesEvaluated = evaluated,
            Optimized = optimizedPlan,
            Naive = naivePlan,
            FuelSavedLiters = Math.Round(fuelSaved, 2),
            FuelSavedPercent = naive.IsFeasible && naive.TotalFuelLiters > 0
                ? Math.Round(fuelSaved / naive.TotalFuelLiters * 100, 1)
                : 0,
            ScoreImprovedPercent = naive.IsFeasible && naive.TotalScore > 0
                ? Math.Round((naive.TotalScore - best.TotalScore) / naive.TotalScore * 100, 1)
                : 0
        };
    }

    // ── Evaluation ────────────────────────────────────────────────────────

    private sealed class Evaluation
    {
        public bool IsFeasible = true;
        public double TotalDistanceKm;
        public double TotalFuelLiters;
        public double UrgencyPenalty;
        public double TotalScore = double.PositiveInfinity;
        public List<SmartRouteLegDto> Legs = new();
    }

    private static double LitersPerKm(double grossWeightKg) =>
        BaseLitersPerKm + ExtraLitersPerKmPerTon * (grossWeightKg / 1000.0);

    /// <summary>
    /// Simulates driving the route in the given stop order (matrix indices are
    /// order[i] + 1; depot is matrix index 0), updating the truck's weight at
    /// every stop and accumulating weight-based fuel + urgency penalty.
    /// </summary>
    private static Evaluation Evaluate(OptimizeRouteRequestDto request, double[,] matrix, int[] order)
    {
        var eval = new Evaluation();
        var loadKg = request.Truck.CurrentLoadKg;

        if (loadKg > request.Truck.MaxCapacityKg)
        {
            eval.IsFeasible = false;
            return eval;
        }

        double cumulativeKm = 0, fuel = 0, penalty = 0;
        var fromMatrixIndex = 0; // depot

        foreach (var stopIndex in order)
        {
            var stop = request.Stops[stopIndex];
            var toMatrixIndex = stopIndex + 1;
            var distance = matrix[fromMatrixIndex, toMatrixIndex];
            var gross = request.Truck.TareWeightKg + loadKg;

            var legFuel = distance * LitersPerKm(gross);
            fuel += legFuel;
            cumulativeKm += distance;

            // The later (further into the route) an urgent stop is reached, the higher the penalty
            penalty += stop.UrgencyScore * cumulativeKm;

            // Load/unload at the stop — the dynamic weight update
            loadKg += stop.LoadDeltaKg;
            if (loadKg < -0.001 || loadKg > request.Truck.MaxCapacityKg + 0.001)
            {
                eval.IsFeasible = false;
                return eval;
            }

            eval.Legs.Add(new SmartRouteLegDto
            {
                FromWarehouseId = fromMatrixIndex, // matrix indices here; mapped to ids by caller
                ToWarehouseId = toMatrixIndex,
                DistanceKm = Math.Round(distance, 2),
                GrossWeightKg = Math.Round(gross, 1),
                FuelLiters = Math.Round(legFuel, 2),
                LoadAfterStopKg = Math.Round(loadKg, 1)
            });

            fromMatrixIndex = toMatrixIndex;
        }

        eval.TotalDistanceKm = Math.Round(cumulativeKm, 2);
        eval.TotalFuelLiters = fuel;
        eval.UrgencyPenalty = penalty;
        eval.TotalScore = fuel + request.UrgencyWeight * penalty;
        return eval;
    }

    // ── Exact search (n ≤ 8): try every visiting order ────────────────────

    private (int[] Order, Evaluation Best, int Evaluated) ExhaustiveSearch(
        OptimizeRouteRequestDto request, double[,] matrix)
    {
        var n = request.Stops.Count;
        var order = Enumerable.Range(0, n).ToArray();
        var bestOrder = (int[])order.Clone();
        var best = new Evaluation { IsFeasible = false }; // sentinel: nothing found yet
        var evaluated = 0;

        Permute(order, 0);
        return (bestOrder, best, evaluated);

        void Permute(int[] current, int index)
        {
            if (index == current.Length)
            {
                evaluated++;
                var eval = Evaluate(request, matrix, current);
                if (eval.IsFeasible && eval.TotalScore < best.TotalScore)
                {
                    best = eval;
                    bestOrder = (int[])current.Clone();
                }
                return;
            }

            for (var i = index; i < current.Length; i++)
            {
                (current[index], current[i]) = (current[i], current[index]);
                Permute(current, index + 1);
                (current[index], current[i]) = (current[i], current[index]);
            }
        }
    }

    // ── Heuristic search (n > 8): greedy construction + 2-opt ─────────────

    private (int[] Order, Evaluation Best, int Evaluated) GreedyPlusTwoOpt(
        OptimizeRouteRequestDto request, double[,] matrix)
    {
        var n = request.Stops.Count;
        var evaluated = 0;

        // Greedy: repeatedly append the stop whose addition yields the best partial score
        var remaining = Enumerable.Range(0, n).ToList();
        var route = new List<int>();
        while (remaining.Count > 0)
        {
            var bestNext = remaining[0];
            var bestScore = double.PositiveInfinity;
            foreach (var candidate in remaining)
            {
                route.Add(candidate);
                var eval = Evaluate(request, matrix, route.ToArray());
                evaluated++;
                route.RemoveAt(route.Count - 1);

                // Infeasible partials still get compared by score (+∞ loses)
                if (eval.TotalScore < bestScore)
                {
                    bestScore = eval.TotalScore;
                    bestNext = candidate;
                }
            }
            route.Add(bestNext);
            remaining.Remove(bestNext);
        }

        var order = route.ToArray();
        var best = Evaluate(request, matrix, order);
        evaluated++;

        // 2-opt: reverse segments while the FULL route score improves.
        // Full re-evaluation per move — the cost is order/state-dependent.
        var improved = true;
        while (improved)
        {
            improved = false;
            for (var i = 0; i < order.Length - 1; i++)
            {
                for (var j = i + 1; j < order.Length; j++)
                {
                    var candidate = (int[])order.Clone();
                    Array.Reverse(candidate, i, j - i + 1);
                    var eval = Evaluate(request, matrix, candidate);
                    evaluated++;
                    if (eval.IsFeasible && eval.TotalScore < best.TotalScore - 0.0001)
                    {
                        order = candidate;
                        best = eval;
                        improved = true;
                    }
                }
            }
        }

        return (order, best, evaluated);
    }

    // ── Mapping ───────────────────────────────────────────────────────────

    private static SmartRoutePlanDto ToPlan(OptimizeRouteRequestDto request, Evaluation eval, int[] order)
    {
        // Convert matrix indices (0 = depot, i+1 = stop i) back to warehouse ids
        int ToWarehouseId(int matrixIndex) =>
            matrixIndex == 0 ? request.DepotWarehouseId : request.Stops[matrixIndex - 1].WarehouseId;

        return new SmartRoutePlanDto
        {
            OrderedWarehouseIds = order.Select(i => request.Stops[i].WarehouseId).ToList(),
            Legs = eval.Legs.Select(l => new SmartRouteLegDto
            {
                FromWarehouseId = ToWarehouseId(l.FromWarehouseId),
                ToWarehouseId = ToWarehouseId(l.ToWarehouseId),
                DistanceKm = l.DistanceKm,
                GrossWeightKg = l.GrossWeightKg,
                FuelLiters = l.FuelLiters,
                LoadAfterStopKg = l.LoadAfterStopKg
            }).ToList(),
            TotalDistanceKm = eval.TotalDistanceKm,
            TotalFuelLiters = Math.Round(eval.TotalFuelLiters, 2),
            UrgencyPenalty = Math.Round(eval.UrgencyPenalty, 1),
            TotalScore = Math.Round(eval.TotalScore, 2)
        };
    }
}
