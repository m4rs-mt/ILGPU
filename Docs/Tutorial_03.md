---
layout: wiki
---

# Tutorial 03 Kernels and Simple Programs.
In this tutorial we actually do work on the GPU! 

## Lets start with an example.
I think the easiest way to explain this is taking the simplest example I can think of and decomposing it. 

This is a modified version of the sample from Primer 01.
```c#
using ILGPU;
using ILGPU.Runtime;
using System;

public static class Program
{
    static void Kernel(Index1D i, ArrayView<int> data, ArrayView<int> output)
    {
        output[i] = data[i % data.Length];
    }

    static void Main()
    {
        // Initialize ILGPU.
        Context context = Context.CreateDefault();
        Accelerator accelerator = context.GetPreferredDevice(preferCPU: false)
                                  .CreateAccelerator(context);

        // Load the data.
        MemoryBuffer1D<int, Stride1D.Dense> deviceData = accelerator.Allocate1D(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        MemoryBuffer1D<int, Stride1D.Dense> deviceOutput = accelerator.Allocate1D<int>(10_000);

        // load / precompile the kernel
        Action<Index1D, ArrayView<int>, ArrayView<int>> loadedKernel = 
            accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<int>>(Kernel);

        // finish compiling and tell the accelerator to start computing the kernel
        loadedKernel((int)deviceOutput.Length, deviceData.View, deviceOutput.View);

        // wait for the accelerator to be finished with whatever it's doing
        // in this case it just waits for the kernel to finish.
        accelerator.Synchronize();

        // moved output data from the GPU to the CPU for output to console
        int[] hostOutput = deviceOutput.GetAsArray1D();

        for(int i = 0; i < 50; i++)
        {
            Console.Write(hostOutput[i]);
            Console.Write(" ");
        }

        accelerator.Dispose();
        context.Dispose();
    }
}
```

## The following parts already have detailed explainations in other tutorials:

#### [Context and an accelerator.](Tutorial_01.md)
```c#
Context context = Context.CreateDefault();
Accelerator accelerator = context.GetPreferredDevice(preferCPU: false)
                            .CreateAccelerator(context);
```
Creates an Accelerator using GetPreferredDevice to hopefully get the "best" device.

#### [Some kind of data and output device memory](Tutorial_02.md)
```c#
MemoryBuffer1D<int, Stride1D.Dense> deviceData = accelerator.Allocate1D(new int[] { 0, 1, 2, 4, 5, 6, 7, 8, 9 });
MemoryBuffer1D<int, Stride1D.Dense> deviceOutput = accelerator.Allocate1D<int>(10_000);
```

Loads some example data into the device memory, using dense striding.

```c#
int[] hostOutput = deviceOutput.GetAsArray1D();
```

After we run the kernel we need to get the data as host memory to use it in CPU code.

## This leaves just few parts that need further explaination.
Ok now we get to the juicy bits.

#### The kernel function definition.
```c#
static void Kernel(Index1 i, ArrayView<int> data, ArrayView<int> output)
{
    output[i] = data[i % data.Length];
}
```
Kernels have a few limitations, but basically anything simple works like you would expect.
Primatives and like math operations all work with no issues and as shown above ArrayViews 
take the place of arrays.

The main limitation comes down to memory. You can only allocate and pass non-nullable value 
types that have a set size. Structs that have arrays can cause issues but more on this in 
the future struct tutorial. In general I have had little issue working around this. Most 
of the change is in how data is stored. As for my classes it was not to hard to change 
over to using structs. Anyways, I am digressing there could be a whole series of tutorials
to cover this in detail.

In general:

* no classes (This may change in an upcoming version of ILGPU)
* no references
* no structs with dynamic sizes

The first parameter in a kernel must be its index. A kernel always iterates over some extent, which
is some 1, 2 or 3 dimensional length. Most of the time this is the length of the output MemoryBuffer<sup>0</sup>.
When you call the kernel this is what you will use, but inside the kernel function the index is the 
threadIndex for the kernel.

The other parameters can be structs or ArrayViews. You can have I *think* 19 parmeters in total. If you 
are approching this limit consider packing things into structs. Honestly before 19 parmeters you should pack things
into structs just to keep it organized. 

The function is whatever your algorithm needs. Be very careful of race conditions, and remember that the kernel is the *inside* of a for loop,
not the for loop itself.

Your code structure will greatly affect performance. This is another complex topic but in general 
try to avoid branches<sup>1</sup> and code that would change in different kernel indices. The thing you are trying 
to avoid is threads that are running different instructions, this is called divergence.

#### The loaded instance of a kernel.
```c#
Action<Index1D, ArrayView<int>, ArrayView<int>> loadedKernel = 
    accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<int>>(Kernel);
```
This is where you precompile the code. It returns an action with the same parameters as the kernel. 

When you compile your C# project you compile all the code into IL. This is a version of your code
that is optimized to be run by the dotnet runtime. ILGPU takes this IL and compiles it to a version
that your accelerator can run. This step is done at runtime whenever you load a kernel or if you 
explicitly compile it.

If you are having issues compiling code try testing with the CPUAccelerator.

#### The actual kernel call and device synchronize.
```c#
loadedKernel((int)deviceOutput.Length, deviceData.View, deviceOutput.View);
accelerator.Synchronize();
```
This is the step that does the actual work! 

The first step is for ILGPU to finish compiling the kernel, this only happens the first time
the kernel is called, or any time a SpecializedValue<> parameter is changed.

Remember that the index parameter is actually the extent of the kernel when you call it,
but in the actual kernel function it is the index.

Kernel calls are asynchronous. When you call them they are added to a work queue that is controlled by the stream.
So if you call kernel A then kernel B you are guaranteed that A is done before B is started, provided you call them
from the same stream. 

Then when you call accelerator.Synchronize(); or stream.Synchronize(); your current thread will wait until
the accelerator (all the steams), or the stream in the case of stream.Synchronize(); is finished executing your kernels.

See Also:

[Simple Kernel Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/SimpleKernel) 

[Simple Math Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/SimpleMath)

> <sup>0</sup>
> While it is easiest to group kernels based on the extent of the output buffer
> it is likely *faster* to group them based on the hardware that is running the kernel.
> For example there is this method of eaking out the most performance from the GPU called a 
> [Grid Stride Loop](https://developer.nvidia.com/blog/cuda-pro-tip-write-flexible-kernels-grid-stride-loops/),
> it can help more efficently use the limited memory bandwidth, as well as more efficently use all the warps.

> <sup>1</sup>
> This is general advice that everyone gives for programming now, and I take a bit of issue with it. Branches are NOT slow.
> For the CPU branches that are unpredictable are slow, and for the GPU branches that are divergent across the same warp are slow.
> Figuring out if that is the case is hard, which is why the general advice is avoid branches. [Matt Godbolt ran into this issue and describes it well in this talk](https://youtu.be/HG6c4Kwbv4I?t=2532)