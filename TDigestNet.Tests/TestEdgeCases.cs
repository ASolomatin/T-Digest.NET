namespace TDigestNet.Tests;

public class TestEdgeCases : TestBase
{
    private readonly TDigest _digest = new();

    [Test, Order(0)]
    public void TestZeroElements()
    {
        ValidateInternalTree(_digest);

        Assert.Throws<InvalidOperationException>(() => _digest.Quantile(.5), "Didn't throw exception when quantile() called before adding any elements");
    }

    [Test, Order(1)]
    public void TestOneElement()
    {
        _digest.Add(50);

        ValidateInternalTree(_digest);

        Assert.Multiple(() =>
        {
            Assert.That(_digest.Quantile(.5), Is.EqualTo(50));
            Assert.That(_digest.Quantile(0), Is.EqualTo(50));
            Assert.That(_digest.Quantile(1), Is.EqualTo(50));
        });
    }

    [Test, Order(2)]
    public void TestTwoElements()
    {
        _digest.Add(100);

        ValidateInternalTree(_digest);

        Assert.That(_digest.Quantile(1), Is.EqualTo(100));
    }
}