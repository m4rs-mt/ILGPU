# Tutorial 02 MemoryBuffers and ArrayViews

Welcome to the seccond ILGPU tutorial. In this tutorial we will cover the basics
 of the Memory in ILGPU. In the best case C# programmers will think of memory 
in terms of stack and heap objects, ref / in / out parameters, and GC. Once you
introduce a coprocessor like a GPU memory gets a little more complex. 

Starting in this tutorial we need a bit of jargon:

* Device: the GPU or a GPU
* Host: the computer that contains the device

Each side can also have memory, to help keep it straight I will refer to it as:

* Device Memory: the GPU memory
* Host Memory: the CPU memory

In most computers the host and device each have there own seperate memory. There are some ways
to pretend that they share memory in ILGPU, like ExchangeBuffers (more on that in a more advanced 
memory tutorial), but for now I will manage both sides manually.

NOTE: This "Device" is the actual hardware described by the Device class in ILGPU.

To use memory you need to be able to allocate it, copy data into it, and copy data out of it.
ILGPU provides an interface to do this. 

NOTE: You will notice that all the memory is talked about in terms of arrays. If you want to pass 
a single value into the GPU you can allocate an array of size 1 or pass it into the kernel as a 
parameter, more on this in the Kernel tutorial and the Structs tutorial.

NOTE 2 (Return of the note): ILGPU 1.0 adds stride data to MemoryBuffer and ArrayView to fix 
some issues. *IMPORTANT:* When in doubt use Stride1D.Dense, Stride2D.DenseY, or Stride2D.DenseZY.
I will go over this better in a striding tutorial, but these should be your defaults because they 
require they match how C# strides 1D, 2D, and 3D arrays.

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
To copy memory out of a MemoyView and into an array on device you use CopyToCPU.

Basic usage, copying everything from OnDeviceInts to IntArray
* OnDeviceInts.CopyToCPU(IntArray)

# ArrayView\<T\>
The ArrayView is the device side copy of memory allocated on the device via the host. This is the side of the MemoryBuffer
API that the kernels / GPU will interact with.

* always obtained from a MemoryBuffer
* requires: using ILGPU.Runtime;
* basic constructing: ArrayView1D\<int, Stride1D.Dense\> ints = OnDeviceInts.View;

Inside the kernel the ArrayView works exactly like you would expect a normal array to. Again, more on that in the 
Kernel tutorial.

### Memory Example [See Also Simple Allocation Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/SimpleAlloc)
All device side memory management happens in the host code through the MemoryBuffer.
The sample goes over the basics of managing memory via MemoryBuffers. There will be far more
in depth memory management in the later tutorials.

```C#
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