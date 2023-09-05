namespace TDigestNet.Tests;

internal static class Extensions
{
    public static double Quantile(this IList<double> samples, double q)
    {
        var position = samples.Count * q;
        var index = (int)position;
        position -= index;
        if (index == samples.Count - 1)
            position = 0;

        var value = samples[index];
        if (position != 0)
            value = value * (1 - position) + samples[index + 1] * position;

        return value;
    }
}
