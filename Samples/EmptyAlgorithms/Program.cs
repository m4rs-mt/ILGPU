using ILGPU;

namespace EmptyAlgorithms;

static class Program
{
    static void Main()
    {
        // In the new API, no special EnableAlgorithms() call is needed.
        // Algorithm operations (scan, reduce, radix sort, initialize, sequence,
        // transform) are built into ILGPU directly via:
        //   using ILGPU.ScanReduce;
        //   using ILGPU.RadixSort;
        //   using ILGPU.Initialization;
        using var context = Context.CreateDefault();
    }
}
