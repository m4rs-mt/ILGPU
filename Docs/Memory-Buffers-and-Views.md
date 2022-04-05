---
layout: wiki
---

## Memory Buffers

`MemoryBuffer` represent allocated memory regions (allocated arrays) of a given value type on specific accelerators.
Data can be copied to and from any accelerator using sync or async copy operations [using Streams](Streams).
ILGPU supports linear, 2D and 3D buffers out of the box, whereas nD-buffers can also be allocated and managed using custom index types.

Note that `MemoryBuffers` *should be* disposed manually and cannot be passed to kernels; only views to memory regions can be passed to kernels.
Should be refers to the fact that all memory buffers will be automatically released by either the `GC` or disposing the parent `Accelerator` instance in ILGPU. However, it is **highly recommended** to dispose buffer instances manually in order to have explicit and immediate control over all memory allocations on a GPU device.

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
        using var accelerator = ...;

        // Allocate a memory buffer on the current accelerator device.
        using (var buffer = accelerator.Allocat<int>(1024))
        {
            ...
        } // Dispose the buffer after performing all operations
    }
}
```

## Array Views

`ArrayViews` realize views to specific memory-buffer regions.
Views comprise pointers and length information.
They can be passed to kernels and simplify index computations.

Similar to memory buffers, there are specialized views for 1D, 2D and 3D scenarios.
However, it is also possible to use the generic structure `ArrayView<Type, IndexType>` to create views to nD-regions.

Accesses on `ArrayViews` are bounds-checked via `Debug` assertions.
Hence, these checks are not performed in `Release` mode, which benefits performance.
You can even enable bounds checks in `Release` builds by specifying the context flag `ContextFlags.EnableAssertions`.

```c#
class ...
{
    static void MyKernel(Index index, ArrayView<int> view1, ArrayView<float> view2)
    {
        ConvertToFloatSample(
            view1.GetSubView(0, view1.Length / 2),
            view2.GetSubView(0, view2.Length / 2));
    }

    static void ConvertToFloatSample(ArrayView<int> source, ArrayView<float> target)
    {
        for (Index i = 0, e = source.Extent; i < e; ++i)
            target[i] = source[i];
    }

    static void Main(string[] args)
    {
        ...
        using (var buffer = accelerator.Allocat&lt...&gt(...))
        {
            var mainView = buffer.View;
            var subView = mainView.GetSubView(0, 1024);
        }
    }
}
```

### Optimized 32-bit and 64-bit Memory Accesses
All addresses on a 64-bit GPU system will be represented using 64-bit addresses under the hood.
The only difference between the accesses is whether you use a 32-bit or a 64-bit offset.
ILGPU differentiates between both scenarios: it uses 32-bit integer math in the case of 32-bit offsets in your program and 64-bit integer math to compute the offsets in the 64-bit world. However, the actual address computation uses 64-bit integer math.

In the case of 32-bit offsets it uses ASM sequences like:
```asm
mul.wide.u32 %rd4, %r1, 4;
add.u64      %rd3, %rd1, %rd4;
```
where `r1` is the 32-bit offset computed in your kernel program, `4` is the constant size in bytes of your access (an integer in this case) and `rd1` is the source buffer address in your GPU memory in a 64-bit register. However, if the offset is a 64-bit integer, ILGPU uses an efficient multiply-add operation working on 64-bit integers like:
```asm
mad.lo.u64    %rd4, %rd3, 4, %rd1;
```

When accessing views using 32-bit indices, the resulting index operation will be performed on 32-bit offsets for performance reasons.
As a result, this operation can overflow when using a 2D 32-bit based `Index2`, for instance.
If you already know, that your offsets will not fit into a 32-bit integer, you have to use 64-bit offsets in your kernel.

If you rely on 64-bit offsets, the emitted indexing operating will be slightly more expensive in terms of register usage and computational overhead (at least conceptually). The actual runtime difference depends on your kernel program.

## Variable Views

A `VariableView` is a specialized array view that points to exactly one element.
`VariableViews` are useful since default 'C# ref' variables cannot be stored in structures, for instance.

```c#
class ...
{
    struct DataView
    {
        public VariableView<int> Variable;
    }

    static void MyKernel(Index index, DataView view)
    {
        // ...
    }

    static void Main(string[] args)
    {
        // ...
        using (var buffer = accelerator.Allocat<...>(...))
        {
            var mainView = buffer.View;
            var firstElementView = mainView.GetVariableView(0);
        }
    }
}
```