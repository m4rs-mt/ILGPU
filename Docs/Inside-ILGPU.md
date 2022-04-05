---
layout: wiki
---

## Optimizations and Compile Time

ILGPU features a modern parallel processing, transformation and compilation model.
It allows parallel code generation and transformation phases to reduce compile time and improve overall performance.

However, parallel code generation in the frontend module is disabled by default.
It can be enabled via the enumeration flag `ContextFlags.EnableParallelCodeGenerationInFrontend`.

The global optimization process can be controlled with the enumeration `OptimizationLevel`.
This level can be specified by passing the desired level to the `ILGPU.Context` constructor.
If the optimization level is not explicitly specified, the level is automatically set to `OptimizationLevel.O1`.

The `OptimizationLevel.O2` level uses additional transformations that increase compile time but yield potentially better GPU code.
For best performance, it is recommended using this mode in `Release` builds only.

## Internal Caches

ILGPU uses a set of internal caches to speed up the compilation process.
The `KernelCache` is based on `WeakReference`s and its own GC thread to avoid memory leaks.
As soon as a kernel is disposed by the .Net GC, the ILGPU GC thread can release the associated data structures.
Although each Accelerator instance is assigned a `MemoryBufferCache` instance, ILGPU does not use this cache anywhere.
It was added to help users write custom accelerator extensions that require temporary memory.
If you do not use the corresponding `MemoryBufferCache`s, you should not get into trouble regarding caching.

Use `Context.ClearCache(ClearCacheMode.Everything)` to clear all internal caches to recover allocated memory.
In addition, each accelerator has its own cache for type information and kernel arguments.
Use `Accelerator.ClearCache(ClearCacheMode.Everything)` to clear the cache on the desired accelerator.
**Note that clearing the caches is not thread-safe in general; you have to ensure** that there are **no running background threads** trying to compile/load/allocate ILGPU related objects while clearing the caches.

## Backends

A `Backend` represents target-specific code-generation functionality for a specific target device.
It can be used to manually compile kernels for a specific platform.

Note that **you do not have to create custom backend instances** on your own when using the ILGPU runtime.
Accelerators already carry associated and configured backends that are used for high-level kernel loading.

```c#
class ...
{
    static void Main(string[] args)
    {
        using (var context = new Context())
        {
            // Creats a user-defined MSIL backend for .Net code generation
            using (var cpuBackend = new DefaultILBackend(context))
            {
                // Use custom backend
            }

            // Creates a user-defined backend for NVIDIA GPUs using compute capability 5.0
            using (var ptxBackend = new PTXBackend(
                context,
                PTXArchitecture.SM_50,
                TargetPlatform.X64))
            {
                // Use custom backend
            }
        }
    }
}
```

## IRContext

An `IRContext` manages and caches intermediate-representation (IR) code, which can be reused during the compilation process.
It can be created using a general ILGPU `Context` instance.
An `IRContext` is not tied to a specific `Backend` instance and can be reused across different hardware architectures.

Note that the main ILGPU `Context` already has an associated `IRContext` that is used for all high-level kernel-loading functions.
Consequently, users are not required to manage their own contexts in general.

```c#
class ...
{
    static void Main(string[] args)
    {
        var context = new Context();

        var irContext = new IRContext(context);
        // ...
    }
}
```

## Compiling Kernels

Kernels can be compiled manually by requesting a code-generation operation from the backend yielding a `CompiledKernel` object.
The resulting kernel object can be loaded by an `Accelerator` instance from the runtime system.
Alternatively, you can cast a `CompiledKernel` object to its appropriate backend-specific counterpart method in order to access the generated and target-specific assembly code.

*Note that the default MSIL backend does not provide additional insights, since the `ILBackend` does not require custom assembly code.*

We recommend that you use the [high-level kernel-loading concepts of ILGPU](ILGPU-Kernels) instead of the low-level interface.

