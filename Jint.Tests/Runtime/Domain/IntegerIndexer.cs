namespace Jint.Tests.Runtime.Domain;

public class IntegerIndexer
{
    private readonly int[] data;

    public IntegerIndexer()
    {
        data = new[] {123, 0, 0, 0, 0};
    }

    public int this[int i]
    {
        get => data[i];
        set => data[i] = value;
    }
}