using System.Diagnostics;
using TDigestNet;

Random random = new();
TDigest digest = new();
Stopwatch stopwatch = new();

stopwatch.Start();
for (int i = 0; i < 1000000; i++)
{
    var n = random.NextDouble() * 100;
    digest.Add(n);
}
stopwatch.Stop();

Console.WriteLine($"Average: {digest.Average}");
Console.WriteLine($"Percentile 10: {digest.Quantile(10 / 100d)}");
Console.WriteLine($"Percentile 50: {digest.Quantile(50 / 100d)}");
Console.WriteLine($"Percentile 80: {digest.Quantile(80 / 100d)}");
Console.WriteLine($"Percentile 99: {digest.Quantile(99 / 100d)}");
Console.WriteLine($"Time spent for 1M samples: {stopwatch.Elapsed}");

