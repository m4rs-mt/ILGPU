using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace SharedMemory;

static class Kernels
{
    /// <summary>
    /// Shared-memory variable kernel: finds the max per group.
    /// </summary>
    public static void SharedMemoryVariableKernel(
        ArrayView1D<int, Stride1D.Dense> dataSource,
        ArrayView1D<int, Stride1D.Dense> dataTarget)
    {
        int globalIndex = (int)Grid.GlobalThreadIndex;

        // Allocate a single-element shared memory array
        var shared = Group.GetSharedMemory<int>(1);

        // Initialize shared memory
        if (Group.IsFirstThread)
            shared[0] = 0;
        Group.Barrier();

        if (globalIndex < dataSource.Length)
            Atomic.Max(ref shared[0], dataSource[globalIndex]);

        // Wait for all threads to complete the maximum computation
        Group.Barrier();

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = shared[0];
    }

    /// <summary>
    /// Shared-memory array kernel: computes per-group sum.
    /// </summary>
    public static void SharedMemoryArrayKernel(
        ArrayView1D<int, Stride1D.Dense> dataSource,
        ArrayView1D<int, Stride1D.Dense> dataTarget)
    {
        int globalIndex = (int)Grid.GlobalThreadIndex;

        // Allocate a shared-memory array with 128 elements
        ArrayView<int> sharedArray = Group.GetSharedMemory<int>(128);

        // Load the element into shared memory
        var value = globalIndex < dataSource.Length
            ? dataSource[globalIndex]
            : 0;
        sharedArray[Group.Index] = value;

        // Wait for all threads to complete the loading process
        Group.Barrier();

        // Compute the sum over all elements in the group
        int sum = 0;
        for (int i = 0, e = Group.Dimension; i < e; ++i)
            sum += sharedArray[i];

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = sum;
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        // The maximum group size in this example is 128 since the second
        // kernel has a shared-memory array of 128 elements.
        var groupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, 128);

        var data = Enumerable.Range(1, 128).ToArray();

        using var dataSource = stream.Allocate1D<int>(data.Length);
        dataSource.CopyFromCPU(data);

        int numGroups = (data.Length + groupSize - 1) / groupSize;
        var config = new KernelConfig(gridDim: numGroups, groupDim: groupSize);

        using var dataTarget = stream.Allocate1D<int>(data.Length);

        // Shared-memory variable kernel: finds the max per group
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.SharedMemoryVariableKernel(dataSource.View, dataTarget.View));
            stream.Synchronize();

            Console.WriteLine("Shared-memory kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // Shared-memory array kernel: computes per-group sum
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.SharedMemoryArrayKernel(dataSource.View, dataTarget.View));
            stream.Synchronize();

            Console.WriteLine("Shared-memory-array kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        Console.WriteLine("Done.");
    }
}
