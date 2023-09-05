namespace TDigest.Tests;

public class TestConstantValue : TestBase
{
    private readonly Random _rand = new();
    private readonly List<double> _actual = new();
    private readonly TDigest _digest = new();

    public TestConstantValue()
    {
        for (int i = 0; i < 10000; i++)
        {
            _digest.Add(100);
            _actual.Add(100);
        }
    }

    [Test, Order(0)]
    public void ValidateStructure() => ValidateInternalTree(_digest);

    [Test]
    public void TestAvgError() => Assert.That(GetAvgError(_actual, _digest), Is.LessThan(.01));

    [Test]
    public void TestAvgPercentileError() => Assert.That(GetAvgPercentileError(_actual, _digest), Is.EqualTo(0));

    [Test]
    public void TestMinMax() => Assert.Multiple(() =>
    {
        Assert.That(MaxIsEqual(_actual, _digest));
        Assert.That(MinIsEqual(_actual, _digest));
    });
}