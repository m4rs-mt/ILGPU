// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilerServices.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using ILGPUC.Util;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Initializes arrays.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    private static unsafe ValueReference RuntimeHelpers_InitializeArray(
        ref InvocationContext context)
    {
        var builder = context.Builder;
        var location = context.Location;

        // Resolve the array data
        var handle = context[1].ResolveAs<HandleValue>().AsNotNull();
        var fieldInfo = handle.GetHandle<FieldInfo>();
        var value = fieldInfo.GetValue(null).AsNotNull();
        int valueSize = Marshal.SizeOf(value);

        // Load the associated array data
        byte* data = stackalloc byte[valueSize];
        Marshal.StructureToPtr(value, new IntPtr(data), true);

        // Convert unsafe data into target chunks and emit
        // appropriate store instructions
        Value target = builder.CreateArrayToViewCast(location, context[0]);
        var arrayType = target.Type.As<ViewType>(location);
        var elementType = fieldInfo.FieldType.GetElementType().ThrowIfNull();

        // Convert values to IR values
        int elementSize = elementType.SizeOf();
        for (int i = 0, e = valueSize / elementSize; i < e; ++i)
        {
            byte* address = data + elementSize * i;
            var instance =
                Marshal.PtrToStructure(new IntPtr(address), elementType).AsNotNull();

            // Convert element to IR value
            var irValue = builder.CreateValue(location, instance, elementType);
            var targetIndex = builder.CreatePrimitiveValue(location, i);

            // Store element
            builder.CreateStore(
                location,
                builder.CreateLoadElementAddress(
                    location,
                    target,
                    targetIndex),
                irValue);
        }
        return context.Builder.CreateUndefined();
    }

    /// <summary>
    /// Converts basic reinterpret casts.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    private static ValueReference Unsafe_As(ref InvocationContext context)
    {
        var codeGenerator = context.CodeGenerator;
        var location = context.Location;
        var sourceValue = context[0];
        var methodReturnType = context.TypeContext.CreateType(
            context.Method.GetReturnType()).As<PointerType>(location);

        return sourceValue.Type == methodReturnType || methodReturnType.IsRootType
            ? sourceValue.Resolve()
            : codeGenerator.CreateConversion(
                sourceValue,
                methodReturnType,
                ConvertFlags.None);
    }
}
