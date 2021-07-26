# Tutorial 01 Context and Accelerators

Welcome to the first ILGPU tutorial! In this tutorial we will cover the basics of the Context and Accelerator objects.

## Context
All ILGPU classes and functions rely on an instance of ILGPU.Context.
The context's job is mainly to act as an interface for the ILGPU compiler. 
I believe it also stores some global state. 
* requires: using ILGPU;
* basic constructing: Context context = new Context();

A context object, as well as most instances of classes that 
require a context, require dispose calls to prevent memory 
leaks. In most simple cases you can use the using pattern as such: using Context context = new Context();
to make it harder to mess up. You can also see this in the first sample below.

You can also use the ContextFlags enum to change many settings.
We will talk about those at the end of this tutorial. 

For now all we need is a basic context.

## Accelerators
In ILGPU the accelerator repersents a hardware or software GPU.
Every ILGPU program will require at least 1 Accelerator.
Currently there are 3 Accelerator types CPU, Cuda, and OpenCL, 
as well as an abstract Accelerator.

### Sample 01|01
```c#
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using System;

public static class Program
{
    public static void Main()
    {
        using Context context = new Context();

        // Prints all accelerators.
        foreach(var id in Accelerator.Accelerators)
        {
            using Accelerator accelerator = Accelerator.Create(context, id);
            Console.WriteLine(accelerator);
            accelerator.PrintInformation();
            Console.WriteLine();
        }

        // Prints the CPUAccelerator
        using CPUAccelerator CPUDevice = new CPUAccelerator(context);
        Console.WriteLine("This is the CPU device:");
        CPUDevice.PrintInformation();
        Console.WriteLine();

        // Prints all Cuda Accelerators 
        foreach (var id in CudaAccelerator.CudaAccelerators)
        {
            using CudaAccelerator accelerator = new CudaAccelerator(context, id);
            Console.WriteLine("Found a Cuda device:");
            accelerator.PrintInformation();
            Console.WriteLine();
        }

        // Prints all OpenCL Accelerators
        foreach (var id in CLAccelerator.CLAccelerators)
        {
            using CLAccelerator accelerator = new CLAccelerator(context, id);
            Console.WriteLine("Found a OpenCL device:");
            accelerator.PrintInformation();
            Console.WriteLine();
        }
    }
}
```

##### CPUAccelerator
* requires no special hardware... well no more than c# does.
* requires: using ILGPU.CPU; and using ILGPU.Runtime;
* basic constructing: Accelerator accelerator = new CPUAccelerator(context);

In general the CPUAccelerator is best for debugging and as a fallback. While the
CPUAccelerator is slow it is the only way to use much of the debugging features built
into C#. It is a good idea to write your program in such a way that you are able to switch to a CPUAcclerator to aid debugging.

##### CudaAccelerator
* requires a supported CUDA capable gpu
* imports: using ILGPU.Cuda; using ILGPU.Runtime;
* basic constructing: Accelerator accelerator = new CudaAccelerator(context);

If you have one or more Nvida GPU's that are supported this is the accelerator for 
you. What is supported is a complex question, but in general anything GTX 680 or 
newer should work. Some features require newer cards. Feature support should<sup>0</sup> match CUDA.

##### CLAccelerator
* requires an OpenCL 2.0+ capable gpu
* imports: using ILGPU.OpenCL, using ILGPU.Runtime;
* basic constructing: Accelerator accelerator = new CLAccelerator(context, CLAccelerator.CLAccelerator[0]);

If you have one or more AMD or Intel GPU's that are supported this is
the accelerator for you. Technically Nvidia GPU's support OpenCL but 
they are limited to OpenCL 1.2 which is essentially useless. 
Because of this these tutorials need a bit of a disclaimer: I do not 
have an OpenCL 2.0 compatible GPU so most of the OpenCL stuff is untested. 
Please let me know if there are any issues.

##### Accelerator
Abstract class for storing and passing around more specific
accelerators.
* requires: using ILGPU.Runtime

### Sample 01|02
There is currently no guaranteed way to find the most powerful accelerator. If you are programming for 
known hardware you can just hardcode it. However, if you do need a method, the following is a pretty simple way
to get what is likely the best accelerator if you have zero or one GPUs. If you have multiple
GPUs or something uncommon you may need something more complex.

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
        // I normally have an easy to change bool or class parameter that forces
        // the cpu accelerator to aid debugging.
        public static readonly bool debug = false;
        static void Main()
        {
            Console.WriteLine("Hello Tutorial 01!");
            using Context context = new Context();
            Console.WriteLine("Context: " + context.ToString());

            Accelerator accelerator = null;
            if (CudaAccelerator.CudaAccelerators.Length > 0 && !debug)
            {
                accelerator = new CudaAccelerator(context);
            }
            else if (CLAccelerator.CLAccelerators.Length > 0 && !debug)
            {
                accelerator = new CLAccelerator(context, CLAccelerator.CLAccelerators.FirstOrDefault());
            }
            else
            {
                accelerator = new CPUAccelerator(context);
            }
            accelerator.PrintInformation();
            accelerator.Dispose();
        }
    }
}
```
Don't forget to dispose the accelerator. We do not have to call dispose 
of context because we used the using pattern. It is important to note 
that you must dispose objects in the reverse order from when you obtain them.

As you can see in the above sample the context is obtained first and then 
the accelerator. We dispose the accelerator explicitly by calling accelerator.Dispose();
and then only afterwards dispose the context automatically via the using pattern.

In more complex programs you will have a more complex tree of memory, kernels, streams, and accelerators
 to dispose of correctly.

Lets assume this is the structure of some program:
* Context
  * CPUAccelerator
    * Some Memory
    * Some Kernel
  * CudaAccelerator
    * Some Other Memory
    * Some Other Kernel

Anything created by the CPU accelerator must be disposed before the CPU accelerator
can be disposed. And then the CPU accelerator must be disposed before the context can
be disposed. However before we can't dispose the context we must dispose the Cuda accelerator
 and everything that it owns.

Ok, this tutorial covers most of the boiler plate code needed.

The next tutorial covers memory, after that I PROMISE we will do something more interesting. I just have to write them first.

> <sup>0</sup> Should is the programmers favorite word
>
> \- Me
> 
> In all seriousness your best bet is to test the features you want to use against the gpu you would like to use, or consult [this](https://docs.nvidia.com/cuda/cuda-c-programming-guide/index.html#compute-capabilities) part of the cuda docs.
