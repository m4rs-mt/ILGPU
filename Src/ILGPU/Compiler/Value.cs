// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Value.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Util;
using System;

namespace ILGPU.Compiler
{
    /// <summary>
    /// Represents an abstract value entry on the abstract execution stack.
    /// </summary>
    public struct Value : IEquatable<Value>
    {
        #region Instance

        /// <summary>
        /// Constructs a new stack entry with the given arguments.
        /// </summary>
        /// <param name="type">The .Net type of the entry.</param>
        /// <param name="value">The LLVM value of the entry.</param>
        [CLSCompliant(false)]
        public Value(Type type, LLVMValueRef value)
        {
            ValueType = type;
            BasicValueType = type.GetBasicValueType();
            LLVMValue = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the .Net type of this entry.
        /// </summary>
        public Type ValueType { get; }

        /// <summary>
        /// Returns the LLVM value of this entry.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMValueRef LLVMValue { get; }

        /// <summary>
        /// Returns true if the value has a valid reference.
        /// </summary>
        public bool IsValid => LLVMValue.Pointer != IntPtr.Zero;

        /// <summary>
        /// Returns the basic-value type of this value.
        /// </summary>
        public BasicValueType BasicValueType { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given value is equal to the current value.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>True, iff the given value is equal to the current value.</returns>
        public bool Equals(Value other)
        {
            return this == other;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current value.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current value.</returns>
        public override bool Equals(object obj)
        {
            if (obj is Value)
                return Equals((Value)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this value.
        /// </summary>
        /// <returns>The hash code of this value.</returns>
        public override int GetHashCode()
        {
            return LLVMValue.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this value.
        /// </summary>
        /// <returns>The string representation of this value.</returns>
        public override string ToString()
        {
            return LLVMValue.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second values are the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second values are the same.</returns>
        public static bool operator ==(Value first, Value second)
        {
            return first.LLVMValue.Pointer == second.LLVMValue.Pointer;
        }

        /// <summary>
        /// Returns true iff the first and second values are not the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second values are not the same.</returns>
        public static bool operator !=(Value first, Value second)
        {
            return first.LLVMValue.Pointer != second.LLVMValue.Pointer;
        }

        #endregion
    }
}
