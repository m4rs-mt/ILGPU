---
layout: wiki
---

## General Information

Kernels are `static` functions that can work on value types and can invoke other functions that work on value types.
`class` (reference) types are currently **not supported** in the scope of kernels.
Note that exception handling, boxing and recursive programs are also **not supported** and will be rejected by the ILGPU compiler.

The type of the first parameter must always be a supported index type in the case of implicitly grouped kernels (see below).
In the case of explicitly grouped kernels, the first parameter is manually defined by the programmer (see below)
All other parameters are always uniform constants that are passed from the CPU to the GPU via constant memory (in the parameter address space).
All parameter types must be value types and must not be passed by reference (e.g. via `out` or `ref` keywords in C#).

Memory allocation is performed via so called `memory buffers`, which are classes that are allocated and disposed on the CPU.
Since they cannot be passed directly to kernels, you can pass `views` (`Span<T>` like data structures) to these buffers by value as kernel arguments.

Note that you must not pass pointers to non-accessible memory regions since these are also passed by value and cannot be marshalled automatically by ILGPU when launching kernels.

## Implicitly Grouped Kernels

Implicitly grouped kernels allow very convenient high-level kernel programming.
They can be launched with automatically configured group sizes (that are determined by ILGPU) or manually defined group sizes.

Such kernels **must not use shared memory, group or warp functionality** since there is no guaranteed group size or thread participation inside a warp/group.
The details of the kernel invocation are hidden from the programmer and managed by ILGPU.
There is no way to access or manipulate the low-level peculiarities from the user's point of view.
Use explicitly grouped kernels for full control over GPU-kernel dispatching.

```c#
class ...
{
    static void ImplicitlyGrouped_Kernel(
        [Index|Index2|Index3] index,
        [Kernel Parameters]...)
    {
        // Kernel code

        // Use the index parameter to access the global index of i-th thread in the global thread grid
    }
}
```

## Explicitly Grouped Kernels

Explicitly grouped kernels offer the full kernel-programming power and behave similarly to Cuda and OpenCL kernels.
These kernels do not receive an index type as first parameter.
Instead, you can use Grid and Group properties to resolve the indices you are interested in.
Moreover, these kernel offer access to shared memory, Group  and other Warp-specific intrinsics.
However, the kernel-dispatch dimensions have to be managed manually.

```c#
class ...
{
    static void ExplicitlyGrouped_Kernel(
        [Kernel Parameters]...)
    {
        var globalIndex = Grid.GlobalIndex.X;
        // or
        var globalIndex = Group.IdxX + Grid.IdxX * Group.DimX;

        // Kernel code
    }
}
```

## Loading and Launching Kernels

Kernels have to be loaded by an accelerator first before they can be executed.
See the [ILGPU kernel sample](https://github.com/m4rs-mt/ILGPU.Samples/tree/master/Src/SimpleKernel) for details.
There are two possibilities in general: using the high-level (described here) and the [low-level loading API](Inside-ILGPU).
We strongly recommend to use the high-level API that simplifies programming, is less error prone and features automatic kernel caching and disposal.

An accelerator object offers different functions to load and configure kernels:
* `LoadAutoGroupedStreamKernel`
   Loads an implicitly grouped kernel with an automatically determined group size (uses a the default accelerator stream)
* `LoadAutoGroupedKernel`
   Loads an implicitly grouped kernel with an automatically determined group size (requires an accelerator stream)
* `LoadImplicitlyGroupedStreamKernel`
   Loads an implicitly grouped kernel with a custom group size (uses the default accelerator stream)
*  `LoadImplicitlyGroupedKernel`
   Loads an implicitly grouped kernel with a custom group size (requires an accelerator stream)
* `LoadStreamKernel`
   Loads explicitly and implicitly grouped kernels. However, implicitly grouped kernels will be launched with a group size that is equal to the warp size (uses the default accelerator stream)
* `LoadKernel`
  Loads explicitly and implicitly grouped kernels. However, implicitly grouped kernels will be launched with a group size that is equal to the warp size (requires an accelerator stream)

Functions following the naming pattern `LoadXXXStreamKernel` use the default accelerator stream for all operations.
If you want to specify the associated accelerator stream, you will have to use the `LoadXXXKernel` functions.

Each function returns a typed delegate (a kernel launcher) that can be called in order to invoke the actual kernel execution.
These launchers are specialized methods that are dynamically generated and specialized for every kernel.
They avoid boxing and realize high-performance kernel dispatching.
In contrast to older versions of ILGPU, all kernels loaded with these functions will be managed by their associated accelerator instances.

```c#
class ...
{
    static void MyKernel(Index index, ArrayView<int> data, int c)
    {
        data[index] = index + c;
    }

    static void Main(string[] args)
    {
        ...
        var buffer = accelerator.Allocate<int>(1024);

         // Load a sample kernel MyKernel using one of the available overloads
        var kernelWithDefaultStream = accelerator.LoadAutoGroupedStreamKernel<
                     Index, ArrayView<int>, int>(MyKernel);
        kernelWithDefaultStream(buffer.Extent, buffer.View, 1);

         // Load a sample kernel MyKernel using one of the available overloads
        var kernelWithStream = accelerator.LoadAutoGroupedKernel<
                     Index, ArrayView<int>, int>(MyKernel);
        kernelWithStream(someStream, buffer.Extent, buffer.View, 1);

        ...
    }
}
```

Note that a kernel-loading operation will trigger a kernel compilation in the case of an uncached kernel.
The compilation step will happen in the background and is transparent for the user.
However, if you require custom control over the low-level kernel-compilation process refer to [Advanced Low-Level Functionality](Inside-ILGPU).

## Immediate Launching of Kernels

Starting with version [v0.10.0](https://github.com/m4rs-mt/ILGPU/releases/tag/v0.10.0), ILGPU offers the ability to immediately compile and launch kernels via the accelerator methods (similar to those provided by other frameworks).
ILGPU exposes direct `Launch` and `LaunchAutoGrouped` methods via the `Accelerator` class using a new strong-reference based kernel cache.
This cache is used for the new launch methods only and can be disabled via the flag `ContextFlags.DisableKernelLaunchCaching`.

```c#
class ...
{
    static void MyKernel(...)
    {

    }

    static void MyImplicitKernel(Index1 index, ...)
    {

    }

    static void Main(string[] args)
    {
        // ...

        // Launch explicitly grouped MyKernel using the default stream
        accl.Launch(MyKernel, < MyKernelConfig >, ...);

        // Launch explicitly grouped MyKernel using the given stream
        accl.Launch(stream, MyKernel, < MyKernelConfig >, ...);

        // Launch implicitly grouped MyKernel using the default stream
        accl.LaunchAutoGrouped(MyImplicitKernel, new Index1(...), ...);

        // Launch implicitly grouped MyKernel using the given stream
        accl.LaunchAutoGrouped(stream, MyImplicitKernel, new Index1(...), ...);
    }
}
```

## Retrieving Information about Loaded Kernels

You can get the underlying `CompiledKernel` instance (see [Inside ILGPU](Inside-ILGPU)) of a kernel launcher instance via:
```c#
var compiledKernel = launcher.GetCompiledKernel();
```
This allows users to access accelerator-specific properties of particular kernel.
For instance, you can access the PTX assembly code of a Cuda kernel by casting the `CompiledKernel` instance into a `PTXCompiledKernel` and access its `PTXAssembly` property, as shown below.
```c#
var ptxKernel = launcher.GetCompiledKernel() as PTXCompiledKernel;
System.IO.File.WriteAllText("Kernel.ptx", ptxKernel.PTXAssembly);
```

You can specify the context flag `ContextFlags.EnableKernelStatistics` to query additional information about compiled kernels.
This includes local functions and consumed local and shared memory.
After enabling the flag, you can get the information from a compiled kernel launcher delegate instance via:
```c#
// Get kernel information from a kernel launcher instance
var information = launcher.GetKernelInfo();

// Dump all information to the stdout stream
information.DumpToConsole();
```