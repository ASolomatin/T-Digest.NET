namespace TDigest.Tests;

public class TestSequential : TestBase
{
    private readonly List<double> _actual = new();
    private readonly TDigest _digest = new(.01);

    public TestSequential()
    {
        for (int i = 0; i < 10000; i++)
        {
            _digest.Add(i);
            _actual.Add(i);
        }
    }

    [Test, Order(0)]
    public void ValidateStructure() => ValidateInternalTree(_digest);

    [Test]
    public void TestAvgError() => Assert.That(GetAvgError(_actual, _digest), Is.LessThan(.01));

    [Test]
    public void TestAvgPercentileError() => Assert.That(GetAvgPercentileError(_actual, _digest), Is.LessThan(5));

    [Test]
    public void TestMinMax() => Assert.Multiple(() =>
    {
        Assert.That(MaxIsEqual(_actual, _digest));
        Assert.That(MinIsEqual(_actual, _digest));
    });
}