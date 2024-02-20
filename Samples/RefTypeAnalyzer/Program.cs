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

    static int AnotherFunction()
    {
        return new MyRefType().Hello;
    }

    // TODO: tests needed: normal, using stuff from the class context, arrays, arrays of
    // ref types, constructor calls, method/function calls
    static void Kernel(Index1D index, ArrayView<int> input, ArrayView<int> output)
    {
        // This is disallowed, since MyRefType is a reference type
        var refType = new MyRefType();
        output[index] = input[index] + refType.Hello;

        // Allocating arrays of unmanaged types is fine
        MyUnmanagedType[] array = [new MyUnmanagedType()];
        int[] ints = [0, 1, 2];

        // But arrays of reference types are still disallowed
        MyRefType[] refs = [new MyRefType()];

        // Any functions that may be called are also analyzed
        int result = AnotherFunction();
    }

    static void Main(string[] args)
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
