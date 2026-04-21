// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilerServices.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
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
    private static unsafe Value? RuntimeHelpers_InitializeArray(
        ref InvocationContext context)
    {
        var builder = context.Builder;
        var location = context.Location;

        // Resolve the array data
        var handle = context[1].AsNotNullCast<HandleValue>();
        var fieldInfo = handle.GetHandle<FieldInfo>();
        var value = fieldInfo.GetValue(null).AsNotNull();
        int valueSize = Marshal.SizeOf(value);

        // Load the associated array data
        byte* data = stackalloc byte[valueSize];
        Marshal.StructureToPtr(value, new IntPtr(data), true);

        // Convert unsafe data into target chunks and emit
        // appropriate store instructions
        Value target = builder.CreateArrayToViewCast(location, context[0]).AsNotNull();
        var arrayType = target.Type.AsNotNullCast<ViewType>();
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
        return context.Builder.UndefinedValue;
    }

    /// <summary>
    /// Converts basic reinterpret casts.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    private static Value? Unsafe_As(ref InvocationContext context)
    {
        var location = context.Location;
        var sourceValue = context[0];
        var methodReturnType = context.ModuleBuilder.CreateType(
            context.Method.GetReturnType()).AsNotNullCast<PointerType>();

        if (sourceValue.Type == methodReturnType)
            return sourceValue;

        // Pointer-to-pointer reinterpret: use PointerCast to change element type
        if (sourceValue.Type is PointerType && methodReturnType is PointerType targetPtr)
            return context.Builder.CreatePointerCast(
                location,
                sourceValue,
                targetPtr.ElementType);

        return context.Builder.CreateConvert(
            location,
            sourceValue,
            methodReturnType,
            ConvertFlags.None);
    }

    /// <summary>
    /// Handles <c>System.Runtime.CompilerServices.Unsafe.Add&lt;T&gt;(
    /// ref T source, int elementOffset)</c>.
    /// <para>Primary use case: element access on <c>[InlineArray(N)]</c>
    /// structs (e.g. <c>buffer[2]</c> is compiled to
    /// <c>Unsafe.Add&lt;int&gt;(ref _element0, 2)</c>). Without this handler
    /// the frontend emits an un-lowered <c>MethodCall</c> to
    /// <c>Unsafe.Add</c> which has no loadable body → undefined method
    /// reference in the generated CPU kernel.</para>
    /// </summary>
    private static Value? Unsafe_Add(ref InvocationContext context)
    {
        var location = context.Location;
        var source = context[0];
        var offset = context[1];
        return context.Builder.CreateLoadElementAddress(
            location, source, offset);
    }
}
