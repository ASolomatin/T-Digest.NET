namespace TDigest.Internal;

partial class CentroidTree
{
    public class SortedBuilder
    {
        private Centroid? _head;
        private Centroid? _tail;
        private int _count;

        public void Add(Centroid node)
        {
            node.subTreeWeight = node.weight;

            if (_tail is null)
            {
                _head = node;
                _tail = node;
                _count = 1;
            }
            else
            {
                _count++;
                _tail.right = node;
                node.parent = _tail;
                _tail = node;
            }
        }

        public CentroidTree Build()
        {
            var tree = new CentroidTree();

            if(_head is null)
                return tree;

            var blackHeight = Log2(_count + 1);
            var maxRed = 1 << blackHeight;
            var red = _count - maxRed + 1;

            tree._min = _head;
            tree._max = _tail;

            var head = _head;
            tree._root = MakeTreeRecursive(ref head, blackHeight, maxRed, red);
            tree._count = _count;

            return tree;

            static Centroid MakeTreeRecursive(ref Centroid rest, int blackHeight, int maxRed, int red)
            {
                Centroid top;

                if (blackHeight == 1)
                {
                    top = rest;

                    rest = rest.right!;
                    if (red > 0)
                    {
                        top.right = null;
                        rest.left = top;
                        top.parent = rest;
                        rest.subTreeWeight += top.subTreeWeight;
                        top = rest;
                        rest = rest.right!;
                        red--;
                    }

                    if (red > 0)
                    {
                        top.right = rest;
                        rest.parent = top;
                        top.subTreeWeight += rest.subTreeWeight;
                        rest = rest.right!;
                        top.right!.right = null;
                    }
                    else
                    {
                        top.right = null;
                    }

                    top.color = NodeColor.Black;
                }
                else
                {
                    maxRed >>= 1;

                    int lRed = red > maxRed ? maxRed : red;
                    var left = MakeTreeRecursive(ref rest, blackHeight - 1, maxRed, lRed);
                    top = rest;

                    rest = rest.right!;
                    top.left = left;
                    left.parent = top;
                    top.subTreeWeight += left.subTreeWeight;
                    top.color = NodeColor.Black;
                    var right = MakeTreeRecursive(ref rest!, blackHeight - 1, maxRed, red - lRed);
                    top.right = right;
                    right.parent = top;
                    top.subTreeWeight += right.subTreeWeight;
                }

                top.parent = null;

                return top;
            }
        }
    }
}
