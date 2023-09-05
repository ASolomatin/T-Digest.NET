using TDigestNet.Internal;

namespace TDigestNet.Tests;

partial class TestBase
{
    protected void ValidateInternalTree(TDigest digest)
    {
        var tree = digest.InternalTree;
        Assert.That(tree, Is.Not.Null);

        if (digest.CentroidCount == 0)
        {
            Assert.That(tree.Count, Is.Zero);
            Assert.That(tree.Root, Is.Null);
            Assert.That(tree.Max, Is.Null);
            Assert.That(tree.Min, Is.Null);

            return;
        }

        Assert.That(tree.Count, Is.EqualTo(digest.CentroidCount));
        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Max, Is.Not.Null);
        Assert.That(tree.Min, Is.Not.Null);

        Assert.That(tree.Root!.color, Is.EqualTo(NodeColor.Black));

        var (minBlacks, maxBlacks) = CountSubTreeBlacks(tree.Root);

        Assert.That(minBlacks, Is.EqualTo(maxBlacks));

        ValidateParents(tree.Root, null);
        ValidateWeights(tree.Root);

        double? lastMean = null;

        foreach (var node in tree)
        {
            if (lastMean is null)
            {
                lastMean = node.mean;
                continue;
            }

            Assert.That(node.mean, Is.GreaterThan(lastMean));

            lastMean = node.mean;
        }
    }

    double ValidateWeights(Centroid node)
    {
        var weight = node.weight;
        if (node.left is not null)
            weight += ValidateWeights(node.left);
        if (node.right is not null)
            weight += ValidateWeights(node.right);

        Assert.That(node.subTreeWeight, Is.EqualTo(weight));

        return weight;
    }

    private void ValidateParents(Centroid node, Centroid? parent)
    {
        Assert.That(node.parent, Is.SameAs(parent));

        if (node.left is not null)
            ValidateParents(node.left, node);

        if (node.right is not null)
            ValidateParents(node.right, node);
    }

    private void ValidateColors(Centroid node)
    {
        if (node.color == NodeColor.Red)
        {
            var leftColor = node.left?.color ?? NodeColor.Black;
            var rightColor = node.right?.color ?? NodeColor.Black;

            Assert.That(leftColor, Is.EqualTo(NodeColor.Black));
            Assert.That(rightColor, Is.EqualTo(NodeColor.Black));
        }

        if (node.left is not null)
            ValidateColors(node.left);

        if (node.right is not null)
            ValidateColors(node.right);
    }

    private (int min, int max) CountSubTreeBlacks(Centroid node)
    {
        var current = node.color == NodeColor.Black ? 1 : 0;
        var (leftMin, leftMax) = node.left is null ? (1, 1) : CountSubTreeBlacks(node.left);
        var (rightMin, rightMax) = node.right is null ? (1, 1) : CountSubTreeBlacks(node.right);

        var min = Math.Min(leftMin, rightMin) + current;
        var max = Math.Min(leftMax, rightMax) + current;

        return (min, max);
    }
}
