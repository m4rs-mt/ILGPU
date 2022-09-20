# Tutorial 01 Context, Device, and Accelerators

Welcome to the first ILGPU tutorial! In this tutorial we will cover the basics of the Context, Device, and Accelerator objects.

## Context
All ILGPU classes and functions rely on an instance of ILGPU.Context.
The context's job is mainly to act as an interface for the ILGPU compiler. 
I believe it also stores some global state. 
* requires: using ILGPU;
* basic constructing: Context context = Context.CreateDefault();

A context object, as well as most instances of classes that 
require a context, require dispose calls to prevent memory 
leaks. In most simple cases you can use the using pattern as 
such: using Context context = Context.CreateDefault();
to make it harder to mess up. You can also see this in the first sample below.

You can also use the Context Builder to change context settings, more on that in a later tutorial.

For now all we need is a default context.

## Device
Before version 1.0.0 ILGPU had no way to query device information without creating a full accelerator instance.
ILGPU v1.0.0 added in the Device class to fix this issue.

In ILGPU the Device represents the hardware in your computer.
* requires: using ILGPU; and using ILGPU.Runtime;

### List Devices Sample
Lists all devices that ILGPU can use.
```c#
using ILGPU;
using ILGPU.Runtime;
using System;

public static class Program
{
    static void Main()
    {
        Context context = Context.Create(builder => builder.AllAccelerators());

        foreach (Device device in context)
        {
            Console.WriteLine(device);
        }
    }
}
```
## Accelerators
In ILGPU the accelerator represents a hardware or software GPU.
Every ILGPU program will require at least 1 Accelerator.
Currently there are 3 Accelerator types: CPU, Cuda, and OpenCL, 
as well as an abstract Accelerator.

### Device Info Example [See Also Device Info Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/DeviceInfo)
```c#
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using System;
using System.IO;

public static class Program
{
    public static void Main()
    {
        // Builds a context that has all possible accelerators.
        using Context context = Context.CreateDefault();

        // Builds a context that only has CPU accelerators.
        //using Context context = Context.Create(builder => builder.CPU());

        // Builds a context that only has Cuda accelerators.
        //using Context context = Context.Create(builder => builder.Cuda());

        // Builds a context that only has OpenCL accelerators.
        //using Context context = Context.Create(builder => builder.OpenCL());

        // Builds a context with only OpenCL and Cuda acclerators.
        //using Context context = Context.Create(builder =>
        //{
        //    builder
        //        .OpenCL()
        //        .Cuda();
        //});

        // Prints all accelerators.
        foreach (Device d in context)
        {
            using Accelerator accelerator = d.CreateAccelerator(context);
            Console.WriteLine(accelerator);
            Console.WriteLine(GetInfoString(accelerator));
        }

        // Prints all CPU accelerators.
        foreach (CPUDevice d in context.GetCPUDevices())
        {
            using CPUAccelerator accelerator = (CPUAccelerator)d.CreateAccelerator(context);
            Console.WriteLine(accelerator);
            Console.WriteLine(GetInfoString(accelerator));
        }

        // Prints all Cuda accelerators.
        foreach (Device d in context.GetCudaDevices())
        {
            using Accelerator accelerator = d.CreateAccelerator(context);
            Console.WriteLine(accelerator);
            Console.WriteLine(GetInfoString(accelerator));
        }

        // Prints all OpenCL accelerators.
        foreach (Device d in context.GetCLDevices())
        {
            using Accelerator accelerator = d.CreateAccelerator(context);
            Console.WriteLine(accelerator);
            Console.WriteLine(GetInfoString(accelerator));
        }
    }

    private static string GetInfoString(Accelerator a)
    {
        StringWriter infoString = new StringWriter();
        a.PrintInformation(infoString);
        return infoString.ToString();
    }
}
```

##### CPUAccelerator
* requires no special hardware... well no more than C# does.
* requires: using ILGPU.CPU; and using ILGPU.Runtime;
* basic constructing: Accelerator accelerator = context.CreateCPUAccelerator(0);

