using System.Collections.Immutable;

namespace ILGPU.IR
{
    public record struct IRMethod(long Id, string Name, long ReturnType, ImmutableArray<long> Blocks)
    {
    }
}
