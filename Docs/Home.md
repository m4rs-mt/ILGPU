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

## Overview
* [Home](Home)
* [Getting Started](Getting-Started)
* [Accelerators & Streams](Accelerators-and-Streams)
* [Memory Buffers & Views](Memory-Buffers-and-Views)
* [Kernels](Kernels)
* [Shared Memory](Shared-Memory)
* [Math Functions](Math-Functions)

## Advanced
* [Dynamically Specialized Kernels](Dynamically-Specialized-Kernels)
* [Debugging & Profiling](Debugging-and-Profiling)
* [Inside ILGPU](Inside-ILGPU)

## Upgrade Guides
* [v0.8.X to v0.9.X](Upgrade-v0.8.X-to-v0.9.X)
* [v0.8.0 to v0.8.1](Upgrade-v0.8.0-to-v0.8.1)
* [v0.7.X to v0.8.X](Upgrade-v0.7.X-to-v0.8.X)
* [v0.6.X to v0.7.X](Upgrade-v0.6.X-to-v0.7.X)
* [v0.3.X to v0.5.X](Upgrade-v0.3.X-to-v0.5.X)
* [v0.1.X to v0.2.X](Upgrade-v0.1.X-to-v0.2.X)