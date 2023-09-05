using TDigestNet;

Random r = new Random();

TDigest digest = new TDigest();

for (int i = 0; i < 10000; i++)
{
    var n = (r.Next() % 50) + (r.Next() % 50);
    digest.Add(n);
}

Console.WriteLine($"Average: {digest.Average}");
Console.WriteLine($"Percentile 10: {digest.Quantile(10 / 100d)}");
Console.WriteLine($"Percentile 50: {digest.Quantile(50 / 100d)}");
Console.WriteLine($"Percentile 80: {digest.Quantile(80 / 100d)}");
Console.WriteLine($"Percentile 99: {digest.Quantile(99 / 100d)}");
