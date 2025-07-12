﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
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
using ILGPUC.IR.Values;
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
    /// Resolves the actual IR name.
    /// </summary>
    /// <param name="attribute">The parent attribute.</param>
    /// <param name="method">The source method.</param>
    /// <returns>The IR name.</returns>
    public static string GetName(this ExternalAttribute attribute, MethodInfo method) =>
        string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name;

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
    /// Returns true if the given type has a supported base class.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type has a supported base class.</returns>
    internal static bool HasSupportedBaseClass(this Type type) =>
        !type.IsValueType && type.BaseType != typeof(object);

    /// <summary>
    /// Returns true if the given type is a delegate type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type is a delegate type.</returns>
    public static bool IsDelegate(this Type type) =>
        type.IsSubclassOf(typeof(Delegate)) || type == typeof(Delegate);

    /// <summary>
    /// Resolves the delegate invocation method of the given type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The resolved delegate invocation method.</returns>
    public static MethodInfo? GetDelegateInvokeMethod(this Type type)
    {
        const string InvokeMethodName = "Invoke";
        return !type.IsDelegate()
            ? null
            : type.GetMethod(
                InvokeMethodName, BindingFlags.Public | BindingFlags.Instance);
    }

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
    /// Returns true if the given type is passed via reference.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type is passed via reference.</returns>
    internal static bool IsPassedViaPtr(this Type type) =>
        !type.IsValueType &&
        !type.IsArray &&
        !type.IsTreatedAsPtr() &&
        type != typeof(string);

    /// <summary>
    /// Returns true if the given type is treated as a pointer type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type is treated as a pointer type.</returns>
    internal static bool IsTreatedAsPtr(this Type type) =>
        type.IsPointer || type.IsByRef;

    /// <summary>
    /// Returns true if the given type represents a signed int.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type represents a signed int.</returns>
    public static bool IsSignedInt(this Type type) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.SByte or
            TypeCode.Int16 or
            TypeCode.Int32 or
            TypeCode.Int64 => true,
            _ => false,
        };

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
    /// Resolves the managed type for the given basic-value type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The resolved managed type.</returns>
    public static Type? GetManagedType(this BasicValueType type) =>
        type switch
        {
            BasicValueType.Int1 => typeof(bool),
            BasicValueType.Int8 => typeof(byte),
            BasicValueType.Int16 => typeof(short),
            BasicValueType.Int32 => typeof(int),
            BasicValueType.Int64 => typeof(long),
            BasicValueType.Float16 => typeof(Half),
            BasicValueType.Float32 => typeof(float),
            BasicValueType.Float64 => typeof(double),
            _ => null,
        };

    /// <summary>
    /// Resolves the managed type for the given basic-value type.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The resolved managed type.</returns>
    public static Type GetManagedType(this ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.UInt1 => typeof(bool),
            ArithmeticBasicValueType.Int8 => typeof(byte),
            ArithmeticBasicValueType.Int16 => typeof(short),
            ArithmeticBasicValueType.Int32 => typeof(int),
            ArithmeticBasicValueType.Int64 => typeof(long),
            ArithmeticBasicValueType.Float16 => typeof(Half),
            ArithmeticBasicValueType.Float32 => typeof(float),
            ArithmeticBasicValueType.Float64 => typeof(double),
            ArithmeticBasicValueType.UInt8 => typeof(byte),
            ArithmeticBasicValueType.UInt16 => typeof(short),
            ArithmeticBasicValueType.UInt32 => typeof(int),
            ArithmeticBasicValueType.UInt64 => typeof(long),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

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
            TypeCode.UInt16 => ArithmeticBasicValueType.UInt16,
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
    /// <returns>The resolved basic-value type.</returns>
    public static BasicValueType GetBasicValueType(
        this ArithmeticBasicValueType type)
    {
        switch (type)
        {
            case ArithmeticBasicValueType.UInt1:
                return BasicValueType.Int1;
            case ArithmeticBasicValueType.Int8:
            case ArithmeticBasicValueType.UInt8:
                return BasicValueType.Int8;
            case ArithmeticBasicValueType.Int16:
            case ArithmeticBasicValueType.UInt16:
                return BasicValueType.Int16;
            case ArithmeticBasicValueType.Int32:
            case ArithmeticBasicValueType.UInt32:
                return BasicValueType.Int32;
            case ArithmeticBasicValueType.Int64:
            case ArithmeticBasicValueType.UInt64:
                return BasicValueType.Int64;
            case ArithmeticBasicValueType.Float16:
                return BasicValueType.Float16;
            case ArithmeticBasicValueType.Float32:
                return BasicValueType.Float32;
            case ArithmeticBasicValueType.Float64:
                return BasicValueType.Float64;
            default:
                return BasicValueType.None;
        }
    }

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
    /// Forces the given basic type to 32 bits.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The limited arithmetic basic value.</returns>
    public static ArithmeticBasicValueType ForceTo32Bit(
        this ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.UInt1 => ArithmeticBasicValueType.UInt32,
            ArithmeticBasicValueType.UInt8 => ArithmeticBasicValueType.UInt32,
            ArithmeticBasicValueType.UInt16 => ArithmeticBasicValueType.UInt32,
            ArithmeticBasicValueType.UInt32 => ArithmeticBasicValueType.UInt32,
            ArithmeticBasicValueType.UInt64 => ArithmeticBasicValueType.UInt32,

            ArithmeticBasicValueType.Int8 => ArithmeticBasicValueType.Int32,
            ArithmeticBasicValueType.Int16 => ArithmeticBasicValueType.Int32,
            ArithmeticBasicValueType.Int32 => ArithmeticBasicValueType.Int32,
            ArithmeticBasicValueType.Int64 => ArithmeticBasicValueType.Int32,

            ArithmeticBasicValueType.Float16 => ArithmeticBasicValueType.Float32,
            ArithmeticBasicValueType.Float32 => ArithmeticBasicValueType.Float32,
            ArithmeticBasicValueType.Float64 => ArithmeticBasicValueType.Float32,
            _ => ArithmeticBasicValueType.None,
        };

    /// <summary>
    /// Limits the given basic type to 32 bits.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The limited arithmetic basic value.</returns>
    public static ArithmeticBasicValueType LimitTo32Bit(
        this ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int64 => ArithmeticBasicValueType.Int32,
            ArithmeticBasicValueType.UInt64 => ArithmeticBasicValueType.UInt32,
            ArithmeticBasicValueType.Float64 => ArithmeticBasicValueType.Float32,
            _ => type,
        };

    /// <summary>
    /// Forces the given basic type to 64 bits.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The limited arithmetic basic value.</returns>
    public static ArithmeticBasicValueType ForceTo64Bit(
        this ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int8 or
            ArithmeticBasicValueType.Int16 or
            ArithmeticBasicValueType.Int32 => ArithmeticBasicValueType.Int64,
            ArithmeticBasicValueType.UInt1 or
            ArithmeticBasicValueType.UInt8 or
            ArithmeticBasicValueType.UInt16 or
            ArithmeticBasicValueType.UInt32 => ArithmeticBasicValueType.UInt64,
            ArithmeticBasicValueType.Float16 or
            ArithmeticBasicValueType.Float32 => ArithmeticBasicValueType.Float64,
            _ => type,
        };

    /// <summary>
    /// Returns true if the given type represents an int.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type represents an int.</returns>
    public static bool IsInt(this Type type) =>
        type.GetBasicValueType().IsInt();

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
    /// Returns true if the given type represents a float.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>True, if the given type represents a float.</returns>
    public static bool IsFloat(this Type type) =>
        type.GetBasicValueType().IsFloat();

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
    /// Returns true if the given arithmetic basic value type represents a float.
    /// </summary>
    /// <param name="value">The arithmetic basic value type.</param>
    /// <returns>
    /// True, if the given arithmetic basic value represents a float.
    /// </returns>
    public static bool IsFloat(this ArithmeticBasicValueType value) =>
        value switch
        {
            ArithmeticBasicValueType.Float16 or
            ArithmeticBasicValueType.Float32 or
            ArithmeticBasicValueType.Float64 => true,
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
        type == typeof(uint) ||
        type == typeof(ulong) ||
        type == typeof(nuint) ||
        type == typeof(UIntPtr);
}
