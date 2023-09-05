namespace TDigest.Tests;

public abstract partial class TestBase
{
    protected static double GetAvgPercentileError(IList<double> all, TDigest digest) => Enumerable.Range(1, 999)
        .Select(n => n / 1000.0)
        .Average(q => Math.Abs(all.Quantile(q) - digest.Quantile(q)));

    protected static double GetAvgPercentileError(TDigest digestA, TDigest digestB) => Enumerable.Range(1, 999)
        .Select(n => n / 1000.0)
        .Average(q => Math.Abs(digestA.Quantile(q) - digestB.Quantile(q)));

    protected static double GetAvgError(IList<double> actual, TDigest digest) => Math.Abs(actual.Average() - digest.Average);

    protected static bool MaxIsEqual(IList<double> actual, TDigest digest) => actual.Max() == digest.Max;

    protected static bool MinIsEqual(IList<double> actual, TDigest digest) => actual.Min() == digest.Min;
}
