namespace TDigestNet.Tests;

public class TestUniformDistribution : TestBase
{
    private readonly Random _rand = new();
    private readonly List<double> _actual = new();
    private readonly TDigest _digest = new();

    public TestUniformDistribution()
    {
        for (int i = 0; i < 50000; i++)
        {
            var v = _rand.NextDouble();
            _digest.Add(v);
            _actual.Add(v);
        }

        _actual.Sort();
    }

    [Test, Order(0)]
    public void ValidateStructure() => ValidateInternalTree(_digest);

    [Test]
    public void TestCount() => Assert.Multiple(() =>
    {
        Assert.That(_actual.Count, Is.EqualTo(50000));
        Assert.That(_digest.Count, Is.EqualTo(50000));
    });

    [Test]
    public void TestAvgError() => Assert.That(GetAvgError(_actual, _digest), Is.LessThan(.01));

    [Test]
    public void TestAvgPercentileError() => Assert.That(GetAvgPercentileError(_actual, _digest), Is.LessThan(.0005));

    [Test]
    public void TestMinMax() => Assert.Multiple(() =>
    {
        Assert.That(MaxIsEqual(_actual, _digest));
        Assert.That(MinIsEqual(_actual, _digest));
    });
}