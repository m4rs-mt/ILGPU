// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.CodeGeneration;
using ILGPU.Util;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Half = ILGPU.Half;

namespace ILGPUC.Util;

/// <summary>
/// Represents general type extensions.
/// </summary>
static class TypeExtensions
{
    /// <summary>
    /// Determines whether the given type is an intrinsic array type.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True, if the given type is an intrinsic array type.</returns>
    public static bool IsIntrinsicArrayType(this Type type) =>
        type == typeof(Array) || type.IsArray;

    /// <summary>
    /// Returns true if the given basic value type can be used in combination with
    /// a view type.
    /// </summary>
    /// <param name="basicValueType"></param>
    /// <returns>
    /// True if the given value type is a compatible view index type.
    /// </returns>
    public static bool IsViewIndexType(this BasicValueType basicValueType) =>
        Bitwise.Or(
            basicValueType == BasicValueType.Int32,
            basicValueType == BasicValueType.Int64);

    /// <summary>
    /// Checks whether the given type is an array view type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <param name="elementType">
    /// The resolved element type in case of an array view.
    /// </param>
    /// <returns>True, in case of an array view.</returns>
    public static bool IsArrayViewType(
        this Type type,
        [NotNullWhen(true)] out Type? elementType)
    {
        elementType = null;
        if (!type.IsGenericType ||
            type.GetGenericTypeDefinition() != typeof(ArrayView<>))
        {
            return false;
        }
        var args = type.GetGenericArguments();
        elementType = args[0];
        return true;
    }

    /// <summary>
    /// Checks whether the given type is a value tuple type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, in case of a value tuple.</returns>
    public static bool IsValueTuple(this Type type)
    {
        if (type.IsGenericType)
        {
            // NB: System.ValueTuple with 9 or more generic arguments are
            // currently not provided by any of the .NET frameworks. If that
            // ever changes, we will need to update this list.
            var genericType = type.GetGenericTypeDefinition();
            return
                genericType == typeof(ValueTuple<>) ||
                genericType == typeof(ValueTuple<,>) ||
                genericType == typeof(ValueTuple<,,>) ||
                genericType == typeof(ValueTuple<,,,>) ||
                genericType == typeof(ValueTuple<,,,,>) ||
                genericType == typeof(ValueTuple<,,,,,>) ||
                genericType == typeof(ValueTuple<,,,,,,>) ||
                genericType == typeof(ValueTuple<,,,,,,,>);
        }
        return false;
    }

    /// <summary>
    /// Checks whether the given type is an immutable array.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <param name="elementType">The element type (if any).</param>
    /// <returns>True, if the given type is an immutable array.</returns>
    public static bool IsImmutableArray(
        this Type type,
        [NotNullWhen(true)] out Type? elementType)
    {
        elementType = null;
        if (!type.IsGenericType ||
            type.GetGenericTypeDefinition() != typeof(ImmutableArray<>))
        {
            return false;
        }

        elementType = type.GetGenericArguments()[0];
        return true;
    }

    /// <summary>
    /// Returns true if the given type is a delegate type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type is a delegate type.</returns>
    public static bool IsDelegate(this Type type) =>
        type.IsSubclassOf(typeof(Delegate)) || type == typeof(Delegate);

    /// <summary>
    /// Resolves the return type of the given method.
    /// </summary>
    /// <param name="method">The method base.</param>
    /// <returns>The resolved return type.</returns>
    public static Type GetReturnType(this MethodBase method) =>
        method is MethodInfo methodInfo
        ? methodInfo.ReturnType
        : typeof(void);

    /// <summary>
    /// Returns true if the given type is a void pointer.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type is a void pointer.</returns>
    public static bool IsVoidPtr(this Type type) =>
        type == typeof(IntPtr) ||
        type == typeof(UIntPtr) ||
        type.IsPointer && type.GetElementType() == typeof(void);

