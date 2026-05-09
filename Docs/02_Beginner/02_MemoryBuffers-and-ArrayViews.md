# Tutorial 02 MemoryBuffers and ArrayViews

Welcome to the seccond ILGPU tutorial. In this tutorial we will cover the basics
of the Memory in ILGPU. In the best case, C# programmers will think of memory
in terms of stack and heap objects, ref / in / out parameters, and GC. Once you
introduce a coprocessor like a GPU, memory gets a little more complex.

Starting in this tutorial we need a bit of jargon:

* Device: the GPU or a GPU
* Host: the computer that contains the device

Each side can also have memory, to help keep it straight I will refer to it as:

* Device Memory: the GPU memory
* Host Memory: the CPU memory

In most computers, the host and device each have there own seperate memory. There are some ways
to pretend that they share memory in ILGPU, like ExchangeBuffers (more on that in a more Advanced
memory tutorial), but for now I will manage both sides manually.

NOTE: This "Device" is the actual hardware described by the Device class in ILGPU.

To use memory you need to be able to allocate it, copy data into it, and copy data out of it.
ILGPU provides an interface to do this.

NOTE: You will notice that all the memory is talked about in terms of arrays. If you want to pass
a single value into the GPU you can allocate an array of size 1 or pass it into the kernel as a
parameter, more on this in the Kernel tutorial and the Structs tutorial.

NOTE 2 (Return of the note): ILGPU v1.0 adds stride data to MemoryBuffer and ArrayView to fix
some issues. The right stride flavour depends on which axis you want contiguous in memory:

* `Stride1D.Dense` — for 1D buffers, always.
* `Stride2D.DenseY` / `Stride3D.DenseZY` — last axis contiguous. Matches the linear order of
  a C# `T[,]` allocated as `new T[H, W]` (or `T[,,]` as `new T[D, H, W]`) if you map ILGPU's
  first axis (`Index2D.X` / `Index3D.X`) to the slowest C# axis (the row / depth index).
* `Stride2D.DenseX` / `Stride3D.DenseXY` — first axis contiguous. Matches the in-memory order
  of a typical row-major bitmap (X = column, contiguous; Y = row, slow).

There is no one-size-fits-all default — it depends on how you mentally model your data. The
"MemoryBuffer2D and MemoryBuffer3D" section below explains the indexer convention and points at
runnable samples; see also `Samples/MemoryBufferStrides` for a worked example of all three
Stride2D flavours side-by-side.

# MemoryBuffer1D\<T\>

The MemoryBuffer is the host side copy of memory allocated on the device. It is essentially just a
pointer to the memory that was allocated on the Device.

* always obtained from an Accelerator
* requires: using ILGPU.Runtime;
* basic constructing: MemoryBuffer1D\<int, Stride1D.Dense\> OnDeviceInts = accelerator.Allocate1D\<int\>(1000);

#### CopyFromCPU

After allocating a MemoryBuffer you will probably want to load data into it. This can be done
using the CopyFromCPU method of a MemoryBuffer.

Basic usage, copying everything from IntArray to OnDeviceInts

* OnDeviceInts.CopyFromCPU(IntArray)

#### CopyToCPU

To copy memory out of a MemoyBuffer and into an array on host you use CopyToCPU.

Basic usage, copying everything from OnDeviceInts to IntArray

* OnDeviceInts.CopyToCPU(IntArray)

# ArrayView\<T\>

The ArrayView is the device side copy of memory allocated on the device via the host. This is the side of the
MemoryBuffer
API that the kernels / GPU will interact with.

* always obtained from a MemoryBuffer
* requires: using ILGPU.Runtime;
* basic constructing: ArrayView1D\<int, Stride1D.Dense\> ints = OnDeviceInts.View;

Inside the kernel the ArrayView works exactly like you would expect a normal array to. Again, more on that in the
Kernel tutorial.

# MemoryBuffer2D\<T, TStride\> and MemoryBuffer3D\<T, TStride\>

For 2D and 3D data — image grids, matrices, voxel volumes — ILGPU exposes
`MemoryBuffer2D` and `MemoryBuffer3D` along with a small family of stride
types (`Stride2D.DenseX` / `DenseY` / `General`, `Stride3D.DenseXY` / `DenseZY` /
`General`). The launch-side and host-side conventions are uniformly **X-then-Y**,
which is the single most common point of confusion when porting CPU image code.

#### Cheat sheet

* Indexes are constructed `new Index2D(X, Y)` and `new Index3D(X, Y, Z)` — never
  `(Y, X)` or `(Z, Y, X)`. Same for `LongIndex2D` / `LongIndex3D`.
* A buffer allocated as

  ```c#
  using var buffer = stream.Allocate2DDenseX<int>(new Index2D(W, H));
  ```

  has `buffer.Extent.X == W` (number of columns) and `buffer.Extent.Y == H`
  (number of rows). The kernel-side indexer is `view[new Index2D(x, y)]` —
  identical X-then-Y argument order.

