namespace TDigestNet.Tests;

public class TestNormalDistribution : TestBase
{
    private readonly Random _rand = new();
    private readonly List<double> _actual = new();
    private readonly TDigest _digest = new();

    public TestNormalDistribution()
    {
        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 100) + (_rand.Next() % 100);
            _digest.Add(n);
            _actual.Add(n);
        }

        _actual.Sort();
    }

    [Test, Order(0)]
    public void ValidateStructure() => ValidateInternalTree(_digest);

    [Test]
    public void TestZero()=> Assert.DoesNotThrow(() => _digest.Quantile(0));

    [Test]
    public void TestAvgError() => Assert.That(GetAvgError(_actual, _digest), Is.LessThan(.01));

    [Test]
    public void TestAvgPercentileError() => Assert.That(GetAvgPercentileError(_actual, _digest), Is.LessThan(.5));

    [Test]
    public void TestMinMax() => Assert.Multiple(() =>
    {
        Assert.That(MaxIsEqual(_actual, _digest));
        Assert.That(MinIsEqual(_actual, _digest));
    });
}