// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPULauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;

#pragma warning disable CA1508 // Avoid dead conditional code

namespace ILGPUC.Backends.CPU;

/// <summary>
/// CPU-specific launcher emitter for vectorized CPU execution.
/// Iterates grid/group dimensions and calls KernelEntryPoint directly.
/// Marshals user-facing types (ArrayView, structs with views) to
/// IR types (CPURuntimeView, IR structs) before calling the entry point.
/// </summary>
sealed class CPULauncherEmitter : LauncherEmitter
{
    public override string[] RequiredUsings => [];

    public override bool NeedsKernelArgsStruct => false;

    public override void EmitLaunchBody(
        LauncherEmissionContext ctx,
        List<ParameterInfo> parameters)
    {
        var className = ctx.KernelClassName;
        var pool = ctx.BufferPool;

        // CPU kernels run on the host and access the memory of CPU buffers
        ctx.WriteLine(
            $"stream.VerifyCompiledKernel({ctx.KernelNameLiteral}, " +
            $"{ctx.AcceleratorTypeExpression});");
        ctx.WriteLine("");

        // Grid/group iteration
        ctx.WriteLine("long gridSize = config.GridSize;");
        ctx.WriteLine("int groupSize = config.GroupSize;");
        ctx.WriteLine($"int simdWidth = {className}.SIMDWidth;");
        ctx.WriteLine("");

        // Marshal parameters from user-facing types to IR types.
        // For auto-sized launches (IndexDimensions >= 1), the entry point's
        // first parameter is the thread index — pass baseIndex.
        // For grouped launches (IndexDimensions == 0), there is no index
        // parameter — the kernel reads Grid.GlobalThreadIndex internally.
        // For grouped KernelIndex launches, construct a KernelIndex struct
        // below and pass that instead of a bare int.
        var callArgs = new List<string>();
        if (ctx.KernelIndexTypeName is not null)
            callArgs.Add("_m_kernelIndex");
        else if (ctx.IndexDimensions >= 1)
            callArgs.Add("baseIndex");
        foreach (var param in parameters)
        {
            var marshaledName = EmitParameterMarshal(ctx, param, className);
            callArgs.Add(marshaledName);
        }

        if (callArgs.Count > 1) // has marshaled params
            ctx.WriteLine("");

        // Pre-allocate pooled buffer object outside the kernel loop
        if (pool != null)
        {
            ctx.WriteLine(
                $"var pool = new {className}.BufferPool(simdWidth);");
            ctx.WriteLine("");
        }

        // Every call of the vectorized kernel runs up to simdWidth threads of
        // one group as its SIMD lanes. Implicitly grouped launches run the
        // user's extent in groups of simdWidth lanes with bounds masking for
        // the last group (userExtent may be less than gridSize * groupSize).
        // Explicitly grouped launches run each group of the KernelConfig in
        // calls of up to simdWidth threads. Barriers and shared memory only
        // span the threads of one call, so kernels that synchronize their
        // group need groups that fit into the SIMD lanes.
        var gridIndex = $"{className}.{CPUIntrinsicEmitter.GridIndexName}";
        var gridDimension = $"{className}.{CPUIntrinsicEmitter.GridDimensionName}";
        var groupDimension =
            $"{className}.{CPUIntrinsicEmitter.GroupDimensionName}";
        var groupOffset = $"{className}.{CPUIntrinsicEmitter.GroupOffsetName}";
        if (ctx.Launch.IsGrouped)
        {
            ctx.WriteLine(
                "int maxGroupSize = stream.Accelerator?.MaxNumThreadsPerGroup " +
                "?? groupSize;");
            ctx.WriteLine(
                $"if ({className}.{CPUCodeGenerator.SynchronizesGroupsName})");
            ctx.WriteLine("    maxGroupSize = Math.Min(maxGroupSize, simdWidth);");
            ctx.WriteLine("if (groupSize < 1 || groupSize > maxGroupSize)");
            ctx.WriteLine(
                "    throw new System.InvalidOperationException(" +
                "$\"Invalid group dimensions {groupSize} " +
                "(exceeds maximum {maxGroupSize})\");");
            ctx.WriteLine($"{gridDimension} = (int)gridSize;");
            ctx.WriteLine($"{groupDimension} = groupSize;");
            ctx.WriteLine(
                "long chunksPerGroup = (groupSize + simdWidth - 1) / simdWidth;");
            ctx.WriteLine(
                "for (long _i = 0; _i < gridSize * chunksPerGroup; ++_i)");
            ctx.OpenScope();
            ctx.WriteLine("long _group = _i / chunksPerGroup;");
            ctx.WriteLine("int _offset = (int)(_i % chunksPerGroup) * simdWidth;");
            ctx.WriteLine("int baseIndex = (int)(_group * groupSize) + _offset;");
            ctx.WriteLine(
                "int activeLanes = Math.Min(simdWidth, groupSize - _offset);");
            ctx.WriteLine($"{gridIndex} = (int)_group;");
            ctx.WriteLine($"{groupOffset} = _offset;");
        }
        else
        {
            ctx.WriteLine(
                $"{gridDimension} = " +
                "(int)((userExtent + simdWidth - 1) / simdWidth);");
            ctx.WriteLine($"{groupDimension} = simdWidth;");
            ctx.WriteLine($"{groupOffset} = 0;");
            ctx.WriteLine("for (long _i = 0; _i < userExtent; _i += simdWidth)");
            ctx.OpenScope();
            ctx.WriteLine("int baseIndex = (int)_i;");
            ctx.WriteLine(
                "int activeLanes = (int)Math.Min(simdWidth, userExtent - _i);");
            ctx.WriteLine($"{gridIndex} = (int)(_i / simdWidth);");
        }

        // Clear pooled buffers, re-init lane indices, and set active mask
        if (pool != null)
        {
            ctx.WriteLine("pool.Clear(baseIndex, activeLanes);");
            ctx.WriteLine("");
        }

        // Construct the KernelIndex struct of the group for grouped launches
        // whose entry point takes a KernelIndex. Fields are laid out as
        // { long Field0 = GridIndex, int Field1 = GroupIndex } — matching the
        // frontend's flat struct layout for ILGPU.KernelIndex. The entry point
        // numbers the lanes of the group itself.
        if (ctx.KernelIndexTypeName is not null)
        {
            var kidxType = ctx.KernelIndexTypeName;
            ctx.WriteLine($"var _m_kernelIndex = default({kidxType});");
            ctx.WriteLine("_m_kernelIndex.Field0 = _group;");
            ctx.WriteLine("");
        }

        // Append pool or activeLanes to call arguments
        if (pool != null)
            callArgs.Add("pool");
        else
            callArgs.Add("activeLanes");

        // For multi-dim indices, pass dimension sizes so KernelEntryPoint
        // can decompose linear laneIdx into X/Y/Z components.
        if (ctx.IndexDimensions >= 2)
        {
            callArgs.Add("_dimX");
            callArgs.Add("_dimY");
        }
        if (ctx.IndexDimensions >= 3)
            callArgs.Add("_dimZ");

        ctx.WriteLine(
            $"{className}.KernelEntryPoint({string.Join(", ", callArgs)});");

        ctx.CloseScope(); // flat loop
    }

