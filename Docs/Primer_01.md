# Primer 01: Code
This page will provide a quick rundown the basics of how kernels (think GPU programs) run.
If you are already familiar with CUDA or OpenCL programs you can probably skip this.

To steal a quote from a very good [talk](https://www.youtube.com/watch?v=uvVy3CqpVbM) that you should *probably* watch.

> You **cannot** program the GPU like its a CPU
> You must pay attention to 3 things
> 1. Memory Access
>
> 2. Data Locality
>
> 3. Threading  

## A GPU is not a CPU
If you will allow a little bit of **massive oversimplification**, this is pretty easy to understand.

### How does a CPU work?

A traditional processor has a very simple cycle: fetch, decode, execute. 

It grabs an instruction from memory (the fetch), figures out how to perform said instruction (the decode), 
and does the instruction (the execute). This cycle then repeats for all the instructions in your algorithm.
Executing this linear stream of instructions is fine for most programs because CPUs are super fast, and most
algorithms are serial.

What happens when you have an algorithm that can be processed in parallel? A CPU has multiple cores, each
doing its own fetch, decode, execute. You can spread the algorithm across all the cores on the CPU, but 
in the end each core will still be running a stream of instructions, likely the *same* stream of instructions,
but with *different* data.

GPUs and CPUs both try to exploit this fact, but use very two different methods.

##### CPU | SIMD: Single Instruction Multiple Data.
CPUs have a trick for parallel programs called SIMD. These are a set of instructions
that allow you to have one instuction do operations on multiple peices of data at once.

Lets say a CPU has an add instruction: 
> ADD RegA RegB

Which would perform
> RegA = RegB + RegA

The SIMD version would be:
> ADD RegABCD RegEFGH

Which would perform
> RegA = RegE + RegA
> 
> RegB = RegF + RegB
> 
> RegC = RegG + RegC
>
> RegD = RegH + RegD

All at once.

A clever programmer can take these instructions and get a 3x-8x performance improvement
in very math heavy scenarios.

##### GPU | SIMT: Single Instruction Multiple Threads.
GPUs have SIMT. SIMT is the same idea as SIMD but instead of just doing the math instructions
 in parallel why not do **all** the instructions in parallel. 

The GPU assumes all the instructions you are going to fetch and decode for 32 threads are 
the same, it does 1 fetch and decode to setup 32 execute steps, then it does all 32 execute 
steps at once. This allows you to get 32 way multithreading per single core, if and only 
if all 32 threads want to do the same instruction. 

### Kernels
With this knowledge we can now talk about kernels. Kernels are just GPU programs, but because
 a GPU program is not a single thread but many it works a little different. 

When I was first learning about kernels I had an observation that made kernels kinda *click*
in my head. 

Kernels and Parallel.For have the same usage pattern. 

If you don't know about Parallel.For it is a function that provides a really easy way to run
 code on every core of the CPU. All you do is pass in the start index, an end index, and a function
that takes an index. Then the function is called from some thread with an index. There are no guarentees
about what core an index is run on, or what order the threads are run, but you get a **very** simple
interface for running parallel functions.
```C#
using System;
using System.Threading.Tasks;

public static class Program
{
    static void Main(string[] args)
    {
        //Load the data
        int[] data = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        int[] output = new int[10_000];
            
        //Load the action and execute
        Parallel.For(0, output.Length, 
        (int i) =>
        {
            output[i] = data[i % data.Length];
        });
    }
}
```
Running the same program as a kernel is **very** similar:
```C#
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

public static class Program
{
    static void Main()
    {
        // Initialize ILGPU.
        Context context = Context.CreateDefault();
        Accelerator accelerator = context.CreateCPUAccelerator(0);

        // Load the data.
        var deviceData = accelerator.Allocate1D(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        var deviceOutput = accelerator.Allocate1D<int>(10_000);

        // load / compile the kernel
        var loadedKernel = accelerator.LoadAutoGroupedStreamKernel(
        (Index1D i, ArrayView<int> data, ArrayView<int> output) =>
        {
            output[i] = data[i % data.Length];
        });

        // tell the accelerator to start computing the kernel
        loadedKernel((int)deviceOutput.Length, deviceData.View, deviceOutput.View);

        // wait for the accelerator to be finished with whatever it's doing
        // in this case it just waits for the kernel to finish.
        accelerator.Synchronize();

        accelerator.Dispose();
        context.Dispose();
    }
}
```
You do not need to understand what is going on in the kernel example to see that the Parallel.For code uses the same
API. The major differences are due to how memory is handled.

Parallel.For and Kernels both have the same potential for race conditions, and for each you must take care to prevent them.