// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: TypeExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace ILGPU.Util
{
    /// <summary>
    /// Represents general type extensions.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Represents all basic-value types.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This type is readonly")]
        public static readonly IReadOnlyList<BasicValueType> BasicValueTypes = (BasicValueType[])Enum.GetValues(typeof(BasicValueType));

        /// <summary>
        /// Represents all integer basic-value types.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This type is readonly")]
        public static readonly IReadOnlyList<BasicValueType> IntBasicValueTypes = (from t in BasicValueTypes where t.IsInt() select t).ToList();

        /// <summary>
        /// Represents all float basic-value types.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This type is readonly")]
        public static readonly IReadOnlyList<BasicValueType> FloatBasicValueTypes = (from t in BasicValueTypes where t.IsFloat() select t).ToList();

        /// <summary>
        /// Represents all numeric basic-value types.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This type is readonly")]
        public static readonly IReadOnlyList<BasicValueType> NumericBasicValueTypes = (from t in BasicValueTypes where t.IsInt() || t.IsFloat() select t).ToList();

        /// <summary>
        /// Represents all managed integer basic-value types (excludes U1).
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This type is readonly")]
        public static readonly IReadOnlyList<BasicValueType> ManagedIntBasicValueTypes = (from t in IntBasicValueTypes where t > BasicValueType.UInt1 select t).ToList();

        /// <summary>
        /// Represents all managed float basic-value types.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This type is readonly")]
        public static readonly IReadOnlyList<BasicValueType> ManagedFloatBasicValueTypes = (from t in FloatBasicValueTypes select t).ToList();

        /// <summary>
        /// Represents all managed numeric basic-value types.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This type is readonly")]
        public static readonly IReadOnlyList<BasicValueType> ManagedNumericBasicValueTypes = ManagedIntBasicValueTypes.Concat(ManagedFloatBasicValueTypes).ToList();

        internal static bool HasSupportedBaseClass(this Type type)
        {
            return !type.IsValueType && type.BaseType != typeof(object);
        }

        internal static bool IsVoidPtr(this Type type)
        {
            return type == typeof(IntPtr) ||
                type == typeof(UIntPtr) ||
                (type.IsPointer && type.GetElementType() == typeof(void));
        }

        internal static int SizeOf(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (!type.IsValueType)
                throw new NotSupportedException($"Type type '{type}' is no value type");
            // We cannot use Marshal.SizeOf<T> since SizeOf<T> throws an exception when using generic types
            // TODO: replace with other functionality
            return Marshal.SizeOf(Activator.CreateInstance(type));
        }

        /// <summary>
        /// Returns a type that reflects a LLVM-type repesentation.
        /// </summary>
        /// <param name="type">The current .Net type.</param>
        /// <returns>A .Net type that reflects a LLVM-type representation.</returns>
        internal static Type GetLLVMTypeRepresentation(this Type type)
        {
            if (type.IsPointer)
                return type;
            if (type.IsPassedViaPtr())
                return type.MakePointerType();
            return type;
        }

        internal static bool IsPassedViaPtr(this Type type)
        {
            return !type.IsValueType &&
                !type.IsArray &&
                !type.IsTreatedAsPtr() &&
                type != typeof(string);
        }

        internal static bool IsTreatedAsPtr(this Type type)
        {
            return type.IsPointer || type.IsByRef;
        }

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
                case BasicValueType.UInt1:
                    return typeof(bool);
                case BasicValueType.Int8:
                    return typeof(sbyte);
                case BasicValueType.UInt8:
                    return typeof(byte);
                case BasicValueType.Int16:
                    return typeof(short);
                case BasicValueType.UInt16:
                    return typeof(ushort);
                case BasicValueType.Int32:
                    return typeof(int);
                case BasicValueType.UInt32:
                    return typeof(uint);
                case BasicValueType.Int64:
                    return typeof(long);
                case BasicValueType.UInt64:
                    return typeof(ulong);
                case BasicValueType.Single:
                    return typeof(float);
                case BasicValueType.Double:
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
        internal static BasicValueType GetBasicValueType(this Type type)
        {
            if (type.IsPointer || type.IsByRef || type == typeof(IntPtr) || type == typeof(UIntPtr))
                return BasicValueType.Ptr;
            if (type.IsArray)
                return BasicValueType.Array;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return BasicValueType.UInt1;
                case TypeCode.SByte:
                    return BasicValueType.Int8;
                case TypeCode.Byte:
                    return BasicValueType.UInt8;
                case TypeCode.Int16:
                    return BasicValueType.Int16;
                case TypeCode.UInt16:
                    return BasicValueType.UInt16;
                case TypeCode.Int32:
                    return BasicValueType.Int32;
                case TypeCode.UInt32:
                    return BasicValueType.UInt32;
                case TypeCode.Int64:
                    return BasicValueType.Int64;
                case TypeCode.UInt64:
                    return BasicValueType.UInt64;
                case TypeCode.Single:
                    return BasicValueType.Single;
                case TypeCode.Double:
                    return BasicValueType.Double;
                default:
                    return BasicValueType.None;
            }
        }

        /// <summary>
        /// Returns true iff the given type represents an int.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>True, iff the given type represents an int.</returns>
        public static bool IsInt(this Type type)
        {
            return type.GetBasicValueType().IsInt();
        }

        /// <summary>
        /// Returns true iff the given basic-value type represents an int.
        /// </summary>
        /// <param name="value">The basic-value type.</param>
        /// <returns>True, iff the given basic-value type represents an int.</returns>
        public static bool IsInt(this BasicValueType value)
        {
            return IsSignedInt(value) || IsUnsignedInt(value);
        }

        /// <summary>
        /// Returns true iff the given basic-value type represents a signed int.
        /// </summary>
        /// <param name="value">The basic-value type.</param>
        /// <returns>True, iff the given basic-value type represents a signed int.</returns>
        public static bool IsSignedInt(this BasicValueType value)
        {
            switch (value)
            {
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
        /// Returns true iff the given basic-value type represents an unsigned int.
        /// </summary>
        /// <param name="value">The basic-value type.</param>
        /// <returns>True, iff the given basic-value type represents an unsigned int.</returns>
        public static bool IsUnsignedInt(this BasicValueType value)
        {
            switch (value)
            {
                case BasicValueType.UInt1:
                case BasicValueType.UInt8:
                case BasicValueType.UInt16:
                case BasicValueType.UInt32:
                case BasicValueType.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true iff the given basic-value type represents either a ptr or an int.
        /// </summary>
        /// <param name="value">The basic-value type.</param>
        /// <returns>True, iff the given basic-value type represents either a ptr or an int.</returns>
        public static bool IsPtrOrInt(this BasicValueType value)
        {
            return value == BasicValueType.Ptr || value.IsInt();
        }

        /// <summary>
        /// Returns true iff the given type represents a float.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>True, iff the given type represents a float.</returns>
        public static bool IsFloat(this Type type)
        {
            return type.GetBasicValueType().IsFloat();
        }

        /// <summary>
        /// Returns true iff the given basic-value type represents a float.
        /// </summary>
        /// <param name="value">The basic-value type.</param>
        /// <returns>True, iff the given basic-value type represents a float.</returns>
        public static bool IsFloat(this BasicValueType value)
        {
            switch (value)
            {
                case BasicValueType.Single:
                case BasicValueType.Double:
                    return true;
                default:
                    return false;
            }
        }
    }
}
