namespace MiningVolume.Core.Compatibility
{
    /// <summary>One immutable empty array per element type, including .NET 4.5.
    /// Array.Empty was introduced in 4.6; do not raise the host CLR to use it.</summary>
    public static class EmptyArray<T>
    {
        public static readonly T[] Value = new T[0];
    }
}
