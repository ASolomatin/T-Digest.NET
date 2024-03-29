﻿using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TDigestNet.Internal;

#if !NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace TDigestNet;

/// <inheritdoc />
public class TDigest : ITDigest
{
    private const int SERIALIZATION_HEADER_SIZE = 8 * 5;
    private const int SERIALIZATION_ITEM_SIZE = 8 * 2;

    private const double DEFAULT_ACCURACY = .02;
    private const double DEFAULT_COMPRESSION = 25;


    private CentroidTree _centroids;
    private double _average;

    /// <inheritdoc />
    public double Count => _centroids.Root?.subTreeWeight ?? 0;

    /// <inheritdoc />
    public int CentroidCount => _centroids.Count;

    /// <inheritdoc />
    public double Accuracy { get; private set; }

    /// <inheritdoc />
    public double CompressionConstant { get; private set; }

    /// <inheritdoc />
    public double Average => _average;

    /// <inheritdoc />
    public double Min { get; private set; }

    /// <inheritdoc />
    public double Max { get; private set; }

    /// <inheritdoc />
    public int ExpectedSerializedBytesLength => SERIALIZATION_HEADER_SIZE + SERIALIZATION_ITEM_SIZE * _centroids.Count;

    internal CentroidTree InternalTree => _centroids;

    /// <summary>
    /// Construct a T-Digest,
    /// </summary>
    /// <param name="accuracy">Controls the trade-off between accuracy and memory consumption/performance.
    /// Default value is .05, higher values result in worse accuracy, but better performance and decreased memory usage, while
    /// lower values result in better accuracy and increased performance and memory usage</param>
    /// <param name="compression">K value</param>
    public TDigest(double accuracy = DEFAULT_ACCURACY, double compression = DEFAULT_COMPRESSION)
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IEnumerable<DistributionPoint> GetDistribution() => _centroids
        .Select(c => new DistributionPoint(c.mean, c.weight));

    /// <inheritdoc />
    public TDigest MultiplyOn(double factor)
    {
        Min *= factor;
        Max *= factor;
        _average *= factor;
        _centroids.Root?.Multiply(factor);

        return this;
    }

    /// <inheritdoc />
    public TDigest DivideOn(double factor)
    {
        Min /= factor;
        Max /= factor;
        _average /= factor;
        _centroids.Root?.Divide(factor);

        return this;
    }

    /// <inheritdoc />
    public TDigest Shift(double value)
    {
        Min += value;
        Max += value;
        _average += value;
        _centroids.Root?.Shift(value);

        return this;
    }

    /// <inheritdoc />
    public TDigest Clone() => new(this);

    /// <inheritdoc />
    public byte[] Serialize(bool compressed = true)
    {
        var centroids = compressed ? CompressCentroidTree() : _centroids;
        var length = SERIALIZATION_HEADER_SIZE + SERIALIZATION_ITEM_SIZE * centroids.Count;

        var buffer = new byte[length];

        SerializeInternal(buffer, centroids);

        return buffer;
    }

