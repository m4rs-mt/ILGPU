using System;

namespace ILGPU.Analyzers.Tests.Programs.ManagedType;

class ILGPUTypesIntrinsics
{
    static void Kernel(Index1D index, ArrayView<int> input)
    {
        var a = input.SubView(0, 10);
        int b = a[index];
        int c = Warp.WarpIdx;
        Group.Barrier();
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