namespace TDigestNet;

/// <summary>
/// The T-Digest quantile estimation algorithm.
/// </summary>
public interface ITDigest
{
    /// <summary>
    /// Returns the sum of the weights of all objects added to the Digest.
    /// Since the default weight for each object is 1, this will be equal to the number
    /// of objects added to the digest unless custom weights are used.
    /// </summary>
    public double Count { get; }

    /// <summary>
    /// Returns the number of Internal Centroid objects allocated.
    /// The number of these objects is directly proportional to the amount of memory used.
    /// </summary>
    public int CentroidCount { get; }

    /// <summary>
    /// Gets the Accuracy setting as specified in the constructor.
    /// Smaller numbers result in greater accuracy at the expense of
    /// poorer performance and greater memory consumption
    /// Default is .02
    /// </summary>
    public double Accuracy { get; }

    /// <summary>
    /// The Compression Constant Setting
    /// </summary>
    public double CompressionConstant { get; }

    /// <summary>
    /// The Average
    /// </summary>
    public double Average { get; }

    /// <summary>
    /// The Min
    /// </summary>
    public double Min { get; }

    /// <summary>
    /// The Max
    /// </summary>
    public double Max { get; }

    /// <summary>
    /// The expected size in bytes after serialization
    /// </summary>
    public int ExpectedSerializedBytesLength { get; }

    /// <summary>
    /// Add a new value to the T-Digest. Note that this method is NOT thread safe.
    /// </summary>
    /// <param name="value">The value to add</param>
    /// <param name="weight">The relative weight associated with this value. Default is 1 for all values.</param>
    public void Add(double value, double weight = 1);

    /// <summary>
    /// Estimates the specified quantile
    /// </summary>
    /// <param name="quantile">The quantile to estimate. Must be between 0 and 1.</param>
    /// <returns>The value for the estimated quantile</returns>
    public double Quantile(double quantile);

    /// <summary>
    /// Gets the Distribution of the data added thus far
    /// </summary>
    /// <returns>An array of objects that contain a value (x-axis) and a count (y-axis)
    /// which can be used to plot a distribution of the data set</returns>
    public IEnumerable<DistributionPoint> GetDistribution();

    /// <summary>
    /// Multiply T-Digest on factor
    /// </summary>
    /// <param name="factor">The factor</param>
    /// <returns>The same instance of T-Digest</returns>
    public TDigest MultiplyOn(double factor);

    /// <summary>
    /// Divide T-Digest on factor
    /// </summary>
    /// <param name="factor">The factor</param>
    /// <returns>The same instance of T-Digest</returns>
    public TDigest DivideOn(double factor);

    /// <summary>
    /// Shift T-Digest on value
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The same instance of T-Digest</returns>
    public TDigest Shift(double value);

    /// <summary>
    /// Create copy of current T-Digest instance
    /// </summary>
    /// <returns>New T-Digest instance</returns>
    public TDigest Clone();

    /// <summary>
    /// Serializes this T-Digest to a byte[]
    /// </summary>
    /// <param name="compressed">If true, serialized distribution points will be compressed</param>
    /// <returns>Serialized bytes array</returns>
    public byte[] Serialize(bool compressed = true);

    /// <summary>
    /// Serializes this T-Digest to a Span
    /// </summary>
    /// <param name="target">The target Span for serialization</param>
    /// <param name="compressed">If true, serialized distribution points will be compressed</param>
    /// <returns>Number of bytes written</returns>
    public int Serialize(Span<byte> target, bool compressed = true);
}
