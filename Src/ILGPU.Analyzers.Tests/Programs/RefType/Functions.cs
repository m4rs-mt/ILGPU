using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs.RefType;

class Functions
{
    class RefType
    {
        public int Hello => 42;
    }
    
    // This would be disallowed anyways, but the analyzer shouldn't hang
    static int Recursion(int i)
    {
        if (i == 0) return new RefType().Hello;
        return Recursion(i - 1);
    }

    static int MutualRecursion1(int i)
    {
        if (i == 0) return new RefType().Hello;
        return MutualRecursion2(i - 1);
    }

    static int MutualRecursion2(int i)
    {
        if (i == 0) return 0;  // TODO: add allocation here
        return MutualRecursion1(i - 1);
    }

    static int AnotherFunction()
    {
        return new RefType().Hello;
    }

    static void Kernel(Index1D index, ArrayView<int> input)
    {
        int result = AnotherFunction();
        int rec1 = Recursion(10);
        int rec2 = MutualRecursion1(10);
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