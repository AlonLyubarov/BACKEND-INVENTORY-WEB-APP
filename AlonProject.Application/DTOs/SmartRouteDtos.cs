using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>Truck parameters for weight-aware route optimization.</summary>
public class TruckDto
{
    [Range(500, 40000, ErrorMessage = "Tare weight must be between 500 and 40,000 kg")]
    public double TareWeightKg { get; set; } = 3500;

    [Range(0, 40000, ErrorMessage = "Current load must be between 0 and 40,000 kg")]
    public double CurrentLoadKg { get; set; }

    [Range(1, 40000, ErrorMessage = "Capacity must be between 1 and 40,000 kg")]
    public double MaxCapacityKg { get; set; } = 1200;
}

/// <summary>One stop in the optimization request.</summary>
public class SmartRouteStopDto
{
    public int WarehouseId { get; set; }

    /// <summary>1 (relaxed) … 10 (critically low stock).</summary>
    [Range(1, 10, ErrorMessage = "Urgency must be between 1 and 10")]
    public int UrgencyScore { get; set; } = 5;

    /// <summary>Net cargo change at this stop: negative = deliver, positive = collect.</summary>
    [Range(-40000, 40000)]
    public double LoadDeltaKg { get; set; }
}

/// <summary>POST /api/route/optimize request.</summary>
public class OptimizeRouteRequestDto
{
    public int DepotWarehouseId { get; set; }

    [Required]
    public TruckDto Truck { get; set; } = new();

    [Required]
    [MinLength(2, ErrorMessage = "At least 2 stops are required")]
    [MaxLength(15, ErrorMessage = "At most 15 stops are supported")]
    public List<SmartRouteStopDto> Stops { get; set; } = new();

    /// <summary>
    /// λ — how many liters one urgency-point × km of lateness is worth.
    /// 0 = pure fuel optimization; higher pushes urgent stops earlier.
    /// </summary>
    [Range(0, 1)]
    public double UrgencyWeight { get; set; } = 0.02;
}

/// <summary>One driving leg with the truck's weight while driving it.</summary>
public class SmartRouteLegDto
{
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public double DistanceKm { get; set; }
    /// <summary>Gross truck weight (tare + cargo) DURING this leg.</summary>
    public double GrossWeightKg { get; set; }
    public double FuelLiters { get; set; }
    /// <summary>Cargo on board after the load/unload at the destination stop.</summary>
    public double LoadAfterStopKg { get; set; }
}

/// <summary>A fully evaluated route (visit order + cost breakdown).</summary>
public class SmartRoutePlanDto
{
    public List<int> OrderedWarehouseIds { get; set; } = new();
    public List<SmartRouteLegDto> Legs { get; set; } = new();
    public double TotalDistanceKm { get; set; }
    public double TotalFuelLiters { get; set; }
    public double UrgencyPenalty { get; set; }
    /// <summary>fuel + λ × penalty — the value the optimizer minimizes.</summary>
    public double TotalScore { get; set; }
    /// <summary>Route geometry ([lat, lng] pairs) for map drawing.</summary>
    public List<double[]> Geometry { get; set; } = new();
}

/// <summary>POST /api/route/optimize response: optimized plan vs naive baseline.</summary>
public class OptimizeRouteResultDto
{
    /// <summary>"Exhaustive" (verified optimum) or "NearestNeighbor2Opt" (heuristic).</summary>
    public string Method { get; set; } = null!;

    /// <summary>How many complete visiting orders were evaluated.</summary>
    public int RoutesEvaluated { get; set; }

    public SmartRoutePlanDto Optimized { get; set; } = null!;

    /// <summary>Baseline: stops visited in the order they were sent.</summary>
    public SmartRoutePlanDto Naive { get; set; } = null!;

    public double FuelSavedLiters { get; set; }
    public double FuelSavedPercent { get; set; }
    public double ScoreImprovedPercent { get; set; }
}