    /// <summary>
    /// Emits marshaling code for a single parameter and returns the
    /// variable name to pass to KernelEntryPoint.
    /// </summary>
    private static string EmitParameterMarshal(
        LauncherEmissionContext ctx,
        ParameterInfo param,
        string? className)
    {
        // No marshaling needed if entry point uses the same type
        if (param.EntryPointTypeName is null)
            return param.Name;

        switch (param.Kind)
        {
            case ParameterKind.View:
                // ArrayView<TUser> → CPURuntimeView<TIR>
                var elemType = GetElementTypeName(ctx, (ViewType)param.Type);
                var viewVar = $"_m_{param.Name}";
                if (((ViewType)param.Type).ElementType is StructureType)
                {
                    // Struct-element views: the launcher's module and the entry
                    // point's module may use different struct IDs for the same
                    // logical type. Extract the entry point's element type from
                    // its parameter type name (e.g. "CPURuntimeView<struct_1717>"
                    // → "struct_1717"). Create a byte view first (to avoid
                    // depending on the pre-optimization struct ID), then BitCast
                    // to the entry point's element type.
                    var entryViewType = param.EntryPointTypeName
                        ?? $"CPURuntimeView<{elemType}>";
                    // Extract element type: "CPURuntimeView<X>" → "X"
                    const string prefix = "CPURuntimeView<";
                    var entryElemType = entryViewType.StartsWith(
                        prefix,
                        System.StringComparison.Ordinal)
                        ? entryViewType[prefix.Length..^1]
                        : elemType;
                    var baseView = param.BaseViewExpression;
                    ctx.WriteLine(
                        $"var {viewVar} = {baseView}.IsValid" +
                        $" ? Unsafe.BitCast<CPURuntimeView<byte>," +
                        $" CPURuntimeView<{entryElemType}>>" +
                        $"(new CPURuntimeView<byte>((byte*)" +
                        $"Unsafe.AsPointer(ref {baseView}" +
                        $".LoadEffectiveAddress()), {param.Name}.Length))" +
                        $" : default(CPURuntimeView<{entryElemType}>);");
                }
                else
                {
                    ctx.WriteLine(
                        $"var {viewVar} = CPURuntimeView<{elemType}>.FromView(" +
                        $"{param.BaseViewExpression});");
                }
                return viewVar;

            case ParameterKind.StructWithViews:
                return EmitStructMarshal(ctx, param, className);

            case ParameterKind.StructPlain:
                return EmitStructMarshal(ctx, param, className);

            default:
                // Primitives — cast to the entry point type to handle
                // signedness mismatches (e.g., user code has sbyte but
                // IR maps Int8 to byte). The cast is harmless for
                // same-type parameters.

                if (param.EntryPointTypeName is not null)
                {
                    // When the IR decomposes a single-field struct to a
                    // scalar (e.g. LambdaClosure { long Offset } → long),
                    // the user launch type is the struct name but the entry
                    // point type is a primitive. A direct C# cast fails —
                    // use Unsafe.BitCast to reinterpret the memory.
                    if (param.LaunchTypeName != null
                        && param.LaunchTypeName != param.EntryPointTypeName
                        && !IsPrimitiveCSharpType(param.LaunchTypeName))
                    {
                        return $"Unsafe.BitCast<{param.LaunchTypeName}, " +
                               $"{param.EntryPointTypeName}>({param.Name})";
                    }
                    return $"({param.EntryPointTypeName}){param.Name}";
                }

                return param.Name;
        }
    }

