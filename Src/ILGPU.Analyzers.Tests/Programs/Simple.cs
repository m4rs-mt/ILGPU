using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs;

public class Simple
{
    class RefType
    {
        public int Hello => 42;
    }

    static void Kernel(Index1D index, ArrayView<int> input, ArrayView<int> output)
    {
        var refType = new RefType();
        output[index] = input[index] + refType.Hello;
    }

    static void Run()
    {
        using var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        using var accelerator = device.CreateAccelerator(context);

        using var input = accelerator.Allocate1D<int>(1024);
        using var output = accelerator.Allocate1D<int>(1024);

        var kernel =
            accelerator
                .LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<int>>(
                    Kernel);

        kernel(input.IntExtent, input.View, output.View);

        accelerator.Synchronize();
    }
}
