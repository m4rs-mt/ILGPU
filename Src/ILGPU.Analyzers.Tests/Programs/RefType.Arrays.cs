using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs;

class Arrays
{
    class RefType
    {
        public int Hello => 42;
    }

    struct ValueType
    {
        public int Hello;

        public ValueType()
        {
            Hello = 42;
        }
    }

    static void Kernel(Index1D index, ArrayView<int> input)
    {
        ValueType[] array = [new ValueType()];
        int[] ints = [0, 1, 2];

        // TODO: the new collection expressions seem to have an issue where analyses
        // will be produced twice. If anyone has any information on this, please
        // let me know.
        RefType[] refs = { new RefType() };
    }

    static void Run()
    {
        using var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        using var accelerator = device.CreateAccelerator(context);

        using var input = accelerator.Allocate1D<int>(1024);

        var kernel =
            accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(Kernel);

        kernel(input.IntExtent, input.View);

        accelerator.Synchronize();
    }
}