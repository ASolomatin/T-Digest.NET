using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TDigestNet.Internal;

internal partial class CentroidTree : IEnumerable<Centroid>
{
    private Centroid? _root;
    private Centroid? _min;
    private Centroid? _max;
    private int _count;

    public Centroid? Root => _root;
    public Centroid? Min => _min;
    public Centroid? Max => _max;
    public int Count => _count;

    public CentroidTree() { }

    private CentroidTree(CentroidTree tree)
    {
        _count = tree._count;

        if (tree._root is not null)
            _root = tree._root.CloneSubTree(out _min, out _max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetOrClosest(double mean, [NotNullWhen(true)] out Centroid? a, out Centroid? b)
    {
        var node = _root;
        a = null;
        b = null;

        while (node is not null)
        {
            if (node.mean == mean)
            {
                a = node;
                b = null;

                return true;
            }

            if (node.mean < mean)
            {
                a = node;
                node = node.right;
            }
            else
            {
                b = node;
                node = node.left;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Centroid? FindByWeight(double weight, out double point)
    {
        var node = _root;

        if (node is null)
        {
            point = 0;
            return null;
        }

        if (weight <= 0)
        {
            point = _min!.weight / 2;
            return _min;
        }

        if (node.subTreeWeight <= weight)
        {
            point = node.subTreeWeight - _max!.weight / 2;
            return _max;
        }

        var sum = 0d;

        while (true)
        {
            var leftWeight = sum + (node.left?.subTreeWeight ?? 0);

            if (leftWeight > weight)
            {
                if (node.left is null)
                    goto returnCurrent;

                node = node.left;
                continue;
            }

            if (leftWeight + node.weight < weight)
            {
                if (node.right is null)
                    goto returnCurrent;

                sum = leftWeight + node.weight;
                node = node.right;
                continue;
            }

        returnCurrent:
            point = sum + (node.left?.subTreeWeight ?? 0) + node.weight / 2;
            return node;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Centroid? GoLeft(Centroid node)
    {
        if (ReferenceEquals(node, _min))
            return null;

        if (node.left is not null)
        {
            for (node = node.left; node.right is not null; node = node.right) ;

            return node;
        }

        while (node.parent is not null)
        {
            if (ReferenceEquals(node.parent.right, node))
                return node.parent;

            node = node.parent;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Centroid? GoRight(Centroid node)
    {
        if (ReferenceEquals(node, _max))
            return null;

        if (node.right is not null)
        {
            for (node = node.right; node.left is not null; node = node.left) ;

            return node;
        }

        while (node.parent is not null)
        {
            if (ReferenceEquals(node.parent.left, node))
                return node.parent;

            node = node.parent;
        }

        return null;
    }

    public void Add(double mean, double weight)
    {
        Centroid? parent = null;
        var node = _root;
        while (node is not null)
        {
            node.subTreeWeight += weight;
            if (mean == node.mean)
            {
                node.weight += weight;
                return;
            }

            parent = node;
            node = mean < node.mean ? node.left : node.right;
        }

        node = new Centroid(mean, weight)
        {
            color = NodeColor.Red,
            subTreeWeight = weight,
            parent = parent,
        };

        if (parent == null)
        {
            _root = node;
            _min = node;
            _max = node;
        }
        else if (mean < parent.mean)
        {
            parent.left = node;
            if (ReferenceEquals(_min, parent))
                _min = node;
        }
        else
        {
            parent.right = node;
            if (ReferenceEquals(_max, parent))
                _max = node;
        }

        _count++;

        while (node.parent is not null && node.parent.color == NodeColor.Red)
        {
            if (ReferenceEquals(node.parent, node.parent.parent?.left))
            {
                if (node.parent.parent.right is not null && node.parent.parent.right.color == NodeColor.Red)
                {
                    node.parent.color = NodeColor.Black;
                    node.parent.parent.right.color = NodeColor.Black;
                    node.parent.parent.color = NodeColor.Red;
                    node = node.parent.parent;

                    continue;
                }

                if (ReferenceEquals(node, node.parent.right))
                {
                    node = node.parent;
                    RotateLeft(node);
                }

                node.parent!.color = NodeColor.Black;

                node.parent.parent!.color = NodeColor.Red;
                RotateRight(node.parent.parent);

                continue;
            }

            if (node.parent.parent?.left is not null && node.parent.parent.left.color == NodeColor.Red)
            {
                node.parent.color = NodeColor.Black;
                node.parent.parent.left.color = NodeColor.Black;
                node.parent.parent.color = NodeColor.Red;
                node = node.parent.parent;

                continue;
            }

            if (ReferenceEquals(node, node.parent.left))
            {
                node = node.parent;
                RotateRight(node);
            }

            node.parent!.color = NodeColor.Black;

            node.parent.parent!.color = NodeColor.Red;
            RotateLeft(node.parent.parent);
        }

        _root!.color = NodeColor.Black;
    }

    public IEnumerator<Centroid> GetEnumerator()
    {
        var node = _min;

        if (node is null)
            yield break;

        var maxDepth = Log2(_count + 1) << 1;
        var hints = new Centroid[maxDepth - 2];
        var hintCount = 0;

    traverse:
        yield return node;

        if (ReferenceEquals(node, _max))
            yield break;

        if (node.right is not null)
        {
            node = node.right;
            while (node.left is not null)
            {
                hints[hintCount++] = node;
                node = node.left;
            }

            goto traverse;
        }

        if (hintCount != 0)
        {
            node = hints[--hintCount];
            goto traverse;
        }

        Centroid? parent;
        while ((parent = node.parent) is not null)
        {
            if (ReferenceEquals(node, parent.left))
            {
                node = parent;
                goto traverse;
            }

            node = parent;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public CentroidTree Clone() => new(this);

    private void RotateLeft(Centroid node)
    {
        var rightNode = node.right!;
        node.right = rightNode.left;

        var subTreeWeight = node.subTreeWeight;
        node.subTreeWeight -= rightNode.subTreeWeight;
        rightNode.subTreeWeight = subTreeWeight;

        if (rightNode.left is not null)
        {
            rightNode.left.parent = node;
            node.subTreeWeight += rightNode.left.subTreeWeight;
        }

        rightNode.parent = node.parent;
        if (node.parent is null)
            _root = rightNode;
        else if (ReferenceEquals(node, node.parent.left))
            node.parent.left = rightNode;
        else
            node.parent.right = rightNode;

        rightNode.left = node;
        node.parent = rightNode;
    }

    private void RotateRight(Centroid node)
    {
        var leftNode = node.left!;
        node.left = leftNode.right;

        var subTreeWeight = node.subTreeWeight;
        node.subTreeWeight -= leftNode.subTreeWeight;
        leftNode.subTreeWeight = subTreeWeight;

        if (leftNode.right is not null)
        {
            leftNode.right.parent = node;
            node.subTreeWeight += leftNode.right.subTreeWeight;
        }

        leftNode.parent = node.parent;
        if (node.parent is null)
            _root = leftNode;
        else if (ReferenceEquals(node, node.parent.right))
            node.parent.right = leftNode;
        else
            node.parent.left = leftNode;

        leftNode.right = node;
        node.parent = leftNode;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static int Log2(int v)
    {
        int r = 0xFFFF - v >> 31 & 0x10;
        v >>= r;
        int shift = 0xFF - v >> 31 & 0x8;
        v >>= shift;
        r |= shift;
        shift = 0xF - v >> 31 & 0x4;
        v >>= shift;
        r |= shift;
        shift = 0x3 - v >> 31 & 0x2;
        v >>= shift;
        r |= shift;
        r |= v >> 1;
        return r;
    }
}
