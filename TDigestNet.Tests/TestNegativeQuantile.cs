namespace TDigestNet.Tests;

public class TestNegativeQuantile : TestBase
{
    private readonly Random _rand = new();
    private readonly List<int> _numbers = new();
    private readonly TDigest _digest = new();

    [Test]
    public void TestNotNegative()
    {
        for (var i = 0; i < 10 * 1000; i++)
        {
            var n = _rand.NextDouble() < 0.001 ? 10001 : _rand.Next(0, 100);
            _digest.Add(n);
            _numbers.Add(n);
            var q99 = _digest.Quantile(0.99);

            Assert.That(q99, Is.GreaterThanOrEqualTo(0), "q99: {0}, numbers: {1}", q99, string.Join(", ", _numbers));
        }
    }
}