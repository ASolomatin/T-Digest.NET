namespace TDigest.Internal;

internal sealed class Centroid
{
    public double mean;
    public double weight;

    public NodeColor color;
    public Centroid? parent;
    public Centroid? left;
    public Centroid? right;

    public double subTreeWeight;

    public Centroid(double mean, double weight)
    {
        this.mean = mean;
        this.weight = weight;
    }

    private Centroid() { }

    public void Update(double deltaWeight, double value, bool withSubTree)
    {
        weight += deltaWeight;
        mean += deltaWeight * (value - mean) / weight;

        if (withSubTree)
            for (var node = this; node is not null; node = node.parent)
                node.subTreeWeight += deltaWeight;
    }

    public double SumOfLeft()
    {
        var node = this;

        var sum = node.subTreeWeight - (node.right?.subTreeWeight ?? 0);

        while(node.parent is not null)
        {
            if(ReferenceEquals(node, node.parent.right))
                sum += node.parent.subTreeWeight - node.subTreeWeight;

            node = node.parent;
        }

        return sum;
    }

    public Centroid CloneSubTree(out Centroid min, out Centroid max)
    {
        var node = min = max = new Centroid();

        node.mean = mean;
        node.weight = weight;
        node.color = color;
        node.subTreeWeight = subTreeWeight;

        if (left is not null)
        {
            node.left = left.CloneSubTree(out min, out _);
            node.left.parent = node;
        }

        if (right is not null)
        {
            node.right = right.CloneSubTree(out _, out max);
            node.right.parent = node;
        }

        return node;
    }

    public void Multiply(double factor)
    {
        mean *= factor;
        left?.Multiply(factor);
        right?.Multiply(factor);
    }

    public void Divide(double factor)
    {
        mean /= factor;
        left?.Divide(factor);
        right?.Divide(factor);
    }

    public void Shift(double factor)
    {
        mean += factor;
        left?.Shift(factor);
        right?.Shift(factor);
    }
}
