using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TDigestNet.Internal;

#if !NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace TDigestNet;

/// <summary>
/// Implementation of the T-Digest quantile estimation algorithm.
/// </summary>
public class TDigest
{
    private const int SERIALIZATION_HEADER_SIZE = 8 * 5;
    private const int SERIALIZATION_ITEM_SIZE = 8 * 2;


    private CentroidTree _centroids;
    private double _average;

    /// <summary>
    /// Returns the sum of the weights of all objects added to the Digest.
    /// Since the default weight for each object is 1, this will be equal to the number
    /// of objects added to the digest unless custom weights are used.
    /// </summary>
    public double Count => _centroids.Root?.subTreeWeight ?? 0;

    /// <summary>
    /// Returns the number of Internal Centroid objects allocated.
    /// The number of these objects is directly proportional to the amount of memory used.
    /// </summary>
    public int CentroidCount => _centroids.Count;

    /// <summary>
    /// Gets the Accuracy setting as specified in the constructor.
    /// Smaller numbers result in greater accuracy at the expense of
    /// poorer performance and greater memory consumption
    /// Default is .02
    /// </summary>
    public double Accuracy { get; private set; }

    /// <summary>
    /// The Compression Constant Setting
    /// </summary>
    public double CompressionConstant { get; private set; }

    /// <summary>
    /// The Average
    /// </summary>
    public double Average => _average;

    /// <summary>
    /// The Min
    /// </summary>
    public double Min { get; private set; }

    /// <summary>
    /// The Max
    /// </summary>
    public double Max { get; private set; }

    /// <summary>
    /// The expected size in bytes after serialization
    /// </summary>
    public int ExpectedSerializedBytesLength => SERIALIZATION_HEADER_SIZE + SERIALIZATION_ITEM_SIZE * _centroids.Count;

    internal CentroidTree InternalTree => _centroids;

    /// <summary>
    /// Construct a T-Digest,
    /// </summary>
    /// <param name="accuracy">Controls the trade-off between accuracy and memory consumption/performance.
    /// Default value is .05, higher values result in worse accuracy, but better performance and decreased memory usage, while
    /// lower values result in better accuracy and increased performance and memory usage</param>
    /// <param name="compression">K value</param>
    public TDigest(double accuracy = 0.02, double compression = 25)
        : this(new CentroidTree())
    {
        if (accuracy <= 0) throw new ArgumentOutOfRangeException(nameof(accuracy), "Accuracy must be greater than 0");
        if (compression < 15) throw new ArgumentOutOfRangeException(nameof(compression), "Compression constant must be 15 or greater");

        Accuracy = accuracy;
        CompressionConstant = compression;
    }

    private TDigest(TDigest digest)
    {
        _centroids = digest._centroids.Clone();
        _average = digest._average;
        Accuracy = digest.Accuracy;
        CompressionConstant = digest.CompressionConstant;
        Min = digest.Min;
        Max = digest.Max;
    }

    private TDigest(CentroidTree centroids) => _centroids = centroids;

