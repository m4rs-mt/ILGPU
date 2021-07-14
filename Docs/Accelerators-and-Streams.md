## Accelerators

Accelerators represent hardware or software GPU devices.
They store information about different devices and allow memory allocation and kernel loading on a particular device.
A launch of a kernel on an accelerator is performed asynchronously by default.
Synchronization with the accelerator or the associated stream is required in order to to wait for completion and to fetch results.

Note that instances of classes that depend on an accelerator reference have to be disposed before disposing of the associated accelerator object.
However, this does not apply to automatically managed kernels, which are cached inside the accelerator object.

```c#
class ...
{
    static void Main(string[] args)
    {
        // Initialize a new ILGPU context
        using var context = new Context();

        using (var cpuAccelerator = new CPUAccelerator(context);
        // Perform operations on the CPU

        using var cudaAccelerator = new CudaAccelerator(context);
        // Perform operations on the default Cuda device

        // Iterate over all available accelerators
        foreach (var acceleratorId in Accelerator.Accelerators)
        {
            using (var accl = Accelerator.Create(context, acceleratorId))
            {
                // Perform operations
            }
        }
    }
}
```

You can print detailed accelerator information to the stdout stream by invoking the `PrintInformation` method. This yields output similar to the following. Sample output of an RTX 3090 using `accelerator.PrintInformation()`:

```
Device: GeForce RTX 3090 [ILGPU InstanceId: 20]
  Cuda device id:                          0
  Cuda driver version:                     11.2
  Cuda architecture:                       SM_86
  Instruction set:                         7.1
  Clock rate:                              1860 MHz
  Memory clock rate:                       9751 MHz
  Memory bus width:                        384-bit
  Number of multiprocessors:               82
  Max number of threads/multiprocessor:    1536
  Max number of threads/group:             1024
  Max number of total threads:             125952
  Max dimension of a group size:           (1024, 1024, 64)
  Max dimension of a grid size:            (2147483647, 65535, 65535)
  Total amount of global memory:           25769803776 bytes, 24576 MB
  Total amount of constant memory:         65536 bytes, 64 KB
  Total amount of shared memory per group: 49152 bytes, 48 KB
  Total amount of shared memory per mp:    102400 bytes, 100 KB
  L2 cache size:                           6291456 bytes, 6144 KB
  Max memory pitch:                        2147483647 bytes
  Total number of registers per mp:        65536
  Total number of registers per group:     65536
  Concurrent copy and kernel execution:    True, with 2 copy engines
  Driver mode:                             WDDM
  Has ECC support:                         False
  Supports managed memory:                 True
  Supports compute preemption:             True
  PCI domain id / bus id / device id:      0 / 11 / 0
  NVML PCI bus id:                         0000:0B:00.0
```

## Cuda and OpenCL Accelerators

The current Cuda (PTX) backend supports different driver and feature levels.
The Cuda backend does not require a Cuda SDK to be installed/configured.

An automatic driver detection module selects an appropriate PTX ISA version for your graphics driver.
However, if you encounter the error message `A PTX jit compilation failed` try updating the graphics driver first before diving deeper into this issue.

Use `CudaAccelerator.CudaAccelerators` to query all Cuda-compatible GPUs in your system. Use `CLAccelerator.CLAccelerators` to query all OpenCL-compatible GPUs in your system.

It is **highly recommended** to use the `CudaAccelerator` class for NVIDIA GPUs and the `CLAccelerator` class for Intel and AMD GPUs.

```c#
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
class ...
{
    static void Main(string[] args)
    {
        ...
        var allSupportedAccelerators = Accelerator.Accelerators;
        var cudaAccelerators = CudaAccelerator.CudaAccelerators;
        var supportedCLAccelerators = CLAccelerator.CLAccelerators;
        var allCLAccelerators = CLAccelerator.AllCLAccelerators;
    }
}
```

## Streams

`AcceleratorStreams` represent async operation queues, which operations can be submitted to.
Custom accelerator streams have to be synchronized manually.
Using streams increases the parallellism of applications.
Every accelerator encapsulates a default accelerator stream that is used for all operations by default.

```c#
class ...
{
    static void Main(string[] args)
    {
        ...

        var defaultStream = accelerator.DefaultStream;
        using (var secondStream = accelerator.CreateStream())
        {

            // Perform actions using default stream...

            // Perform actions on second stream...

            // Wait for results from the first stream.
            defaultStream.Synchronize();

            // Use results async compared to operations on the second stream...

            // Wait for results from the second stream
            secondStream.Synchronize();

            ...
        }
    }
}
```