    /// <summary>
    /// Returns true if the given type represents an unsigned int.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type represents an unsigned int.</returns>
    public static bool IsUnsignedInt(this Type type) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean or
            TypeCode.Byte or
            TypeCode.Char or
            TypeCode.UInt16 or
            TypeCode.UInt32 or
            TypeCode.UInt64 => true,
            _ => false,
        };

    /// <summary>
    /// Returns true if the given type is an ILGPU intrinsic primitive type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>
    /// True, if the given type is an ILGPU intrinsic primitive type.
    /// </returns>
    public static bool IsILGPUPrimitiveType(this Type type) =>
        type.GetBasicValueType() != BasicValueType.None;

    /// <summary>
    /// Resolves the basic-value type for the given managed type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The resolved basic-value type.</returns>
    public static BasicValueType GetBasicValueType(this Type type)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                return BasicValueType.Int1;
            case TypeCode.SByte:
            case TypeCode.Byte:
                return BasicValueType.Int8;
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
                return BasicValueType.Int16;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                return BasicValueType.Int32;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return BasicValueType.Int64;
            case TypeCode.Single:
                return BasicValueType.Float32;
            case TypeCode.Double:
                return BasicValueType.Float64;
            default:
                return type == typeof(Half)
                    ? BasicValueType.Float16
                    : BasicValueType.None;
        }
    }

    /// <summary>
    /// Resolves the basic-value type for the given managed type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The resolved basic-value type.</returns>
    public static ArithmeticBasicValueType GetArithmeticBasicValueType(
        this Type type) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => ArithmeticBasicValueType.UInt1,
            TypeCode.SByte => ArithmeticBasicValueType.Int8,
            TypeCode.Byte => ArithmeticBasicValueType.UInt8,
            TypeCode.Int16 => ArithmeticBasicValueType.Int16,
            TypeCode.UInt16 or TypeCode.Char => ArithmeticBasicValueType.UInt16,
            TypeCode.Int32 => ArithmeticBasicValueType.Int32,
            TypeCode.UInt32 => ArithmeticBasicValueType.UInt32,
            TypeCode.Int64 => ArithmeticBasicValueType.Int64,
            TypeCode.UInt64 => ArithmeticBasicValueType.UInt64,
            TypeCode.Single => ArithmeticBasicValueType.Float32,
            TypeCode.Double => ArithmeticBasicValueType.Float64,
            _ => type == typeof(Half)
                ? ArithmeticBasicValueType.Float16
                : ArithmeticBasicValueType.None
        };

    /// <summary>
    /// Resolves the basic-value type for the given type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <param name="isUnsigned">
    /// True, if the basic value type should be interpreted as unsigned.
    /// </param>
    /// <returns>The resolved basic-value type.</returns>
    public static ArithmeticBasicValueType GetArithmeticBasicValueType(
        this BasicValueType type,
        bool isUnsigned) =>
        type switch
        {
            BasicValueType.Int1 => ArithmeticBasicValueType.UInt1,
            BasicValueType.Int8 => isUnsigned
                ? ArithmeticBasicValueType.UInt8
                : ArithmeticBasicValueType.Int8,
            BasicValueType.Int16 => isUnsigned
                ? ArithmeticBasicValueType.UInt16
                : ArithmeticBasicValueType.Int16,
            BasicValueType.Int32 => isUnsigned
                ? ArithmeticBasicValueType.UInt32
                : ArithmeticBasicValueType.Int32,
            BasicValueType.Int64 => isUnsigned
                ? ArithmeticBasicValueType.UInt64
                : ArithmeticBasicValueType.Int64,
            BasicValueType.Float16 => ArithmeticBasicValueType.Float16,
            BasicValueType.Float32 => ArithmeticBasicValueType.Float32,
            BasicValueType.Float64 => ArithmeticBasicValueType.Float64,
            _ => ArithmeticBasicValueType.None,
        };

    /// <summary>
    /// Returns true if the given basic-value type represents an int.
    /// </summary>
    /// <param name="value">The basic-value type.</param>
    /// <returns>True, if the given basic-value type represents an int.</returns>
    public static bool IsInt(this BasicValueType value) =>
        value switch
        {
            BasicValueType.Int1 or
            BasicValueType.Int8 or
            BasicValueType.Int16 or
            BasicValueType.Int32 or
            BasicValueType.Int64 => true,
            _ => false,
        };

    /// <summary>
    /// Returns true if the given basic-value type represents a float.
    /// </summary>
    /// <param name="value">The basic-value type.</param>
    /// <returns>True, if the given basic-value type represents a float.</returns>
    public static bool IsFloat(this BasicValueType value) =>
        value switch
        {
            BasicValueType.Float16 or
            BasicValueType.Float32 or
            BasicValueType.Float64 => true,
            _ => false,
        };

    /// <summary>
    /// Converts the given type into conversion target flags.
    /// </summary>
    /// <param name="type">The type to convert.</param>
    /// <returns>The required conversion flags.</returns>
    public static ConvertFlags ToTargetUnsignedFlags(this Type type) =>
        type.IsUnsignedInt() ? ConvertFlags.TargetUnsigned : ConvertFlags.None;

    /// <summary>
    /// Computes the size of the given type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    public static int SizeOf<T>() where T : unmanaged => Unsafe.SizeOf<T>();

    /// <summary>
    /// Computes the size of the given type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    public static int SizeOf<T>(T structure) where T : unmanaged => SizeOf<T>();

    private static readonly MethodInfo InteropSizeOfMethod =
        typeof(Interop).GetMethod(
            nameof(SizeOf),
            Type.EmptyTypes,
            null)
        .ThrowIfNull();

    /// <summary>
    /// Computes the size of the given type.
    /// </summary>
    /// <param name="type">The target type</param>
    /// <remarks>Only supports unmanaged types.</remarks>
    public static int SizeOf(this Type type) =>
        (int)InteropSizeOfMethod
        .MakeGenericMethod(type)
        .Invoke(null, null)
        .AsNotNull();

    /// <summary>
    /// Returns true if the given type is an unsigned basic type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the given type is an unsigned basic type.</returns>
    public static bool IsUnsignedBasicType(this Type type) =>
        type == typeof(byte) ||
        type == typeof(ushort) ||
        type == typeof(char) ||
        type == typeof(uint) ||
        type == typeof(ulong) ||
        type == typeof(nuint) ||
        type == typeof(UIntPtr);
}