The parameter of CreateCPUAccelerator denotes which cpu will be used if the context is constructed with multiple debug cpu acclerators.

In general the CPUAccelerator is best for debugging and as a fallback. While the
CPUAccelerator is slow it is the only way to use much of the debugging features built
into C#. It is a good idea to write your program in such a way that you are able to switch to a CPUAcclerator to aid debugging.

##### CudaAccelerator
* requires a supported CUDA capable gpu
* imports: using ILGPU.Cuda; using ILGPU.Runtime;
* basic constructing: Accelerator accelerator = context.CreateCudaAccelerator(0);

The parameter of CreateCudaAccelerator denotes which gpu will be used in the case of a multi-gpu system.

If you have one or more Nvida GPUs that are supported, this is the accelerator for 
you. What is supported is a complex question, but in general anything GTX 680 or 
newer should work. Some features require newer cards. Feature support should<sup>0</sup> match CUDA.

##### CLAccelerator
* requires an OpenCL 2.0+ capable gpu
* imports: using ILGPU.OpenCL, using ILGPU.Runtime;
* basic constructing: Accelerator accelerator = context.CreateCLAccelerator(0);

The parameter of CreateCLAccelerator denotes which gpu will be used in the case of a multi-gpu system.
NOTE: This is the *1st* OpenCL device usable by ILGPU and *not* the 1st OpenCL device of your machine.

If you have one or more AMD or Intel GPUs that are supported this is
the accelerator for you. Technically Nvidia GPUs support OpenCL but 
they are limited to OpenCL 1.2 which is essentially useless. 
Because of this, these tutorials need a bit of a disclaimer: I do not 
have an OpenCL 2.0 compatible GPU so most of the OpenCL stuff is untested. 
Please let me know if there are any issues.

NOTE: OpenCL 3.0 makes this far more complex but still doesn't fix the issue that Nvidia GPUs are unsupported.

##### Accelerator
Abstract class for storing and passing around more specific
accelerators.
* requires: using ILGPU.Runtime

### Sample 01|03
There is no guaranteed way to find the most powerful accelerator. If you are programming for 
known hardware you can, and should, just hardcode it. However, if you do need a method, ILGPU provides two.

For a single device: context.GetPreferredDevice(preferCPU);

For multiple devices: context.GetPreferredDevices(preferCPU, matchingDevicesOnly);

```c#
using System;
using ILGPU;
using ILGPU.Runtime;
public static class Program
{
    static void Main()
    {
        using Context context = Context.Create(builder => builder.AllAccelerators());
        Console.WriteLine("Context: " + context.ToString());

        Device d = context.GetPreferredDevice(preferCPU: false);
        Accelerator a = d.CreateAccelerator(context);

        a.PrintInformation();
        a.Dispose();

        foreach(Device device in context.GetPreferredDevices(preferCPU: false, matchingDevicesOnly: false))
        {
            Accelerator accelerator = device.CreateAccelerator(context);
            accelerator.PrintInformation();
            accelerator.Dispose();
        }
    }
}
```
Don't forget to dispose the accelerator. We do not have to call dispose 
on context because we used the using pattern. It is important to note 
that you must dispose objects in the reverse order from when you obtain them.

As you can see in the above sample, the context is obtained first and then 
the accelerator. We dispose the accelerator explicitly by calling accelerator.Dispose();
and then only afterwards dispose the context automatically via the using pattern.

The Device instances do not need to be disposed.

In more complex programs you may have a more complex tree of memory, kernels, streams, and accelerators
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
be disposed. However before we can dispose the context we must dispose the Cuda accelerator
 and everything that it owns.

Ok, this tutorial covers most of the boiler plate code needed.

The next tutorial covers memory.

> <sup>0</sup> Should is the programmers favorite word
>
> \- Me
> 
> In all seriousness your best bet is to test the features you want to use against the gpu you would like to use, or consult [this](https://docs.nvidia.com/cuda/cuda-c-programming-guide/index.html#compute-capabilities) part of the cuda docs.