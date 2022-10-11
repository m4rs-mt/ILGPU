ILGPU support both `static` and `dynamic` shared memory.
Static shared memory is limited to `statically known` allocations which have a known size at compile time of the kernel.
The latest ILGPU versions allow the use of dynamic shared memory, which can be specified for each kernel launch individually.

```c#
class ...
{
    static void SharedMemKernel(ArrayView<int> data)
    {
        // Static memory allocation
        var staticMemory = SharedMemory.Allocate<int>>(1024);

        // Use GetDynamic access dynamically specified shared memory
        var dynamicMemory = SharedMemory.GetDynamic<int>>();

        ...

        // Use GetDynamic with a different element type to access
        // the same memory region in a different way
        var dynamicMemory2 = SharedMemory.GetDynamic<double>>();

        ...
    }

    static void ...(...)
    {
        using var context = Context.CreateDefault();
        using var accl = context.CreateCudaAccelerator(0);

        // Create shared memory configuration using a custom element type.
        // Note that this does not need to be the same type that is used in the scope of the kernel.
        // Therefore, the following two configurations will allocate the same amount of shared memory:
        var config = SharedMemoryConfig.RequestDynamic<byte>(<GroupSize> * sizeof(int));
        var config2 = SharedMemoryConfig.RequestDynamic<int>(<GroupSize>);

        ...
        // Pass the shared memory configuration to the kernel configuration
        kernel((<UserGridDim>, <UserGroupDim>, config), buffer.View);
        ...
    }
}
```

*Note that this feature available on CPU, Cuda and OpenCL accelerators.*
