using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs.RefType;

class Constructors
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
            Hello = new RefType().Hello;
        }
    }

    static void Kernel(Index1D index, ArrayView<int> input)
    {
        ValueType value = new ValueType();
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