﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: TypeExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Reflection;
using System.Text;

namespace ILGPU.Util
{
    /// <summary>
    /// Represents general type extensions.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Checks whether the given type is an array view type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="elementType">The resolved element type in case of an array view.</param>
        /// <returns>True, in case of an array view.</returns>
        public static bool IsArrayViewType(this Type type, out Type elementType)
        {
            elementType = null;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ArrayView<>))
                return false;
            var args = type.GetGenericArguments();
            elementType = args[0];
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
        public static MethodInfo GetDelegateInvokeMethod(this Type type)
        {
            const string InvokeMethodName = "Invoke";
            if (!type.IsDelegate())
                return null;
            return type.GetMethod(InvokeMethodName, BindingFlags.Public | BindingFlags.Instance);
        }

        /// <summary>
        /// Resolves the return type of the given method.
        /// </summary>
        /// <param name="method">The method base.</param>
        /// <returns>The resolved return type.</returns>
        public static Type GetReturnType(this MethodBase method)
        {
            if (method is MethodInfo methodInfo)
                return methodInfo.ReturnType;
            return typeof(void);
        }

        /// <summary>
        /// Returns true if the given type is a void pointer.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>True, if the given type is a void pointer.</returns>
        public static bool IsVoidPtr(this Type type)
        {
            return type == typeof(IntPtr) ||
                type == typeof(UIntPtr) ||
                (type.IsPointer && type.GetElementType() == typeof(void));
        }

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
        /// Returns true iff the given type represents a signed int.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>True, iff the given type represents a signed int.</returns>
        public static bool IsSignedInt(this Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true iff the given type represents an unsigned int.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>True, iff the given type represents an unsigned int.</returns>
        public static bool IsUnsignedInt(this Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Resolves the managed type for the given basic-value type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The resolved managed type.</returns>
        public static Type GetManagedType(this BasicValueType type)
        {
            switch (type)
            {
                case BasicValueType.Int1:
                    return typeof(bool);
                case BasicValueType.Int8:
                    return typeof(byte);
                case BasicValueType.Int16:
                    return typeof(short);
                case BasicValueType.Int32:
                    return typeof(int);
                case BasicValueType.Int64:
                    return typeof(long);
                case BasicValueType.Float32:
                    return typeof(float);
                case BasicValueType.Float64:
                    return typeof(double);
                default:
                    return null;
            }
        }

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
                    return BasicValueType.None;
            }
        }

        /// <summary>
        /// Resolves the basic-value type for the given managed type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The resolved basic-value type.</returns>
        public static ArithmeticBasicValueType GetArithmeticBasicValueType(this Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return ArithmeticBasicValueType.UInt1;
                case TypeCode.SByte:
                    return ArithmeticBasicValueType.Int8;
                case TypeCode.Byte:
                    return ArithmeticBasicValueType.UInt8;
                case TypeCode.Int16:
                    return ArithmeticBasicValueType.Int16;
                case TypeCode.UInt16:
                    return ArithmeticBasicValueType.UInt16;
                case TypeCode.Int32:
                    return ArithmeticBasicValueType.Int32;
                case TypeCode.UInt32:
                    return ArithmeticBasicValueType.UInt32;
                case TypeCode.Int64:
                    return ArithmeticBasicValueType.Int64;
                case TypeCode.UInt64:
                    return ArithmeticBasicValueType.UInt64;
                case TypeCode.Single:
                    return ArithmeticBasicValueType.Float32;
                case TypeCode.Double:
                    return ArithmeticBasicValueType.Float64;
                default:
                    return ArithmeticBasicValueType.None;
            }
        }

        /// <summary>
        /// Resolves the basic-value type for the given type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The resolved basic-value type.</returns>
        public static BasicValueType GetBasicValueType(this ArithmeticBasicValueType type)
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
        /// <param name="isUnsigned">True, if the basic value type should be interpreted as unsigned.</param>
        /// <returns>The resolved basic-value type.</returns>
        public static ArithmeticBasicValueType GetArithmeticBasicValueType(
            this BasicValueType type,
            bool isUnsigned)
        {
            switch (type)
            {
                case BasicValueType.Int1:
                    return ArithmeticBasicValueType.UInt1;
                case BasicValueType.Int8:
                    return isUnsigned ? ArithmeticBasicValueType.UInt8 : ArithmeticBasicValueType.Int8;
                case BasicValueType.Int16:
                    return isUnsigned ? ArithmeticBasicValueType.UInt16 : ArithmeticBasicValueType.Int16;
                case BasicValueType.Int32:
                    return isUnsigned ? ArithmeticBasicValueType.UInt32 : ArithmeticBasicValueType.Int32;
                case BasicValueType.Int64:
                    return isUnsigned ? ArithmeticBasicValueType.UInt64 : ArithmeticBasicValueType.Int64;
                case BasicValueType.Float32:
                    return ArithmeticBasicValueType.Float32;
                case BasicValueType.Float64:
                    return ArithmeticBasicValueType.Float64;
                default:
                    return ArithmeticBasicValueType.None;
            }
        }

        /// <summary>
        /// Returns true iff the given type represents an int.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>True, iff the given type represents an int.</returns>
        public static bool IsInt(this Type type) =>
            type.GetBasicValueType().IsInt();

        /// <summary>
        /// Returns true iff the given basic-value type represents an int.
        /// </summary>
        /// <param name="value">The basic-value type.</param>
        /// <returns>True, iff the given basic-value type represents an int.</returns>
        public static bool IsInt(this BasicValueType value)
        {
            switch (value)
            {
                case BasicValueType.Int1:
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true iff the given type represents a float.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>True, iff the given type represents a float.</returns>
        public static bool IsFloat(this Type type) =>
            type.GetBasicValueType().IsFloat();

        /// <summary>
        /// Returns true iff the given basic-value type represents a float.
        /// </summary>
        /// <param name="value">The basic-value type.</param>
        /// <returns>True, iff the given basic-value type represents a float.</returns>
        public static bool IsFloat(this BasicValueType value)
        {
            switch (value)
            {
                case BasicValueType.Float32:
                case BasicValueType.Float64:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts the given type into conversion target flags.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The required conversion flags.</returns>
        internal static ConvertFlags ToTargetUnsignedFlags(this Type type) =>
            type.IsUnsignedInt() ? ConvertFlags.TargetUnsigned : ConvertFlags.None;

        /// <summary>
        /// Returns the string representation of the given type.
        /// </summary>
        /// <param name="type">The type to convert to a string.</param>
        /// <returns>The string represenation of the given type.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1307:Specify StringComparison",
            Justification = "string.IndexOf(char, StringComparison) not available in net47")]
        public static string GetStringRepresentation(this Type type)
        {
            var result = new StringBuilder();
            result.Append(type.Namespace);
            result.Append('.');
            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                if (args.Length < 1)
                    result.Append(type.Name);
                else
                {
                    result.Append(type.Name.Substring(0, type.Name.IndexOf('`')));
                    result.Append('<');
                    result.Append(GetStringRepresentation(args[0]));
                    for (int i = 1; i < args.Length; ++i)
                    {
                        result.Append(", ");
                        result.Append(GetStringRepresentation(args[i]));
                    }
                    result.Append('>');
                }
            }
            else
                result.Append(type.Name);
            return result.ToString();
        }
    }
}