* The stride flavour decides which axis is contiguous in memory:
  * `Stride2D.DenseX` → `view[Index2D(x, y)]` resolves to linear offset
    `y * W + x`. X varies fastest. Same memory order as a typical row-major
    bitmap (X = column, Y = row), with the *index argument* spelled `(x, y)`,
    not `(y, x)`.
  * `Stride2D.DenseY` → linear offset `x * H + y`. Y varies fastest. Useful for
    column-major numerics or when matching a `T[,]` allocated as `new T[H, W]`
    if you map ILGPU's X to C#'s row index.
  * `Stride2D.General` → user-provided `XStride` and `YStride`. Use this for
    pitched / aligned bitmaps where the row stride is larger than `extent.X`
    elements (see `Samples/MemoryBufferStrides`).
  * 3D mirrors the same idea: `Stride3D.DenseXY` makes X contiguous,
    `Stride3D.DenseZY` makes Z contiguous, `Stride3D.General` is fully
    user-specified.

* Round-tripping back to the host:

  ```c#
  var arr = buffer.GetAsArray2D();   // returns int[buffer.Extent.X, buffer.Extent.Y]
  ```

  `arr` is indexed `arr[x, y]` — *not* `arr[y, x]`. The first dimension of the
  returned `T[,]` is the X axis, matching the kernel-side `view[Index2D(x, y)]`.
  This is the opposite convention to the everyday C# bitmap idiom
  (`pixels[y, x]`), and is the single most common source of confusion when
  porting CPU image-processing code.

#### Common pitfall

Allocating with the dimensions transposed:

```c#
// WRONG — width and height swapped on the allocation, but the launch
// extent matches the *kernel*-side mental model.
var bad = stream.Allocate2DDenseX<byte>(new Index2D(height, width * 4));
stream.Launch(new Index2D(width, height), …);
```

The kernel iterates over `Index2D(width, height)` but writes into a buffer
whose `Extent.X` is `height` and `Extent.Y` is `width * 4` — every store lands
in the wrong place and the read-back image is rotated/garbled. The fix is to
make the allocation extent match the launch extent, or for byte-per-pixel
buffers, allocate as `Index2D(width * 4, height)` so the contiguous X axis
matches your row stride.

#### Runnable samples

* `Samples/SimpleArrayView2D` — minimal `Index2D`-launch + `ArrayView2D`
  parameter + `GetAsArray2D` round-trip with a position-encoded kernel that
  fails loudly on any layout mistake.
* `Samples/SimpleArrayView3D` — 3D analogue.
* `Samples/MemoryBufferStrides` — the three Stride2D flavours plus a worked
  pitched-bitmap example.

### Memory Example [See Also Simple Allocation Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/SimpleAlloc)

All device side memory management happens in the host code through the MemoryBuffer.
The sample goes over the basics of managing memory via MemoryBuffers. There will be far more
in depth memory management in the later tutorials.

```c#
using System;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

public static class Program
{
    public static readonly bool debug = false;
    static void Main()
    {
        // We still need the Context and Accelerator boiler plate.
        Context context = Context.CreateDefault();
        Accelerator accelerator = context.CreateCPUAccelerator(0);

        // Gets array of 1000 doubles on host.
        double[] doubles = new double[1000];

        // Gets MemoryBuffer on device with same size and contents as doubles.
        MemoryBuffer1D<double, Stride1D.Dense> doublesOnDevice = accelerator.Allocate1D(doubles);

        // What if we change the doubles on the host and need to update the device side memory?
        for (int i = 0; i < doubles.Length; i++) { doubles[i] = i * Math.PI; }

        // We call MemoryBuffer.CopyFrom which copies any linear slice of doubles into the device side memory.
        doublesOnDevice.CopyFromCPU(doubles);

        // What if we change the doublesOnDevice and need to write that data into host memory?
        doublesOnDevice.CopyToCPU(doubles);

        // You can copy data to and from MemoryBuffers into any array / span / memorybuffer that allocates the same
        // type. for example:
        double[] doubles2 = new double[doublesOnDevice.Length];
        doublesOnDevice.CopyFromCPU(doubles2);

        // There are also helper functions, but be aware of what a function does.
        // As an example this function is shorthand for the above two lines.
        // This completely allocates a new double[] on the host. This is slow.
        double[] doubles3 = doublesOnDevice.GetAsArray1D();

        // Notice that you cannot access memory in a MemoryBuffer or an ArrayView from host code.
        // If you uncomment the following lines they should crash.
        // doublesOnDevice[1] = 0;
        // double d = doublesOnDevice[1];

        // There is not much we can show with ArrayViews currently, but in the 
        // Kernels Tutorial it will go over much more.
        ArrayView1D<double, Stride1D.Dense> doublesArrayView = doublesOnDevice.View;

        // do not forget to dispose of everything in the reverse order you constructed it.
        doublesOnDevice.Dispose();
        // note the doublesArrayView is now invalid, but does not need to be disposed.
        accelerator.Dispose();
        context.Dispose();
    }
}
```
