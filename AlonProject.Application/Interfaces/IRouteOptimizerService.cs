using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

/// <summary>
/// Weight- and urgency-aware route optimization over a road-distance matrix.
/// Pure algorithm — no I/O; the caller supplies the distance matrix.
/// </summary>
public interface IRouteOptimizerService
{
    /// <summary>
    /// Finds the visiting order minimizing: weight-based fuel + λ × urgency penalty.
    /// Matrix layout: index 0 = depot, index i (1-based) = Stops[i-1].
    /// Distances in km. Throws InvalidOperationException when no feasible order exists.
    /// </summary>
    OptimizeRouteResultDto Optimize(OptimizeRouteRequestDto request, double[,] distanceKmMatrix);
}