    /// <summary>
    /// Add a new value to the T-Digest. Note that this method is NOT thread safe.
    /// </summary>
    /// <param name="value">The value to add</param>
    /// <param name="weight">The relative weight associated with this value. Default is 1 for all values.</param>
    public void Add(double value, double weight = 1)
    {
        if (weight <= 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be greater than 0");

        if (_centroids.Root is null)
        {
            _average = value;
            Min = value;
            Max = value;

            _centroids.Add(value, weight);
            return;
        }
        else
        {
            _average += (value - _average) * weight / (_centroids.Root.subTreeWeight + weight);
            Max = value > Max ? value : Max;
            Min = value < Min ? value : Min;
        }

        if (_centroids.GetOrClosest(value, out var candidateA, out var candidateB))
        {
            candidateA!.Update(weight, value, true);
            return;
        }

        var thresholdA = 0d;
        var thresholdB = 0d;

        if (candidateA is not null)
        {
            if (candidateB is not null)
            {
                var aDiff = Math.Abs(candidateA.mean - value);
                var bDiff = Math.Abs(candidateB.mean - value);

                if (aDiff < bDiff)
                    candidateB = null;
                else if (aDiff > bDiff)
                {
                    candidateA = candidateB;
                    candidateB = null;
                }
                else
                    thresholdB = GetThreshold(ComputeCentroidQuantile(candidateB));
            }

            thresholdA = GetThreshold(ComputeCentroidQuantile(candidateA));
        }
        else if (candidateB is not null)
            thresholdB = GetThreshold(ComputeCentroidQuantile(candidateB));

        FilterCandidate(ref candidateA, thresholdA, weight);
        FilterCandidate(ref candidateB, thresholdB, weight);

        if (candidateA is null)
        {
            candidateA = candidateB;
            candidateB = null;
            thresholdA = thresholdB;
        }

        if (candidateA is not null)
        {
            UpdateCentroid(candidateA, thresholdA, value, ref weight, true);

            if (candidateB is not null && weight > 0)
                UpdateCentroid(candidateB, thresholdB, value, ref weight, true);


            if (weight == 0)
                return;
        }

        _centroids.Add(value, weight);

        if (_centroids.Count > (CompressionConstant / Accuracy))
            _centroids = CompressCentroidTree();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double ComputeCentroidQuantile(Centroid centroid) => (centroid.SumOfLeft() - centroid.weight / 2) / _centroids.Root.subTreeWeight;
    }

    /// <summary>
    /// Estimates the specified quantile
    /// </summary>
    /// <param name="quantile">The quantile to estimate. Must be between 0 and 1.</param>
    /// <returns>The value for the estimated quantile</returns>
    public double Quantile(double quantile)
    {
        if (quantile < 0 || quantile > 1)
            throw new ArgumentOutOfRangeException(nameof(quantile), "Quantile must be between 0 and 1");

        if (_centroids.Count == 0)
            throw new InvalidOperationException("Cannot call Quantile() method until first Adding values to the digest");

        if (_centroids.Count == 1)
            return _centroids.Root!.mean;

        var weight = quantile * _centroids.Root!.subTreeWeight;

        var nearest = _centroids.FindByWeight(weight, out var pointA)!;
        var meanA = nearest.mean;

        if (weight == pointA)
            return meanA;

        double meanB;
        double pointB;
        var pointWeight = nearest.weight;

        if (weight < pointA)
        {
            nearest = _centroids.GoLeft(nearest);
            if (nearest is null)
            {
                meanB = Min;
                pointB = 0;
            }
            else
            {
                meanB = nearest.mean;
                pointB = pointA - (pointWeight + nearest.weight) / 2;
            }
        }
        else
        {
            nearest = _centroids.GoRight(nearest);
            if (nearest is null)
            {
                meanB = Max;
                pointB = _centroids.Root!.subTreeWeight;
            }
            else
            {
                meanB = nearest.mean;
                pointB = pointA + (pointWeight + nearest.weight) / 2;
            }
        }

        var distance = Math.Abs(pointA - pointB);

        var result = (meanA * (distance - Math.Abs(weight - pointA)) + meanB * (distance - Math.Abs(weight - pointB))) / distance;

        return result;
    }

    /// <summary>
    /// Gets the Distribution of the data added thus far
    /// </summary>
    /// <returns>An array of objects that contain a value (x-axis) and a count (y-axis)
    /// which can be used to plot a distribution of the data set</returns>
    public IEnumerable<DistributionPoint> GetDistribution() => _centroids
        .Select(c => new DistributionPoint(c.mean, c.weight));

    /// <summary>
    /// Multiply T-Digest on factor
    /// </summary>
    /// <param name="factor">The factor</param>
    /// <returns>The same instance of T-Digest</returns>
    public TDigest MultiplyOn(double factor)
    {
        Min *= factor;
        Max *= factor;
        _average *= factor;
        _centroids.Root?.Multiply(factor);

        return this;
    }

    /// <summary>
    /// Divide T-Digest on factor
    /// </summary>
    /// <param name="factor">The factor</param>
    /// <returns>The same instance of T-Digest</returns>
    public TDigest DivideOn(double factor)
    {
        Min /= factor;
        Max /= factor;
        _average /= factor;
        _centroids.Root?.Divide(factor);

        return this;
    }

    /// <summary>
    /// Shift T-Digest on value
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The same instance of T-Digest</returns>
    public TDigest Shift(double value)
    {
        Min += value;
        Max += value;
        _average += value;
        _centroids.Root?.Shift(value);

        return this;
    }

    /// <summary>
    /// Create copy of current T-Digest instance
    /// </summary>
    /// <returns>New T-Digest instance</returns>
    public TDigest Clone() => new(this);

    /// <summary>
    /// Serializes this T-Digest to a byte[]
    /// </summary>
    /// <param name="compressed">If true, serialized distribution points will be compressed</param>
    /// <returns>Serialized bytes array</returns>
    public byte[] Serialize(bool compressed = true)
    {
        var centroids = compressed ? CompressCentroidTree() : _centroids;
        var length = SERIALIZATION_HEADER_SIZE + SERIALIZATION_ITEM_SIZE * centroids.Count;

        var buffer = new byte[length];

        SerializeInternal(buffer, centroids);

        return buffer;
    }

    /// <summary>
    /// Serializes this T-Digest to a Span
    /// </summary>
    /// <param name="target">The target Span for serialization</param>
    /// <param name="compressed">If true, serialized distribution points will be compressed</param>
    /// <returns>Number of bytes written</returns>
    public int Serialize(Span<byte> target, bool compressed = true)
    {
        var centroids = compressed ? CompressCentroidTree() : _centroids;
        var length = SERIALIZATION_HEADER_SIZE + SERIALIZATION_ITEM_SIZE * centroids.Count;

        if (length > target.Length)
            throw new ArgumentException("Target buffer has to low capacity for serialization", nameof(target));

        SerializeInternal(target, _centroids);

        return length;
    }

    /// <summary>
    /// Creates a TDigest from a serialized string of Bytes created by the Serialize() method
    /// </summary>
    /// <param name="serialized"></param>
    public static TDigest Deserialize(ReadOnlySpan<byte> serialized)
    {
        if (serialized == null)
            throw new ArgumentNullException(nameof(serialized), "Serialized parameter cannot be null");

        if (serialized.Length < SERIALIZATION_HEADER_SIZE ||
            (serialized.Length - SERIALIZATION_HEADER_SIZE) % SERIALIZATION_ITEM_SIZE != 0)
            throw new ArgumentException("Serialized data has invalid length");

        var reader = serialized;

        var average = Read(reader);
        reader = reader.Slice(8);

        var accuracy = Read(reader);
        reader = reader.Slice(8);

        var compression = Read(reader);
        reader = reader.Slice(8);

        var min = Read(reader);
        reader = reader.Slice(8);

        var max = Read(reader);
        reader = reader.Slice(8);

        var builder = new CentroidTree.SortedBuilder();

        while (!reader.IsEmpty)
        {
            var mean = Read(reader);
            reader = reader.Slice(8);

            var weight = Read(reader);
            reader = reader.Slice(8);

            builder.Add(new(mean, weight));
        }

        return new(builder.Build())
        {
            _average = average,
            Accuracy = accuracy,
            CompressionConstant = compression,
            Min = min,
            Max = max,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double Read(ReadOnlySpan<byte> buffer)
        {
#if NET6_0_OR_GREATER
            return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
#else
            return !BitConverter.IsLittleEndian ?
                BitConverter.Int64BitsToDouble(ReverseBytes(MemoryMarshal.Read<ulong>(buffer))) :
                MemoryMarshal.Read<double>(buffer);
#endif
        }
    }

    /// <summary>
    /// Merge two T-Digests
    /// </summary>
    /// <param name="a">The first T-Digest</param>
    /// <param name="b">The second T-Digest</param>
    /// <param name="accuracy">Controls the trade-off between accuracy and memory consumption/performance.
    /// Default value is .05, higher values result in worse accuracy, but better performance and decreased memory usage, while
    /// lower values result in better accuracy and increased performance and memory usage</param>
    /// <param name="compression">K value</param>
    /// <returns>A T-Digest created by merging the specified T-Digests</returns>
    public static TDigest Merge(TDigest a, TDigest b, double accuracy = 0.02, double compression = 25)
    {
        var builder = new CentroidTree.SortedBuilder();

        using var enumeratorA = a._centroids.GetEnumerator();
        using var enumeratorB = b._centroids.GetEnumerator();
        IEnumerator<Centroid>? finalEnumerator = null;

        Centroid nodeA;
        Centroid nodeB;

    noValuesYet:
        if (enumeratorA.MoveNext())
            nodeA = enumeratorA.Current;
        else
        {
            finalEnumerator = enumeratorB;
            goto finish;
        }

        if (enumeratorB.MoveNext())
            nodeB = enumeratorB.Current;
        else
        {
            builder.Add(Copy(nodeA));
            finalEnumerator = enumeratorA;
            goto finish;
        }

        while (true)
        {
            if (nodeA.mean == nodeB.mean)
            {
                builder.Add(new(nodeA.mean, nodeA.weight + nodeB.weight));
                goto noValuesYet;
            }

            if (nodeA.mean < nodeB.mean)
            {
                builder.Add(Copy(nodeA));

                if (enumeratorA.MoveNext())
                    nodeA = enumeratorA.Current;
                else
                {
                    builder.Add(Copy(nodeB));
                    finalEnumerator = enumeratorB;
                    goto finish;
                }
            }
            else
            {
                builder.Add(Copy(nodeB));

                if (enumeratorB.MoveNext())
                    nodeB = enumeratorB.Current;
                else
                {
                    builder.Add(Copy(nodeA));
                    finalEnumerator = enumeratorA;
                    goto finish;
                }
            }
        }

    finish:
        if (finalEnumerator is not null)
            while (finalEnumerator.MoveNext())
                builder.Add(Copy(finalEnumerator.Current));

        return new TDigest(builder.Build())
        {
            _average = ((a._average * a.Count) + (b._average * b.Count)) / (a.Count + b.Count),
            Accuracy = accuracy,
            CompressionConstant = compression,
            Min = Math.Min(a.Min, b.Min),
            Max = Math.Max(a.Max, b.Max),
        };

        static Centroid Copy(Centroid node) => new(node.mean, node.weight);
    }

    #region Operators

    /// <summary>
    /// Merge two T-Digests
    /// </summary>
    /// <param name="a">The first T-Digest</param>
    /// <param name="b">The second T-Digest</param>
    /// <returns>A T-Digest created by merging the specified T-Digests</returns>
    public static TDigest operator +(TDigest a, TDigest b) => Merge(a, b);

    /// <summary>
    /// Shift the copy T-Digest on value
    /// </summary>
    /// <param name="digest">The T-Digest</param>
    /// <param name="value">The value</param>
    /// <returns>New instance of the T-Digest</returns>
    public static TDigest operator +(TDigest digest, double value) => digest.Clone().Shift(value);

    /// <summary>
    /// Shift the copy T-Digest on negative value
    /// </summary>
    /// <param name="digest">The T-Digest</param>
    /// <param name="value">The value</param>
    /// <returns>New instance of the T-Digest</returns>
    public static TDigest operator -(TDigest digest, double value) => digest.Clone().Shift(-value);

    /// <summary>
    /// Multiply T-Digest on factor
    /// </summary>
    /// <param name="digest">The T-Digest</param>
    /// <param name="factor">The factor</param>
    /// <returns>New instance of the T-Digest</returns>
    public static TDigest operator *(TDigest digest, double factor) => digest.Clone().MultiplyOn(factor);

    /// <summary>
    /// Divide T-Digest on factor
    /// </summary>
    /// <param name="digest">The T-Digest</param>
    /// <param name="factor">The factor</param>
    /// <returns>New instance of the T-Digest</returns>
    public static TDigest operator /(TDigest digest, double factor) => digest.Clone().DivideOn(factor);

    #endregion

    private void SerializeInternal(Span<byte> target, CentroidTree centroids)
    {
        var writeTarget = target;

        Write(writeTarget, _average);
        writeTarget = writeTarget.Slice(8);

        Write(writeTarget, Accuracy);
        writeTarget = writeTarget.Slice(8);

        Write(writeTarget, CompressionConstant);
        writeTarget = writeTarget.Slice(8);

        Write(writeTarget, Min);
        writeTarget = writeTarget.Slice(8);

        Write(writeTarget, Max);
        writeTarget = writeTarget.Slice(8);

        foreach (var centroid in centroids)
        {
            Write(writeTarget, centroid.mean);
            writeTarget = writeTarget.Slice(8);

            Write(writeTarget, centroid.weight);
            writeTarget = writeTarget.Slice(8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Write(Span<byte> buffer, double value)
        {
#if NET6_0_OR_GREATER
            BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
#else
            if (!BitConverter.IsLittleEndian)
            {
                var tmp = ReverseBytes((ulong)BitConverter.DoubleToInt64Bits(value));
                MemoryMarshal.Write(buffer, ref tmp);
            }
            else
                MemoryMarshal.Write(buffer, ref value);
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateCentroid(Centroid centroid, double threshold, double value, ref double weight, bool withSubTree)
    {
        var delta = Math.Min(threshold - centroid.weight, weight);
        centroid.Update(delta, value, withSubTree);
        weight -= delta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FilterCandidate(ref Centroid? candidate, double threshold, double weight)
    {
        if (candidate is not null && candidate.weight + weight >= threshold)
            candidate = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetThreshold(double q) => 4 * _centroids.Root!.subTreeWeight * Accuracy * q * (1 - q);

    private CentroidTree CompressCentroidTree()
    {
        var builder = new CentroidTree.SortedBuilder();

        using var enumerator = _centroids.GetEnumerator();

        if (enumerator.MoveNext())
        {
            var centroid = enumerator.Current;
            var nearest = new Centroid(centroid.mean, centroid.weight);
            var sum = centroid.weight;
            builder.Add(nearest);

            var count = _centroids.Root!.subTreeWeight;

            while (enumerator.MoveNext())
            {
                centroid = enumerator.Current;
                var weight = centroid.weight;

                var candidate = nearest;

                var threshold = GetThreshold((sum - candidate.weight / 2) / count);
                FilterCandidate(ref candidate, threshold, weight);

                sum += weight;

                if (candidate is not null)
                {
                    UpdateCentroid(candidate, threshold, centroid.mean, ref weight, false);
                    candidate.subTreeWeight = candidate.weight;
                }

                if (weight > 0)
                {
                    nearest = new Centroid(centroid.mean, weight);
                    builder.Add(nearest);
                }
            }
        }

        return builder.Build();
    }

#if !NET6_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReverseBytes(ulong v) => (long)(
        (v & 0x00000000000000FFul) << (8 * 7) |
        (v & 0x000000000000FF00ul) << (8 * 5) |
        (v & 0x0000000000FF0000ul) << (8 * 3) |
        (v & 0x00000000FF000000ul) << (8 * 1) |
        (v & 0x000000FF00000000ul) >> (8 * 1) |
        (v & 0x0000FF0000000000ul) >> (8 * 3) |
        (v & 0x00FF000000000000ul) >> (8 * 5) |
        (v & 0xFF00000000000000ul) >> (8 * 7)
    );
#endif
}
