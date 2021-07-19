# Tutorial 03 Kernels and Simple Programs.
In this tutorial we actually do work on the GPU! 

## Lets start with a sample.
I think the easiest way to explain this is taking the simplest example I can think of and decomposing it. 

This is a modified version of the sample from Primer 01.
```C#
using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace Tutorial
{
    class Program
    {
        static void Kernel(Index1 i, ArrayView<int> data, ArrayView<int> output)
        {
            output[i] = data[i % data.Length];
        }

        static void Main()
        {
            Context context = new Context();
            Accelerator accelerator = accelerator = new CPUAccelerator(context);

            MemoryBuffer<int> deviceData = accelerator.Allocate(new int[] { 0, 1, 2, 4, 5, 6, 7, 8, 9 });
            MemoryBuffer<int> deviceOutput = accelerator.Allocate<int>(10_000);

            Action<Index1, ArrayView<int>, ArrayView<int>> loadedKernel = accelerator.LoadAutoGroupedStreamKernel<Index1, ArrayView<int>, ArrayView<int>>(Kernel);
            
            loadedKernel(deviceOutput.Length, deviceData, deviceOutput);
            accelerator.Synchronize();

            int[] hostOutput = deviceOutput.GetAsArray();

            deviceData.Dispose();
            deviceOutput.Dispose();
            accelerator.Dispose();
            context.Dispose();
        }
    }
}
```

## The following parts already have detailed explainations in other tutorials:

#### [Context and an accelerator.](Tutorial_01.md)
```C#
Context context = new Context();
Accelerator accelerator = accelerator = new CPUAccelerator(context);
```
Creates a CPUAccelerator, if you are feeling frisky you can try a CUDA or OpenCL accelerator, or replace this with the example code from Tutorial 01 try to pick the optimal one automatically.

#### [Some kind of data and output device memory](Tutorial_02.md)
```C#
MemoryBuffer<int> deviceData = accelerator.Allocate(new int[] { 0, 1, 2, 4, 5, 6, 7, 8, 9 });
MemoryBuffer<int> deviceOutput = accelerator.Allocate<int>(10_000);
```

Loads some example data into the device memory.

```C#
int[] hostOutput = deviceOutput.GetAsArray();
```

After we run the kernel we need to get the data as host memory to use it in CPU code.

## This leaves just few parts that need further explaination.
Ok now we get to the juicy bits.

#### The kernel function definition.
```C#
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

* no classes
* no references
* no structs with dynamic sizes
* I think it needs to be static TODO CHECK

The first parameter in a kernel must be its index. A kernel always iterates over some extent, which
is some 1, 2 or 3 dimensional length. Most of the time this is the length of the output MemoryBuffer.
When you call the kernel this is what you will use, but inside the kernel function the index is the 
threadIndex for the kernel.

The other parameters can be structs or ArrayViews. You can have TODO N parmeters in total. If you 
are approching this limit consider packing things into structs.

The function is whatever your algorithm needs. Be very careful of race conditions.

Your code structure will greatly affect performance. This is another complex topic but in general 
try to avoid branches and code that would change in different kernel indices. The thing you are trying 
to avoid is threads that are running different instructions, this is called divergence.

#### The loaded instance of a kernel.
```C#
Action<Index1, ArrayView<int>, ArrayView<int>> loadedKernel = accelerator.LoadAutoGroupedStreamKernel<Index1, ArrayView<int>, ArrayView<int>>(Kernel);
```
This is where you actually compile the code and load it into the accelerator memory. It returns 
an action with the same parameters as the kernel. 

When you compile your C# project you compile all the code into IL. This is a version of your code
that is optimized to be run by the dotnet runtime. ILGPU takes this IL and compiles it to a version
that your accelerator can run. This step is done at runtime whenever you load a kernel or if you 
explicitly compile it.

If you are having issues compiling code try testing with the CPUAccelerator.

#### The actual kernel call and device synchronize.
```C#
loadedKernel(deviceOutput.Length, deviceData, deviceOutput);
accelerator.Synchronize();
```
This is the step that does the actual work! 

Remember that the index parameter is actually the extent of the kernel when you call it,
but in the actual kernel it is the index.

Kernel calls are asynchronous but happen in the order that they are called. So if you 
call kernel A then kernel B you are guaranteed that A is done before B is started. 

Then when you call accelerator.Synchronize(); your current thread will wait until
the accelerator is finished executing your kernels. If you are using streams you may want
to just synchronize the stream the kernels are running on, but we will talk about that 
in another tutorial.