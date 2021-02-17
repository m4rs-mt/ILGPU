## TLDR - Quick Start

Create a new ILGPU `Context` instance that initializes ILGPU.
Create `Accelerator` instances that target specific hardware devices.
Compile and load the desired kernels and launch them with allocated chunks of memory.
Retrieve the data and you're done 😄

Refer to the related <a href="https://github.com/m4rs-mt/ILGPU.Samples/blob/master/Src/SimpleKernel" target="_blank">ILGPU sample</a> for additional insights.

```c#
class ...
{
    static void MyKernel(
        Index1 index, // The global thread index (1D in this case)
        ArrayView<int> dataView, // A view to a chunk of memory (1D in this case)
        int constant) // A sample uniform constant
    {
        dataView[index] = index + constant;
    }

    public static void Main(string[] args)
    {
        // Create the required ILGPU context
        using var context = new Context();
        using var accelerator = new CudaAccelerator(context);

        // accelerator.LoadAutoGroupedStreamKernel creates a typed launcher
        // that implicitly uses the default accelerator stream.
        // In order to create a launcher that receives a custom accelerator stream
        // use: accelerator.LoadAutoGroupedKernel<Index1, ArrayView<int> int>(...)
        var myKernel = accelerator.LoadAutoGroupedStreamKernel<
            Index1,
            ArrayView<int>,
            int>(MyKernel);

        // Allocate some memory
        using var buffer = accelerator.Allocate<int>(1024);
        // Launch buffer.Length many threads and pass a view to buffer
        myKernel(buffer.Length, buffer.View, 42);

        // Wait for the kernel to finish...
        accelerator.Synchronize();

        // Resolve data
        var data = buffer.GetAsArray();
        // ...
    }
}
```