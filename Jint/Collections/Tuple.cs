namespace Jint.Collections
{
    public readonly struct Tuple<A,B>
    {
        public Tuple(A first, B second)
        {
            First = first;
            Second = second;
        }

        public void Deconstruct(out A first, out B second)
        {
            first = First;
            second = Second;
        }

        public A First  { get; }
        public B Second { get; }
    }
}