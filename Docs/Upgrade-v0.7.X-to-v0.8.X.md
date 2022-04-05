---
layout: wiki
---

## General Notes

All explicitly grouped kernel launchers have been updated.
This simplifies programming and feels more natural for most kernel developers familiar with Cuda and OpenCL.
New static `Group` and `Grid` properties provide convenient access to grid and group indices.

Dynamic partial evaluation allows to create specialized kernels that are <i>automagically compiled</i> for you.
Use the structure type `SpecializedValue<T>` on kernel parameters to enable specialization of values at runtime.
Note that these values must support the `IEquatable` interface for them to be cached correctly.
As soon as a value is found (which could not be found in the cache), the kernel is recompiled in the background with the new specialized value.

Note that the `ILGPU.Index` type is now considered obsolete because there is a name ambiguity between `ILGPU.Index` and `System.Index`.
Please update your code to use the new type `ILGPU.Index1` instead.
*Note further that these index types might be removed in the future.*

## New Kernel Launchers

In previous versions, it was necessary to have a kernel parameter of type `GroupedIndex` for explicitly grouped kernels.
Accesses to the current group and grid indices were only possible by accessing this parameter.
Alternative methods to access group and grid indices were introduced in v0.7.X, in order to create helper methods that can directly access those properties.
From a software-engineering point of view, this could have been considered the same functionality with two different flavors.
This *issue* was corrected in v0.8.
It also simplifies programming for Cuda and OpenCL developers.

```c#
class ...
{
    static void OldKernel(GroupedIndex index, ArrayView<int> data)
    {
      var globalIndex = index.ComputeGlobalIndex();
      data[globalIndex] = 42;
    }

    static void NewKernel(ArrayView<int> data)
    {
      var globalIndex = Grid.GlobalIndex.X;
      // or
      var globalIndex = Group.IdxX + Grid.IdxX * Group.DimX;

      data[globalIndex] = 42;
    }

    static void ...(...)
    {
        using var context = new Context();
        using var accl = new CudaAccelerator(context);

        // Old way
        var oldKernel = accl.LoadStreamKernel<GroupedIndex, ArrayView<int>>(OldKernel);
        ...
        oldKernel(new GroupedIndex(<GridDim>, <GroupDim>), buffer.View);

        // New way
        var newKernel = accl.LoadStreamKernel<ArrayView<int>>(NewKernel);
        ...
        newKernel((<GridDim>, <GroupDim>), buffer.View);
        // Or
        newKernel(new KernelConfig(<GridDim>, <GroupDim>), buffer.View);

        // Or (using dynamic shared memory)
        var sharedMemConfig  = SharedMemoryConfig.RequestDynamic<int>(<GroupDim>);
        newKernel((<GridDim>, <GroupDim>, sharedMemConfig), buffer.View);

        ...
    }
}
```

## Dynamic Shared Memory

Shared memory has long been supported by ILGPU.
However, it was limited to *statically known* allocations which have a known size at compile time of the kernel.
The new ILGPU version allows the use of dynamic shared memory, which can be specified for each kernel launch individually.

Note that this feature is only available with CPU and Cuda accelerators.
OpenCL users can use the **Dynamic Specialization** features to emulate this feature.

```c#
class ...
{
    static void SharedMemKernel(ArrayView<int> data)
    {
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
        using var context = new Context();
        using var accl = new CudaAccelerator(context);

        // Create shared memory configuration using a custom element type.
        // Note that this does not need to be the same type that is used in the scope of the kernel.
        // Therefore, the following two configurations will allocate the same amount of shared memory:
        var config = SharedMemoryConfig.RequestDynamic<byte>(<GroupSize> * sizeof(byte));
        var config2 = SharedMemoryConfig.RequestDynamic<int>(<GroupSize>);

        ...
        // Pass the shared memory configuration to the kernel configuration
        kernel((<GridDim>, <GroupDim>, config), buffer.View);
        ...
    }
}
```

## Dynamic Specialization

Dynamic specialization allows the definition of kernels that will be *specialized/optimized* during runtime.
This allows you to define kernels with *constant values* that are not known at compile time of the kernel or application.
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
        // Generates code that loads <i>c</i> and adds the value <i>2</i> at runtime of the kernel
        data[globalIndex] = c + 2;
    }

    static void SpecializedKernel(ArrayView<int> data, SpecializedValue<int> c)
    {
        var globalIndex = Grid.GlobalIndex.X;
        // Generates code that has an inlined constant value
        data[globalIndex] = c + 2; // Will be specialized for every value <i>c</i>
    }

    static void ...(...)
    {
        using var context = new Context();
        using var accl = new CudaAccelerator(context);

        var genericKernel = accl.LoadStreamKernel<ArrayView<int>, int>(GenericKernel);
        ...
        genericKernel((<GridDim>, <GroupDim>), buffer.View, 40);

        var specializedKernel = accl.LoadStreamKernel<ArrayView<int>, SpecializedValue<int>>(GenericKernel);
        ...
        specializedKernel((<GridDim>, <GroupDim>), buffer.View, SpecializedValue.New(40));
        ...
    }
}
```

## New Grid & Group Properties

The new version adds revised static *Grid* and *Group* properties.
The cumbersome *Index(X|Y|Z)* and *Dimension(X|Y|Z)* properties are now considered obsolete.
The new (much more convenient) properties *Idx(X|Y|Z)* and *Dim(X|Y|Z)* are available to replace the old ones.
Note that the *Index* and *Dimension* properties are still available to accessing all three dimensions of the dispatched *Grid*s and *Group*s.

Please update all uses in your programs, as the old properties will be removed in a future version.

```c#
class ...
{
    static void GenericKernel(ArrayView<int> data, int c)
    {
        var globalIndex = Grid.GlobalIndex.X;
        // Generates code that loads <i>c</i> and adds the value <i>2</i> at runtime of the kernel
        data[globalIndex] = c + 2;
    }

    static void SpecializedKernel(ArrayView<int> data, SpecializedValue<int> c)
    {
        var globalIndex = Grid.GlobalIndex.X;
        // Generates code that has an inlined constant value
        data[globalIndex] = c + 2; // Will be specialized for every value <i>c</i>
    }

    static void ...(...)
    {
        using var context = new Context();
        using var accl = new CudaAccelerator(context);

        var genericKernel = accl.LoadStreamKernel<ArrayView<int>, int>(GenericKernel);
        ...
        genericKernel((<GridDim>, <GroupDim>), buffer.View, 40);

        var specializedKernel = accl.LoadStreamKernel<ArrayView<int>, SpecializedValue<int>>(GenericKernel);
        ...
        specializedKernel((<GridDim>, <GroupDim>), buffer.View, SpecializedValue.New(40));
        ...
    }
}
```

## Unmanaged Type Constraints

The new version leverages the newly available *umanaged* structure type constraint in C# in favor of the *struct* generic type constraint on buffers, view and functions.
This ensures that a certain structure has the same representation in managed and unmanaged memory. This in turn allows ILGPU to generate faster code and simplifies the development process:
The compiler can immediately emit an error message at compile time when a type does not match this particular *unmanaged* type constraint.

In approx. 90% of all cases you do not need to do anything. However, if you use (or instantiate) generic types that should be considered *unmanaged*, you have to enable language version *8.0* of C#.
Please note that it is also safe to do for programs running on .Net 4.7 or .Net 4.8. 