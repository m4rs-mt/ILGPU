using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs.RefType;

class ManagedUnmanaged
{
    class RefType
    {
    } 
    
    struct Unmanaged
    {
        private int a;
        private int b;
    }

    struct Managed
    {
        private int a;
        private RefType r;
    }
    
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
        var unmanaged = new Unmanaged();
        var managed = new Managed();
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