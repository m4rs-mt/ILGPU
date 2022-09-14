## General Notes

A new OpenCL backend has been added that supports OpenCL C 2.0 (or higher) compatible GPUs.
The OpenCL backend does not require an OpenCL SDK to be installed/configured.
There is the possibility to query all supported OpenCL accelerators via `CLAccelerator.CLAccelerators`.
Since NVIDIA GPUs typically does not support OpenCL C 2.0 (or higher), they are usually not contained in this list.
However, if you still want to access those devices via the OpenCL API you can query `CLAccelerators.AllCLAccelerators`.
Note that the global list of all accelerators `Accelerator.Accelerators` will contain supported accelerators only.
It is highly recommended to use the `CudaAccelerator` class for NVIDIA GPUs and the `CLAccelerator` class for Intel and
AMD GPUs.
Furthermore, it is not necessary to worry about native library dependencies regarding OpenCL (except, of course, for the
actual GPU drivers).

The `XMath` class has been removed as it contained many software implementations for different platforms that are not
related to the actual ILGPU compiler.
However, there are several math functions that are supported on all platforms which are still exposed via the
new `IntrinsicMath` class.
There is also a class `IntrinsicMath.CPU` which contains implementations for all math functions for the `CPUAccelerator`
.
Please note that these functions are not supported on other accelerators except the `CPUAccelerator`.
If you want to use the full range of math functions refer to the `XMath` class of the `ILGPU.Algorithms` library.

The new version of the `ILGPU.Algorithms` library offers support for a set of commonly used algorithms (like Scan or
Reduce).
Moreover, it offers `GroupExtensions` and `WarpExtensions` to support group/warp-wide reductions or prefix sums within
kernels.

## New Algorithms Library

The new `ILGPU.Algorithms` library comes in a separate nuget package.
In order to use any of the exposed group/warp/math extensions you have to enable the library.
This setups all internal ILGPU hooks and custom code-generators to emit code that realizes the extensions in the right
places.
This is achieved by using the new extension and intrinsic API.

```c#
using ILGPU.Algorithms;
class ...
{
    static void ...(...)
    {
        using var context = new Context();

        // Enable all algorithms and extension methods
        context.EnableAlgorithms();

        ...
    }
}
```

## Math Functions

As mentioned <a href="#v07">here</a>, the `XMath` class has been removed from the actual GPU compiler framework.
Leverage the `IntrinsicMath` class to use math functions that are available on all supported accelerators.
If you want to access all math functions use the newly designed `XMath` class of the `ILGPU.Algorithms` library.

```c#
class ...
{
    static void ...(...)
    {
        // Old way (obsolete and no longer supported)
        float x = ILGPU.XMath.Sin(...);

        // New way
        // 1) Don't forget to enable algorithm support ;)
        context.EnableAlgorithms();
        
        // 2) Use the new XMath class
        float x = ILGPU.Algorithms.XMath.Sin(...);
    }
}
```

## Warp & Group Intrinsics

Previous versions of ILGPU had several warp-shuffle overloads to expose the native warp and group intrinsics to the
programmer.
However, these functions are typically available for `int` and `float` data types.
More complex or larger types required programming of custom `IShuffleOperation` interfaces that had to be passed to the
shuffle functions.
This inconvenient way of programming is no longer required.
The new warp and group intrinsics support generic data structures.
ILGPU will automatically generate the required code for every target platform and use case.

The intrinsics `Group.Broadcast` and `Warp.Broadcast` have been added.
In contrast to `Warp.Shuffle`, the `Warp.Broadcast` intrinsic requires that all participating threads read from the same
lane.
`Warp.Shuffle` supports different source lanes in every thread.
`Group.Broadcast` works like `Warp.Broadcast`, but for all threads in a group.

```c#
class ...
{
    static void ...(...)
    {
        ComplexDataType y = ...;
        ComplexDataType x = Warp.Shuffle(y, threadIdx);

        ...

        ComplexDataType y = ...;
        ComplexDataType x = Group.Broadcast(y, groupIdx);
    }
}
```

## Grid & Group Indices

It is no longer required to access grid and group indices via the `GroupedIndex(|2|3) index` parameter of a kernel.
Instead, you can access the static properties `Grid.Index(X|Y|Z)` and `Group.Index(X|Y|Z)` from every function in the
scope of a kernel.
This simplifies programming of helper methods significantly.
Furthermore, this also feels natural to experienced Cuda and OpenCL developers.

class ...
{
static void ...(GroupedIndex index)
{
// Common ILGPU way (still supported)
int gridIdx = index.GridIdx;
int groupIdx = index.GroupIdx;

        // New ILGPU way
        int gridIdx = Grid.IndexX;
        int groupIdx = Group.IndexX;
    }

}
