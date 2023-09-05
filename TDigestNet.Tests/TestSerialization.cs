namespace TDigestNet.Tests;

public class TestSerialization : TestBase
{
    private readonly Random _rand = new();
    private readonly TDigest _digestA = new();
    private readonly TDigest _digestB;
    private readonly TDigest _digestC;

    public TestSerialization()
    {
        for (int i = 0; i < 10000; i++)
        {
            var n = (_rand.Next() % 50) + (_rand.Next() % 50);
            _digestA.Add(n);
        }

        byte[] sN = _digestA.Serialize(false);
        byte[] sC = _digestA.Serialize();
        _digestB = TDigestNet.Deserialize(sN);
        _digestC = TDigestNet.Deserialize(sC);
    }

    [Test, Order(0)]
    public void TestNotNull() => Assert.Multiple(() =>
    {
        Assert.IsNotNull(_digestB);
        Assert.IsNotNull(_digestC);
    });

    [Test, Order(1)]
    public void ValidateStructure()
    {
        ValidateInternalTree(_digestB);
        ValidateInternalTree(_digestC);
    }

    [Test]
    public void TestDistribution()
    {
        var a = _digestA.GetDistribution().ToArray();
        var b = _digestB.GetDistribution().ToArray();
        var c = _digestC.GetDistribution().ToArray();

        Assert.That(b.Length, Is.EqualTo(a.Length));
        Assert.That(c.Length, Is.LessThanOrEqualTo(a.Length));

        for (int i = 0; i < a.Length; i++)
            Assert.Multiple(() =>
            {
                Assert.That(a[i].Count, Is.EqualTo(b[i].Count), "Centroid counts are not equal after serialization");
                Assert.That(a[i].Value, Is.EqualTo(b[i].Value), "Centroid means are not equal after serialization");
            });
    }

    [Test]
    public void TestAverage() => Assert.Multiple(() =>
    {
        Assert.That(_digestA.Average, Is.EqualTo(_digestB.Average), "Averages are not equal after serialization");
        Assert.That(Math.Abs(_digestC.Average - _digestA.Average), Is.LessThan(.01));
    });

    [Test]
    public void TestCount() => Assert.Multiple(() =>
    {
        Assert.That(_digestB.Count, Is.EqualTo(10000), "Counts are not equal after serialization");
        Assert.That(_digestC.Count, Is.EqualTo(10000), "Counts are not equal after serialization");
    });

    [Test]
    public void TestAvgPercentileError() => Assert.Multiple(() =>
    {
        Assert.That(GetAvgPercentileError(_digestA, _digestB), Is.EqualTo(0));
        Assert.That(GetAvgPercentileError(_digestA, _digestC), Is.LessThan(.01));
    });

    [Test]
    public void TestMinMax() => Assert.Multiple(() =>
    {
        Assert.That(_digestB.Min, Is.EqualTo(_digestA.Min), "Minimum is not equal after serialization");
        Assert.That(_digestC.Min, Is.EqualTo(_digestA.Min), "Minimum is not equal after serialization");
        Assert.That(_digestB.Max, Is.EqualTo(_digestA.Max), "Maximum is not equal after serialization");
        Assert.That(_digestC.Max, Is.EqualTo(_digestA.Max), "Maximum is not equal after serialization");
    });

    [Test]
    public void TestCentroidCount() => Assert.Multiple(() =>
    {
        Assert.That(_digestB.CentroidCount, Is.EqualTo(_digestA.CentroidCount), "Centroid Counts are not equal after serialization");
        Assert.That(_digestC.CentroidCount, Is.LessThanOrEqualTo(_digestA.CentroidCount), "Centroid Counts are not equal after serialization");
    });

    [Test]
    public void TestCompressionConstant() => Assert.Multiple(() =>
    {
        Assert.That(_digestB.CompressionConstant, Is.EqualTo(_digestA.CompressionConstant), "Compression Constants are not equal after serialization");
        Assert.That(_digestC.CompressionConstant, Is.EqualTo(_digestA.CompressionConstant), "Compression Constants are not equal after serialization");
    });

    [Test]
    public void TestAccuracy() => Assert.Multiple(() =>
    {
        Assert.That(_digestB.Accuracy, Is.EqualTo(_digestA.Accuracy), "Accuracies are not equal after serialization");
        Assert.That(_digestC.Accuracy, Is.EqualTo(_digestA.Accuracy), "Accuracies are not equal after serialization");
    });

    [Test]
    public void TestSame()
    {
        var centroidsA = _digestA.InternalTree.ToArray();
        var centroidsB = _digestB.InternalTree.ToArray();

        var sequenceA = Enumerable.Range(1, 999).Select(n => _digestA.Quantile(n / 1000d));
        var sequenceB = Enumerable.Range(1, 999).Select(n => _digestB.Quantile(n / 1000d));

        Assert.That(sequenceA, Is.EquivalentTo(sequenceB), "Serialized TDigest is not the same as original");
    }
}