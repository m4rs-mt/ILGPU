using System;
using ILGPU;
using ILGPU.Runtime;

namespace DeviceInfo;

static class Program
{
    static void PrintAcceleratorInfo(Accelerator accelerator)
    {
        Console.WriteLine($"Name: {accelerator.Name}");
        Console.WriteLine($"MemorySize: {accelerator.MemorySize}");
        Console.WriteLine($"MaxThreadsPerGroup: {accelerator.MaxNumThreadsPerGroup}");
        Console.WriteLine($"MaxSharedMemoryPerGroup: {accelerator.MaxSharedMemoryPerGroup}");
        Console.WriteLine($"MaxConstantMemory: {accelerator.MaxConstantMemory}");
        Console.WriteLine($"WarpSize: {accelerator.WarpSize}");
        Console.WriteLine($"NumMultiprocessors: {accelerator.NumMultiprocessors}");
    }

    static void Main()
    {
        // Enumerate all available devices
        using var context = Context.CreateDefault();

        foreach (var device in context)
        {
            using var accelerator = device.CreateAccelerator(context);
            Console.WriteLine($"Accelerator: {device.AcceleratorType}, {accelerator.Name}");
            PrintAcceleratorInfo(accelerator);
            Console.WriteLine();
        }
    }
}
