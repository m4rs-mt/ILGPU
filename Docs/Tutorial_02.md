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
* Host Memory: the computers memory

In most computers the host and device each have there own seperate memory. There are some ways
to pretend that they share memory in ILGPU, like ExchangeBuffers (more on that in a more advanced 
memory tutorial), but for now I will manage both sides manually.

To use memory you need to be able to allocate it, copy data into it, and copy data out of it.
ILGPU provides an interface to do this. 

NOTE: You will notice that all the memory is talked about in terms of arrays. If you want to pass 
a single value into the GPU you can allocate an array of size 1 or pass it into the kernel as a 
parameter, more on this in the Kernel tutorial and the Structs tutorial.

# MemoryBuffer\<T\>
The MemoryBuffer is the host side copy of memory allocated on the device. 

* always obtained from an Accelerator
* requires: using ILGPU.Runtime;
* basic constructing: MemoryBuffer\<int\> OnDeviceInts = accelerator.Allocate\<int\>(1000);

#### CopyFrom
After allocating a MemoryBuffer you will probably want to load data into it. This can be done 
using the CopyFrom method of a MemoryBuffer.

Basic usage, copying everything from IntArray to OnDeviceInts
* OnDeviceInts.CopyFrom(IntArray, sourceOffset, targetOffset, count)

This works as you would expect. Starting at sourceOffset in IntArray and targetOffset in OnDeviceInts it 
copies count values from IntArray into OnDeviceInts.

#### CopyTo
To copy memory out of a MemoyView and into an array on device you use CopyTo.

Basic usage, copying everything from OnDeviceInts to IntArray
* OnDeviceInts.CopyTo(IntArray, sourceOffset, targetOffset, count)

This works just like CopyFrom, just backwards. Starting at sourceOffset in OnDeviceInts and targetOffset in IntArray it 
copies count values from OnDeviceInts into IntArray.

In both CopyTo and CopyFrom setting sourceOffset and targetOffset to 0 and count to IntArray.Length would copy all
values.
* OnDeviceInts.CopyFrom(IntArray, 0, 0, IntArray.Length)
* OnDeviceInts.CopyTo(IntArray, 0, 0, IntArray.Length)


# ArrayView\<T\>
The ArrayView is the device side copy of memory allocated on the device via the host. This is the side of the MemoryBuffer
API that the kernels / GPU will interact with.

* always obtained from a MemoryBuffer
* requires: using ILGPU.Runtime;
* basic constructing: ArrayView\<int\> ints = OnDeviceInts.View;

Inside the kernel the ArrayView works exactly like you would expect a normal array to. Again, more on that in the 
Kernel tutorial.

### Memory Sample
All device side memory management happens in the host code through the MemoryBuffer.
The sample goes over the basics of managing memory via MemoryBuffers. There will be far more
in depth memory management in the later tutorials.

```C#
using System;
using System.Linq;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace Tutorial
{
    class Program
    {
        public static readonly bool debug = false;
        static void Main()
        {
            // We still need the Context and Accelerator boiler plate.
            Console.WriteLine("Hello Tutorial 02!");
            Context context = new Context();
            Accelerator accelerator = null;
            
            if (CudaAccelerator.CudaAccelerators.Length > 0 && !debug)
            {
                accelerator = new CudaAccelerator(context);
            }
            else if (CLAccelerator.AllCLAccelerators.Length > 0 && !debug)
            {
                accelerator = new CLAccelerator(context, CLAccelerator.AllCLAccelerators.FirstOrDefault());
            }
            else
            {
                accelerator = new CPUAccelerator(context);
            }

            // Gets array of 1000 doubles on host.
            double[] doubles = new double[1000];

            // Gets MemoryBuffer on device with same size and contents as doubles.
            MemoryBuffer<double> doublesOnDevice = accelerator.Allocate<double>(doubles);

            // What if we change the doubles on the host and need to update the device side memory?
            for (int i = 0; i < doubles.Length; i++) { doubles[i] = i * Math.PI; }

            // We call MemoryBuffer.CopyFrom which copies any linear slice of doubles into the device side memory.
            doublesOnDevice.CopyFrom(doubles, 0, 0, doubles.Length);

            // What if we change the doublesOnDevice and need to write that data into host memory?
            doublesOnDevice.CopyTo(doubles, 0, 0, doubles.Length);

            // You can copy data to and from MemoryBuffers into any array / span / memorybuffer that allocates the same
            // type. for example:
            double[] doubles2 = new double[doublesOnDevice.Length];
            doublesOnDevice.CopyTo(doubles2, 0, 0, doubles2.Length);

            // There are also helper functions, but be aware of what a function does.
            // As an example this function is shorthand for the above two lines.
            // It does a relatively slow memory allocation on the host.
            double[] doubles3 = doublesOnDevice.GetAsArray();

            // Notice that you cannot access memory in a MemoryBuffer or an ArrayView from host code.
            // If you uncomment the following lines they should crash.
            // doublesOnDevice[1] = 0;
            // double d = doublesOnDevice[1];

            // There is not much we can show with ArrayViews currently, but in the 
            // Kernels Tutorial it will go over much more.
            ArrayView<double> doublesArrayView = doublesOnDevice.View;

            // do not forget to dispose of everything in the reverse order you constructed it.
            doublesOnDevice.Dispose(); 
            // note the doublesArrayView is now invalid, but does not need to be disposed.
            accelerator.Dispose();
            context.Dispose();
        }
    }
}
```