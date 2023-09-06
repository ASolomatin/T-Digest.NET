namespace TDigestNet;

/// <summary>
/// An objects that contain a value (x-axis) and a count (y-axis)
/// which can be used to plot a distribution of the data set
/// </summary>
/// <param name="Value">The value (x-axis)</param>
/// <param name="Count">The count (y-axis)</param>
public record struct DistributionPoint(double Value, double Count);