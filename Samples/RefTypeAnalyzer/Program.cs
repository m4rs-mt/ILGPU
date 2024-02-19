using ILGPU;
using ILGPU.Runtime;

class Program
{
    class MyRefType
    {
        public int Hello => 42;
    }

    struct MyUnmanagedType
    {
        public int Hello;

        public MyUnmanagedType()
        {
            Hello = 42;
        }
    }
    
    void Kernel(Index1D index, ArrayView<int> input, ArrayView<int> output)
    {
        // This is disallowed, since MyRefType is a reference type
        var refType = new MyRefType();
        output[index] = input[index] + refType.Hello;
        
        // Allocating arrays of unmanaged types is fine
        MyUnmanagedType[] array = [new MyUnmanagedType()];
        int[] ints = [0, 1, 2];
        
        // But arrays of reference types are still disallowed
        MyRefType[] refs = [new MyRefType()];
    }
    
    void Main(string[] args)
    {
        var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        var accelerator = device.CreateAccelerator(context);

        var input = accelerator.Allocate1D<int>(1024);
        var output = accelerator.Allocate1D<int>(1024);
        
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<int>>(Kernel);

        kernel(input.IntExtent, input.View, output.View);
        
        accelerator.Synchronize();
    }
}