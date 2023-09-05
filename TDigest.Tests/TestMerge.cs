namespace TDigest.Tests;

public class TestMerge : TestBase
{
    private readonly Random _rand = new();
    private readonly List<double> _actual = new();
    private readonly TDigest _digestA = new();
    private readonly TDigest _digestB = new();
    private readonly TDigest _digestAll = new();
    private readonly TDigest _merged;

    public TestMerge()
    {
        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 50) + (_rand.Next() % 50);
            _digestA.Add(n);
            _digestAll.Add(n);
            _actual.Add(n);
        }

        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 100) + (_rand.Next() % 100);
            _digestB.Add(n);
            _digestAll.Add(n);
            _actual.Add(n);
        }

        _actual.Sort();

        _merged = _digestA + _digestB;
    }

    [Test, Order(0)]
    public void ValidateStructure() => ValidateInternalTree(_merged);

    [Test, Order(1)]
    public void TestNotNull() => Assert.IsNotNull(_merged);

    [Test]
    public void TestCount() => Assert.That(_actual.Count, Is.EqualTo(_merged.Count));

    [Test]
    public void TestAvgError() => Assert.That(GetAvgError(_actual, _merged!), Is.LessThan(.01));

    [Test]
    public void TestAvgPercentileError() => Assert.That(GetAvgPercentileError(_actual, _merged), Is.LessThan(.5));
}