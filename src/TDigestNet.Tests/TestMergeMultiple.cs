namespace TDigestNet.Tests;

public class TestMergeMultiple : TestBase
{
    private readonly Random _rand = new();
    private readonly List<double> _actual = new();
    private readonly TDigest _digestA = new();
    private readonly TDigest _digestB = new();
    private readonly TDigest _digestC = new();
    private readonly TDigest _digestD = new();
    private readonly TDigest _merged;

    public TestMergeMultiple()
    {
        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 50) + (_rand.Next() % 50);
            _digestA.Add(n);
            _actual.Add(n);
        }

        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 100) + (_rand.Next() % 100);
            _digestB.Add(n);
            _actual.Add(n);
        }

        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 50) + (_rand.Next() % 50) + 100;
            _digestC.Add(n);
            _actual.Add(n);
        }

        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 100) + (_rand.Next() % 100) + 100;
            _digestD.Add(n);
            _actual.Add(n);
        }

        _actual.Sort();

        _merged = TDigest.MergeMultiple(_digestA,  _digestB, _digestC, _digestD);
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
    public void TestAvgPercentileError() => Assert.That(GetAvgPercentileError(_actual, _merged), Is.LessThan(1));
}