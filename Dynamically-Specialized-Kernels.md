Dynamic specialization allows the definition of kernels that will be `specialized/optimized` during runtime.
This allows you to define kernels with `constant values` that are not known at compile time of the kernel or application.
Without knowing the exact values (or ranges of values) of certain parameters, the compiler's optimization capabilities are limited, e.g. with regard to constant propagation and loop unrolling.

Similar functionality can be achieved by using generic types in a clever way.
However, dynamic specialization is much more convenient and easier to use.
Moreover, it is more flexible without leveraging the .Net reflection API to create specialized instances.

Please note that dynamically specialized kernels are precompiled during loading.
The final compilation step occurs during the first call of a new (non-cached) specialized parameter combination.
If a parameter combination was used previously, the corresponding specialized kernel instance is called.

```c#
class ...
{
    static void GenericKernel(ArrayView<int> data, int c)
    {
        var globalIndex = Grid.GlobalIndex.X;
        // Generates code that loads c and adds the value 2 at runtime of the kernel
        data[globalIndex] = c + 2;
    }

    static void SpecializedKernel(ArrayView<int> data, SpecializedValue<int> c)
    {
        var globalIndex = Grid.GlobalIndex.X;
        // Generates code that has an inlined constant value
        data[globalIndex] = c + 2; // Will be specialized for every value c
    }

    static void ...(...)
    {
        using var context = new Context();
        using var accl = new CudaAccelerator(context);

        var genericKernel = accl.LoadStreamKernel<ArrayView<int>, int>(GenericKernel);
        ...
        genericKernel((<UserGridDim>, <UserGroupDim>), buffer.View, 40);

        var specializedKernel = accl.LoadStreamKernel<ArrayView<int>, SpecializedValue<int>>(GenericKernel);
        ...
        specializedKernel((<UserGridDim>, <UserGroupDim>), buffer.View, SpecializedValue.New(40));
        ...
    }
}
```