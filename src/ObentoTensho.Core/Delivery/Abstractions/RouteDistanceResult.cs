namespace ObentoTensho.Core.Delivery.Abstractions;

public sealed record RouteDistanceResult(RouteOutcome Outcome, int Meters)
{
    public static RouteDistanceResult NotFound { get; } = new(RouteOutcome.NotFound, 0);

    public static RouteDistanceResult Unavailable { get; } = new(RouteOutcome.Unavailable, 0);

    public static RouteDistanceResult Of(int meters) => new(RouteOutcome.Found, meters);
}