    /// <summary>
    /// Emits field-by-field marshaling for a struct parameter containing
    /// view fields. Uses <see cref="ParameterInfo.UserFieldAccessors"/>
    /// to access user struct properties, and converts view fields to
    /// <c>CPURuntimeView&lt;T&gt;</c>.
    /// </summary>
    private static string EmitStructMarshal(
        LauncherEmissionContext ctx,
        ParameterInfo param,
        string? className)
    {
        var structType = (StructureType)param.Type;
        var entryTypeName = param.EntryPointTypeName!;

        // Int128, UInt128 and decimal values have the layout of their kernel
        // structures, whose fields are not accessible to the launcher
        if (param.LaunchTypeName is "System.Int128" or "System.UInt128" or
            "decimal" or "System.Decimal")
        {
            return $"Unsafe.BitCast<{param.LaunchTypeName}, {entryTypeName}>" +
                $"({param.Name})";
        }

        var accessors = param.UserFieldAccessors;
        var marshaledVar = $"_m_{param.Name}";

        // The struct type is emitted at the same scope level as the
        // Launch method (inside the CompiledKernel class), so no
        // additional qualification is needed.
        var qualifiedType = entryTypeName;

        ctx.WriteLine($"var {marshaledVar} = default({qualifiedType});");

        // Padding fields that fill explicitly sized structures have no user
        // accessor, so user accessors are counted separately
        int accessorIndex = 0;
        for (int i = 0; i < structType.NumFields; i++)
        {
            var fieldType = structType.Fields[i];
            var irFieldName = StructureType.GetFieldName(i);
            int userIndex = fieldType is PaddingType ? -1 : accessorIndex++;
            var userAccessor = accessors is not null && userIndex >= 0
                && userIndex < accessors.Length
                ? $"{param.Name}.{accessors[userIndex]}"
                : $"{param.Name}.{irFieldName}";

            if (fieldType is ViewType vt)
            {
                // ArrayView<TUser> → CPURuntimeView<TIR>
                var elemName = GetElementTypeName(ctx, vt);
                if (vt.ElementType is StructureType)
                {
                    // Struct-element views: user and IR struct types differ
                    // in name but have identical memory layout. Write the
                    // raw {pointer, length} pair into the struct field
                    // using Unsafe byte manipulation.
                    var tmpView = $"_tmpv_{irFieldName}";
                    ctx.WriteLine(
                        $"if ({userAccessor}.IsValid)");
                    ctx.OpenScope();
                    ctx.WriteLine(
                        $"var {tmpView} = new CPURuntimeView<byte>(" +
                        $"(byte*)Unsafe.AsPointer(ref {userAccessor}" +
                        $".LoadEffectiveAddress()), {userAccessor}.Length);");
                    // Copy 16 bytes (sizeof CPURuntimeView) from tmpView
                    // to the field location in the marshaled struct.
                    // Field0 at offset 0 for Sequential layout.
                    ctx.WriteLine(
                        $"Unsafe.CopyBlockUnaligned(" +
                        $"ref Unsafe.As<{qualifiedType}, byte>(ref {marshaledVar})," +
                        $" ref Unsafe.As<CPURuntimeView<byte>, byte>(ref {tmpView})," +
                        $" (uint)Unsafe.SizeOf<CPURuntimeView<byte>>());");
                    ctx.CloseScope();
                }
                else
                {
                    ctx.WriteLine(
                        $"{marshaledVar}.{irFieldName} = " +
                        $"CPURuntimeView<{elemName}>.FromView(" +
                        $"{userAccessor});");
                }
            }
            else if (fieldType is StructureType nested &&
                nested.HasFlags(TypeFlags.ViewDependent))
            {
                // Nested struct with views — recursive (future extension)
                ctx.WriteLine(
                    $"{marshaledVar}.{irFieldName} = {userAccessor};");
            }
            else if (fieldType is PaddingType padType)
            {
                // Padding fields come from decomposed zero-size structs
                // (e.g., Stride1D.Dense → byte). Use default.
                var primName = GetPrimitiveTypeName(padType, ctx);
                ctx.WriteLine(
                    $"{marshaledVar}.{irFieldName} = default({primName});");
            }
            else
            {
                // Primitive fields — assignment via conversions (e.g.,
                // LongIndex1D → long), cast to the IR type, which differs
                // from the user field type in signedness (e.g., sbyte and
                // ushort fields map to byte and short).
                // For view-containing structs, skip small fields that
                // come from zero-size stride types (Stride1D.Dense → byte)
                // since the user accessor type doesn't match the IR type.
                if (param.Kind == ParameterKind.StructWithViews
                    && fieldType is PrimitiveType pt && pt.Size <= 2)
                    continue;
                var value = fieldType is PrimitiveType
                    ? $"unchecked(({GetPrimitiveTypeName(fieldType, ctx)})" +
                        $"{userAccessor})"
                    : userAccessor;
                ctx.WriteLine($"{marshaledVar}.{irFieldName} = {value};");
            }
        }

        return marshaledVar;
    }

    /// <summary>
    /// Returns the C# element type name of the runtime view of a view type:
    /// pointers are stored as addresses.
    /// </summary>
    private static string GetElementTypeName(
        LauncherEmissionContext ctx,
        ViewType viewType) =>
        viewType.ElementType is PointerType
            ? "nint"
            : ctx.GetElementTypeName(viewType);

    /// <summary>
    /// Returns the C# type name for a primitive IR type.
    /// </summary>
    private static string GetPrimitiveTypeName(
        TypeValue type,
        LauncherEmissionContext ctx)
    {
        return ctx.GetPrimitiveTypeName(type);
    }

    /// <summary>
    /// Returns true if the given type name is a C# primitive / built-in
    /// numeric type that supports direct casts between each other.
    /// </summary>
    private static bool IsPrimitiveCSharpType(string typeName) =>
        typeName is "bool" or "byte" or "sbyte"
            or "short" or "ushort" or "int" or "uint"
            or "long" or "ulong" or "float" or "double"
            or "System.Half" or "nint" or "nuint" or "char"
            or "decimal";
}

#pragma warning restore CA1508 // Avoid dead conditional code