```c#
class ...
{
    public static void MyKernel(Index index, ...)
    {
        // ...
    }

    static void Main(string[] args)
    {
        using var context = new Context();
        using var b = new PTXBackend(context, ...);
        // Compile kernel using no specific KernelSpecialization settings
        var compiledKernel = b.Compile(
            typeof(...).GetMethod(nameof(MyKernel), BindingFlags.Public | BindingFlags.Static),
            default);

        // Cast kernel to backend-specific PTXCompiledKernel to access the PTX assembly
        var ptxKernel = compiledKernel as PTXCompiledKernel;
        System.IO.File.WriteAllBytes("MyKernel.ptx", ptxKernel.PTXAssembly);
    }
}
```

## Loading Compiled Kernels

Compiled kernels have to be loaded by an accelerator first before they can be executed.
See the [ILGPU low-level kernel sample](https://github.com/m4rs-mt/ILGPU.Samples/tree/master/Src/LowLevelKernelCompilation) for details.
**Note: manually loaded kernels should be disposed manually to have full control over the lifetime of the kernel function in driver memory. You can also rely on the .Net GC to dispose kernels in the background.**

An accelerator object offers different functions to load and configure kernels:
* `LoadAutoGroupedKernel`
   Loads an implicitly grouped kernel with an automatically determined group size
* `LoadImplicitlyGroupedKernel`
   Loads an implicitly grouped kernel with a custom group size
* `LoadKernel`
   Loads explicitly and implicitly grouped kernels. However, implicitly grouped kernels will be launched with a group size that is equal to the warp size

```c#
class ...
{
    static void Main(string[] args)
    {
        ...
        var compiledKernel = backend.Compile(...);

        // Load implicitly grouped kernel with an automatically determined group size
        var k1 = accelerator.LoadAutoGroupedKernel(compiledKernel);

        // Load implicitly grouped kernel with custom group size
        var k2 = accelerator.LoadImplicitlyGroupedKernel(compiledKernel);

        // Load any kernel (explicitly and implicitly grouped kernels).
        // However, implicitly grouped kernels will be dispatched with a group size
        // that is equal to the warp size of its associated accelerator
        var k3 = accelerator.LoadKernel(compiledKernel);

        ...

        k1.Dispose();
        k2.Dispose();
        // Leave K3 to the GC
        // ...
    }
}
```

## Direct Kernel Launching

A loaded kernel can be dispatched using the `Launch` method.
However, the dispatch method takes an object-array as an argument, all arguments are boxed upon invocation and there is not type-safety at this point.
For performance reasons, we strongly recommend the use of typed kernel launchers that avoid boxing.

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

        // Load a sample kernel MyKernel
        var compiledKernel = backend.Compile(...);
        using (var k = accelerator.LoadAutoGroupedKernel(compiledKernel))
        {
            k.Launch(buffer.Extent, buffer.View, 1);

            ...

            accelerator.Synchronize();
        }

        ...
    }
}
```

## Typed Kernel Launchers

Kernel launchers are delegates that provide an alternative to direct kernel invocations.
These launchers are specialized methods that are dynamically generated and specialized for every kernel.
They avoid boxing and realize [high-performance kernel dispatching](https://github.com/m4rs-mt/ILGPU.Samples/blob/master/Src/SimpleKernelDelegate).
You can create a custom kernel launcher using the `CreateLauncherDelegate` method.
It Creates a specialized launcher for the associated kernel.
Besides all required kernel parameters, it also receives a parameter of type `AcceleratorStream` as an argument.

Note that high-level API kernel loading functionality that simply returns a launcher delegate instead of a kernel object.
These loading methods work similarly to the these versions, e.g. `LoadAutoGroupedStreamKernel` loads a kernel with a custom delegate type that is linked to the default accelerator stream.

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

        // Load a sample kernel MyKernel
        var compiledKernel = backend.Compile(...);
        using (var k = accelerator.LoadAutoGroupedKernel(compiledKernel))
        {
            var launcherWithCustomAcceleratorStream =
                k.CreateLauncherDelegate<AcceleratorStream, Index, ArrayView<int>>();
            launcherWithCustomAcceleratorStream(someStream, buffer.Extent, buffer.View, 1);

            ...
        }

        ...
    }
}
```