    /// <inheritdoc />
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
    public static TDigest Merge(TDigest a, TDigest b, double accuracy = DEFAULT_ACCURACY, double compression = DEFAULT_COMPRESSION)
    {
        var count = a.Count + b.Count;
        var tree = CompressCentroidTree(Enumerate(), count, accuracy);

        return new TDigest(tree)
        {
            _average = ((a._average * a.Count) + (b._average * b.Count)) / count,
            Accuracy = accuracy,
            CompressionConstant = compression,
            Min = Math.Min(a.Min, b.Min),
            Max = Math.Max(a.Max, b.Max),
        };

        IEnumerable<Centroid> Enumerate()
        {
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
                yield return nodeA;
                finalEnumerator = enumeratorA;
                goto finish;
            }

            while (true)
            {
                if (nodeA.mean == nodeB.mean)
                {
                    yield return new(nodeA.mean, nodeA.weight + nodeB.weight);
                    goto noValuesYet;
                }

                if (nodeA.mean < nodeB.mean)
                {
                    yield return nodeA;

                    if (enumeratorA.MoveNext())
                        nodeA = enumeratorA.Current;
                    else
                    {
                        yield return nodeB;
                        finalEnumerator = enumeratorB;
                        goto finish;
                    }
                }
                else
                {
                    yield return nodeB;

                    if (enumeratorB.MoveNext())
                        nodeB = enumeratorB.Current;
                    else
                    {
                        yield return nodeA;
                        finalEnumerator = enumeratorA;
                        goto finish;
                    }
                }
            }

        finish:
            if (finalEnumerator is not null)
                while (finalEnumerator.MoveNext())
                    yield return finalEnumerator.Current;
        }
    }

    /// <summary>
    /// Merge multiple T-Digests with default accuracy and compression settings
    /// </summary>
    /// <param name="digests">T-Digests</param>
    /// <returns>A T-Digest created by merging the specified T-Digests</returns>
    public static TDigest MergeMultiple(params TDigest[] digests) => MergeMultiple(DEFAULT_ACCURACY, DEFAULT_COMPRESSION, digests as IEnumerable<TDigest>);

    /// <summary>
    /// Merge multiple T-Digests
    /// </summary>
    /// <param name="accuracy">Controls the trade-off between accuracy and memory consumption/performance.
    /// Default value is .05, higher values result in worse accuracy, but better performance and decreased memory usage, while
    /// lower values result in better accuracy and increased performance and memory usage</param>
    /// <param name="compression">K value</param>
    /// <param name="digests">T-Digests</param>
    /// <returns>A T-Digest created by merging the specified T-Digests</returns>
    public static TDigest MergeMultiple(double accuracy, double compression, params TDigest[] digests) => MergeMultiple(accuracy, compression, digests as IEnumerable<TDigest>);

    /// <summary>
    /// Merge multiple T-Digests with default accuracy and compression settings
    /// </summary>
    /// <param name="digests">T-Digests</param>
    /// <returns>A T-Digest created by merging the specified T-Digests</returns>
    public static TDigest MergeMultiple(IEnumerable<TDigest> digests) => MergeMultiple(DEFAULT_ACCURACY, DEFAULT_COMPRESSION, digests);

    /// <summary>
    /// Merge multiple T-Digests
    /// </summary>
    /// <param name="accuracy">Controls the trade-off between accuracy and memory consumption/performance.
    /// Default value is .05, higher values result in worse accuracy, but better performance and decreased memory usage, while
    /// lower values result in better accuracy and increased performance and memory usage</param>
    /// <param name="compression">K value</param>
    /// <param name="digests">T-Digests</param>
    /// <returns>A T-Digest created by merging the specified T-Digests</returns>
    public static TDigest MergeMultiple(double accuracy, double compression, IEnumerable<TDigest> digests)
    {
        var count = digests.Sum(d => d.Count);
        var tree = CompressCentroidTree(Enumerate(), count, accuracy);

        var digest = new TDigest(tree)
        {
            _average = digests.Sum(d => d._average * d.Count) / count,
            Accuracy = accuracy,
            CompressionConstant = compression,
            Min = digests.Min(d => d.Min),
            Max = digests.Max(d => d.Max),
        };

        if (digest._centroids.Count > (digest.CompressionConstant / digest.Accuracy))
            digest._centroids = digest.CompressCentroidTree();

        return digest;

        IEnumerable<Centroid> Enumerate()
        {
            var enumerators = digests.Select(d => (IEnumerator<Centroid>?)d._centroids.GetEnumerator()).ToArray();
            var enumeratorsCount = enumerators.Length;
            var enumeratorsLost = enumeratorsCount;
            var centroids = new Centroid?[enumeratorsCount];
            try
            {
                for (int i = 0; i < enumeratorsCount; i++)
                    LoadValue(i);

                while (enumeratorsLost != 0)
                {
                    Centroid? minimum = null;
                    for (int i = 0; i < enumeratorsCount; i++)
                    {
                        var current = centroids[i];
                        if (current is not null && (minimum is null || current.mean < minimum.mean))
                            minimum = current;
                    }

                    if (minimum is null)
                        throw new ApplicationException("Centroid is null but this was not expected");

                    var weight = .0;
                    for (int i = 0; i < enumeratorsCount; i++)
                    {
                        var current = centroids[i];
                        if (current is not null && minimum.mean == current.mean)
                        {
                            weight += current.weight;
                            LoadValue(i);
                        }
                    }

                    if (weight == minimum.weight)
                        yield return minimum;
                    else
                        yield return new(minimum.mean, weight);
                }
            }
            finally
            {
                foreach (var enumerator in enumerators)
                    enumerator?.Dispose();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void LoadValue(int i)
            {
                var enumerator = enumerators[i];

                if (enumerator is null)
                    throw new ApplicationException("Enumerator is null but this was not expected");

                if (enumerator.MoveNext())
                    centroids[i] = enumerator.Current;
                else
                {
                    enumerator.Dispose();
                    enumerators[i] = null;
                    centroids[i] = null;
                    enumeratorsLost--;
                }
            }
        }
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
    private double GetThreshold(double q) => GetThreshold(q, _centroids.Root!.subTreeWeight, Accuracy);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetThreshold(double q, double count, double accuracy) => 4 * count * accuracy * q * (1 - q);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CentroidTree CompressCentroidTree() => CompressCentroidTree(_centroids, _centroids.Root!.subTreeWeight, Accuracy);

    private static CentroidTree CompressCentroidTree(IEnumerable<Centroid> centroids, double count, double accuracy)
    {
        var builder = new CentroidTree.SortedBuilder();

        using var enumerator = centroids.GetEnumerator();

        if (enumerator.MoveNext())
        {
            var centroid = enumerator.Current;
            var nearest = new Centroid(centroid.mean, centroid.weight);
            var sum = centroid.weight;
            builder.Add(nearest);

            while (enumerator.MoveNext())
            {
                centroid = enumerator.Current;
                var weight = centroid.weight;

                var candidate = nearest;

                var threshold = GetThreshold((sum - candidate.weight / 2) / count, count, accuracy);
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
