// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Group.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Linq;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles group barrier operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_Barrier(ref InvocationContext context) =>
        context.Builder.CreateBarrier(context.Location, BarrierKind.GroupLevel);

    /// <summary>
    /// Handles group barrier pop-count operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_BarrierPopCount(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.GroupLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.PopCount);

    /// <summary>
    /// Handles group barrier and operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_BarrierAnd(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.GroupLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.And);

    /// <summary>
    /// Handles group barrier or operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_BarrierOr(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.GroupLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.Or);

    /// <summary>
    /// Handles group broadcast operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_Broadcast(ref InvocationContext context)
    {
        var builder = context.Builder;
        var location = context.Location;

        var parameters = context.Method.GetParameters();
        var sourceLane = parameters[0].ParameterType == typeof(FirstLaneValue<>)
            ? builder.CreatePrimitiveValue(location, 0)
            : builder.CreateArithmetic(
                location,
                builder.CreateGroupDimensionValue(location),
                builder.CreatePrimitiveValue(location, 1),
                BinaryArithmeticKind.Sub);

        return builder.CreateBroadcast(
            location,
            context.Pull(),
            sourceLane,
            BroadcastKind.GroupLevel);
    }

    /// <summary>
    /// Handles group specialized broadcast operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_BroadcastFromThread(ref InvocationContext context) =>
        context.Builder.CreateBroadcast(
            context.Location,
            context.Pull(),
            context.Pull(),
            BroadcastKind.GroupLevel);

    /// <summary>
    /// Handles group dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_Dimension(ref InvocationContext context) =>
        context.Builder.CreateGroupDimensionValue(context.Location);

    /// <summary>
    /// Handles group index operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_Index(ref InvocationContext context) =>
        context.Builder.CreateGroupIndexValue(context.Location);

    /// <summary>
    /// Handles group memory fence operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_MemoryFence(ref InvocationContext context) =>
        context.Builder.CreateMemoryBarrier(
            context.Location,
            MemoryBarrierKind.GroupLevel);

    /// <summary>
    /// Handles group local memory buffer operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetLocalMemoryBuffer(ref InvocationContext context)
    {
        var builder = context.ModuleBuilder;

        // Create local allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var length = context.Pull();
        var alloca = builder.CreateGlobal(
            context.Location,
            builder.CreateType(allocaType),
            MemoryAddressSpace.Local,
            length);

        // Build view using the same length
        return context.Builder.CreateNewView(
            context.Location,
            alloca,
            length);
    }

    /// <summary>
    /// Handles group shared memory element operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryElement(ref InvocationContext context)
    {
        var builder = context.ModuleBuilder;

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        return builder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared);
    }

    /// <summary>
    /// Handles group shared memory buffer operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryBuffer(ref InvocationContext context)
    {
        var builder = context.ModuleBuilder;

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var length = context.Pull();

        // Backends require a compile-time constant extent for shared-memory
        // allocations (see LowerGroupCollectives comment on the scratch-slot
        // path). If the length is runtime-dependent (e.g. derived from a
        // kernel parameter), ModuleBuilder.CreateGlobal silently returns
        // null and the frontend pushes an UndefinedValue whose type is
        // KindType — that then fails at the next `stloc`'s convert with an
        // opaque `BasicValueType.None` assertion. Detect the unsupported
        // shape here and raise a clear diagnostic instead.
        if (!IsCompileTimeConstant(length))
        {
            throw context.Location.GetNotSupportedException(
                "Group.GetSharedMemory<T>(int) requires a compile-time "
                + "constant extent. Expressions involving kernel parameters "
                + "or other runtime values are not supported — declare the "
                + "extent as a `const int` or a literal.");
        }

        var alloca = builder.CreateGlobal(
            context.Location,
            builder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            length);

        // Build view using the same length
        return context.Builder.CreateNewView(
            context.Location,
            alloca,
            length);
    }

    /// <summary>
    /// Handles <see cref="Group.GetSharedMemory2D{T, TStride}(Index2D)"/>. The
    /// runtime method is intercepted at the intrinsic boundary so the backend
    /// never has to compile its generic body (static-abstract <c>FromExtent</c>
    /// dispatch + extension-method <c>As2DView</c> chaining that does not round
    /// trip through source emission). The handler allocates a compile-time
    /// sized shared buffer, wraps it as a 1D view, computes the stride fields
    /// that <c>TStride.FromExtent(extent)</c> would produce, and assembles an
    /// <see cref="ArrayView2D{T, TStride}"/> struct directly in IR.
    /// </summary>
    private static Value? Group_GetSharedMemory2D(ref InvocationContext context)
    {
        var moduleBuilder = context.ModuleBuilder;
        var builder = context.Builder;
        var location = context.Location;

        var generics = context.GetMethodGenericArguments();
        var elementCLRType = generics[0];
        var strideCLRType = generics[1];

        // Pull the Index2D extent argument.
        //
        // `new Index2D(X, Y)` at the IL call site emits a newobj whose
        // frontend lowering (CodeGenerator.MakeNewObject) allocates a temp
        // via `CreateTempAlloca`, stores a null struct, invokes the ctor
        // against the alloca pointer, then pushes a `Load(alloca)`. So at
        // this point `extent` is a Load — trace it back through the ctor
        // call to recover the literal X / Y argument values. That is the
        // only compile-time-constant shape supported here — backends cannot
        // emit a static shared-memory allocation with a runtime-derived
        // extent.
        var extent = context.Pull();
        if (!TryResolveIndex2DCtorLiteral(
                builder, extent, out var extentX, out var extentY) ||
            !IsCompileTimeConstant(extentX) ||
            !IsCompileTimeConstant(extentY))
        {
            throw location.GetNotSupportedException(
                "Group.GetSharedMemory2D<T, TStride>(Index2D) requires a "
                + "compile-time constant extent. Expressions involving "
                + "kernel parameters or other runtime values are not "
                + "supported — pass a `new Index2D(N, M)` literal with "
                + "const N and M.");
        }

        // totalLength = extent.X * extent.Y (as int — matches what the
        // inline wrapper `stride.ComputeBufferLength(extent)` produces for
        // every concrete Stride2D with YStride/XStride == 1 on one axis).
        var totalLength = builder.CreateArithmetic(
            location, extentX, extentY, BinaryArithmeticKind.Mul);

        // Allocate the shared buffer and build the backing 1D view.
        var alloca = moduleBuilder.CreateGlobal(
            location,
            moduleBuilder.CreateType(elementCLRType),
            MemoryAddressSpace.Shared,
            totalLength);
        var sharedBaseView = builder.CreateNewView(
            location, alloca, totalLength);

        // Convert extents to long for the LongIndex2D Extent field.
        var extentXLong = builder.CreateConvertToInt64(location, extentX)
            .AsNotNull();
        var extentYLong = builder.CreateConvertToInt64(location, extentY)
            .AsNotNull();

        // Resolve the runtime ArrayView2D<T, TStride> structure type so the
        // builder enforces the expected flat field layout. The default
        // (generic) address space is what downstream users of the struct
        // expect — local variables / kernel reads are always typed against
        // the generic form, so we cast the shared view to generic before
        // embedding.
        var arrayView2DCLRType = typeof(ArrayView2D<,>)
            .MakeGenericType(elementCLRType, strideCLRType);
        var structType =
            (StructureType)moduleBuilder.CreateType(arrayView2DCLRType);
        var baseViewType = structType.Fields[0].As<ViewType>();
        var baseView = builder.CreateAddressSpaceCast(
            location, sharedBaseView, baseViewType.AddressSpace);

        var sb = builder.CreateStructure(location, structType);
        sb.Add(baseView);
        sb.Add(extentXLong);
        sb.Add(extentYLong);
        AddStride2DFields(ref sb, strideCLRType, extentX, extentY, location);
        return sb.Seal();
    }

    /// <summary>
    /// Traces an <see cref="Index2D"/> value back to a
    /// <c>new Index2D(x, y)</c> constructor invocation and recovers the two
    /// primitive argument values that the ctor received. Matches the exact
    /// pattern that <see cref="CodeGenerator.MakeNewObject"/> emits for a
    /// struct newobj: an <see cref="Alloca"/> followed by a
    /// <see cref="MethodCall"/> whose first argument is that alloca and
    /// whose target is the two-int Index2D ctor.
    /// </summary>
    /// <returns>
    /// <c>true</c> and sets <paramref name="x"/>/<paramref name="y"/> on a
    /// match; <c>false</c> otherwise. The returned values have not yet been
    /// checked for compile-time constancy — callers must verify.
    /// </returns>
    private static bool TryResolveIndex2DCtorLiteral(
        IR.BasicBlockValues.Construction.BasicBlockBuilder builder,
        Value? extent,
        out Value x,
        out Value y)
    {
        x = y = null!;

        if (extent is not Load load) return false;

        // The ctor was invoked with a pointer argument that the frontend may
        // have wrapped in an address-space cast between the alloca and the
        // call site — unwrap to compare.
        var loadSource = UnwrapAddressSpaceCast(load.Source);
        if (loadSource is not Alloca alloca) return false;

        // Walk the linked list of values already emitted into the current
        // basic-block builder (back-to-front). We're looking for the
        // MethodCall to the Index2D(int, int) constructor whose 'this'
        // pointer is the same alloca.
        for (var cursor = builder.Last; cursor is not null; cursor = cursor.Previous)
        {
            if (cursor is not MethodCall call) continue;
            if (call.Arguments.Length != 3) continue;

            var callInstance = UnwrapAddressSpaceCast(call.Arguments[0]);
            if (!ReferenceEquals(callInstance, alloca)) continue;

            if (call.Target.Source is not System.Reflection.ConstructorInfo ctor
                || ctor.DeclaringType != typeof(Index2D))
                continue;

            x = call.Arguments[1];
            y = call.Arguments[2];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Unwraps any <see cref="AddressSpaceCast"/> layers around a pointer
    /// value to expose the underlying allocation / global.
    /// </summary>
    private static Value? UnwrapAddressSpaceCast(Value? value)
    {
        while (value is AddressSpaceCast cast)
            value = cast.Source;
        return value;
    }

    /// <summary>
    /// Appends the flattened field values that <c>TStride.FromExtent(extent)</c>
    /// would produce for each supported concrete <see cref="IStride2D{TSelf}"/>.
    /// </summary>
    private static void AddStride2DFields(
        ref StructureValue.Builder sb,
        Type strideCLRType,
        Value extentX,
        Value extentY,
        Location location)
    {
        // DenseX.FromExtent(extent) = new(extent.X)            → {YStride = extent.X}
        // DenseY.FromExtent(extent) = new(extent.Y)            → {XStride = extent.Y}
        // General.FromExtent(extent) = new(extent)             → {StrideExtent = extent}
        // Infinite has no instance fields.
        if (strideCLRType == typeof(Stride2D.DenseX))
        {
            sb.Add(extentX);
        }
        else if (strideCLRType == typeof(Stride2D.DenseY))
        {
            sb.Add(extentY);
        }
        else if (strideCLRType == typeof(Stride2D.General))
        {
            // General wraps an Index2D, which flattens to two ints.
            sb.Add(extentX);
            sb.Add(extentY);
        }
        else if (strideCLRType == typeof(Stride2D.Infinite))
        {
            // No fields to add.
        }
        else
        {
            throw location.GetNotSupportedException(
                $"Group.GetSharedMemory2D does not support stride type "
                + $"'{strideCLRType.FullName}'. Supported types: "
                + $"Stride2D.DenseX, Stride2D.DenseY, Stride2D.General, "
                + $"Stride2D.Infinite.");
        }
    }

    /// <summary>
    /// Returns true if the given value is a compile-time constant — either
    /// a primitive literal or a pure-value tree whose leaves are all
    /// primitives (no parameters / method values).
    /// </summary>
    private static bool IsCompileTimeConstant(Value? value)
    {
        if (value is null) return false;
        if (value is PrimitiveValue) return true;
        if (value is not PureValue pureValue) return false;
        bool allPure = true;
        pureValue.VisitDFS(childValue =>
        {
            if (childValue is not PureValue)
                allPure = false;
        });
        return allPure;
    }

    /// <summary>
    /// Handles group shared element buffer-per-thread operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryElementPerThread(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var groupDimension = builder.CreateGroupDimensionValue(context.Location);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = context.ModuleBuilder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            groupDimension);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            groupDimension);
    }

    /// <summary>
    /// Handles group shared element buffer-per-thread operations receiving a multiplier.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryElementPerThreadMultiplier(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var groupDimension = builder.CreateGroupDimensionValue(context.Location);

        // Get multiplier and apply it
        var multiplier = context.Pull();
        var dimension = builder.CreateArithmetic(
            context.Location,
            groupDimension,
            multiplier,
            BinaryArithmeticKind.Mul);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = context.ModuleBuilder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            dimension);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            dimension);
    }

    /// <summary>
    /// Handles group shared element buffer-per-thread operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryElementPerWarp(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var warpDimension = builder.CreateSubGroupDimensionValue(context.Location);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = context.ModuleBuilder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            warpDimension);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            warpDimension);
    }

    /// <summary>
    /// Handles group reduce/all-reduce operations with lambda binary operations.
    /// Always emits a custom <see cref="GroupReduce"/>; operation recognition
    /// happens later in <see cref="IR.Transformations.RecognizeCollectiveOps"/>.
    /// </summary>
    private static Value? Group_Reduce(
        ref InvocationContext context,
        GroupReduceKind kind = GroupReduceKind.Reduce)
    {
        var data = context.Pull();
        var identity = context.Pull();
        var methodBase = context.PullDelegateMethodBase();
        var method = methodBase is not null
            ? context.CodeGenerator.GetMethod(methodBase)
            : null;
        return context.Builder.CreateGroupReduce(
            context.Location, data, (Value?)method, kind, identity);
    }

    /// <summary>
    /// Handles group scan operations with lambda binary operations.
    /// Always emits a custom <see cref="GroupScan"/>; operation recognition
    /// happens later in <see cref="IR.Transformations.RecognizeCollectiveOps"/>.
    /// </summary>
    private static Value? Group_Scan(
        ref InvocationContext context,
        GroupScanKind kind = GroupScanKind.Inclusive)
    {
        var data = context.Pull();
        var identity = context.Pull();
        var methodBase = context.PullDelegateMethodBase();
        var method = methodBase is not null
            ? context.CodeGenerator.GetMethod(methodBase)
            : null;
        return context.Builder.CreateGroupScan(
            context.Location,
            data,
            (Value?)method,
            kind,
            identity);
    }

    /// <summary>
    /// Handles group radix sort operations.
    /// Resolves IRadixSortOperation static abstract members via reflection
    /// at frontend time, since the concrete operation type is fully known.
    /// </summary>
    private static Value? Group_RadixSort(ref InvocationContext context)
    {
        var data = context.Pull();

        // Get generic type arguments: [T, TRadixSortOperation]
        var operationType = context.GetMethodGenericArguments()[1];

        // Resolve NumBits (static abstract int property)
        var numBits = (int)operationType
            .GetProperty(
                "NumBits",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

        // Resolve DefaultValue (static abstract T property)
        var defaultValueObj = operationType
            .GetProperty(
                "DefaultValue",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        var defaultValue = context.Builder.CreatePrimitiveValue(
            context.Location,
            defaultValueObj);

        // Resolve ExtractRadixBits method
        var extractMethodInfo = operationType.GetMethod(
            "ExtractRadixBits",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static)!;
        var extractMethod = context.CodeGenerator.GetMethod(extractMethodInfo);

        return context.Builder.CreateGroupRadixSort(
            context.Location,
            data,
            numBits,
            defaultValue,
            extractMethod);
    }
}
