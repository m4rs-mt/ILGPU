## ArrayViews and VariableViews

The `ArrayView` and `VariableView` structures have been adapted to the C# 'ref' features.
This renders explicit `Load` and `Store` methods obsolete.
In addition, all methods that accept `VariableView<X>` parameter types have been adapted to the parameter types `ref X`.
This applies, for example, to all methods of the class `Atomic`.
```c#
class ...
{
    static void ...(...)
    {
        // Old way (obsolete and no longer supported)
        ArrayView<int> someView = ...
        var variableView = someView.GetVariableView(X);
        Atomic.Add(variableView);
        ...
        variableView.Store(42);

        // New way
        ArrayView<int> someView = ...
        Atomic.Add(ref someView[X]);
        ...
        someView[X] = 42;

        // or
        ref var variable = ref someView[X];
        variable = 42;

        // or
        var variableView = someView.GetVariableView(X);
        variableView.Value = 42;
    }
}
```

## Shared Memory

The general concept of shared memory has been redesigned.
The previous model required `SharedMemoryAttribute` attributes on specific parameters that should be allocated in shared memory.
The new model uses the static class `SharedMemory` to allocate this kind of memory procedurally in the scope of kernels.
This simplifies programming, kernel-delegate creation and enables non-kernel methods to allocate their own pool of shared memory.

Note that array lengths must be constants in this ILGPU version.
Hence, a dynamic allocation of shared memory is currently not supported.

The kernel loader methods `LoadSharedMemoryKernelX` and `LoadSharedMemoryStreamKernelX` have been removed.
They are no longer required, since a kernel does not have to declare its shared memory allocations in the form of additional parameters.

```c#
class ...
{
    static void SharedMemoryKernel(GroupedIndex index, ...)
    {
        // Allocate an array of 32 integers
        ArrayView<int> sharedMemoryArray = SharedMemory.Allocate<int>(32);

        // Allocate a single variable of type long in shared memory
        ref long sharedMemoryVariable = ref SharedMemory.Allocate<long>();

        ...
    }
}
```

## CPU Debugging

Starting a kernel in debug mode is a common task that developers go through many times a day.
Although ILGPU has been optimized for performance, you may not wait a few milliseconds every time you start your program to debug a kernel on the CPU.
For this reason, the context flag `ContextFlags.SkipCPUCodeGeneration` has been added.
It suppresses IR code generation for CPU kernels and uses the .Net runtime directly.
*Warning: This avoids general kernel analysis/verification checks. It should only be used by experienced users.*

## Internals

The old LLVM-based concept of `CompileUnit` objects is obsolete and has been replaced by a completely new IR.
The new IR leverages `IRContext` objects to manage IR objects that are derived from the class `ILGPU.IR.Node`.
Unlike previous versions, an `IRContext` is not tied to a specific `Backend` instance and can be reused accross different hardware architectures.

The global optimization process can be controlled with the enumeration `OptimizationLevel`.
This level can be specified by passing the desired level to the `ILGPU.Context` constructor.
If the optimization level is not explicitly specified, the level is determined by the current build mode (either `Debug` or `Release`).
