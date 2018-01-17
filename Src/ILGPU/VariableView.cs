// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: VariableView.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// Represents a general view to a variable at a specific address on a gpu.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public struct VariableView<T> : IEquatable<VariableView<T>>
        where T : struct
    {
        #region Constants

        /// <summary>
        /// Represents the native size of a single element.
        /// </summary>
        public static readonly int VariableSize = Interop.SizeOf<T>();

        #endregion

        #region Instance

        private IntPtr ptr;

        /// <summary>
        /// Constructs a new variable view.
        /// </summary>
        /// <param name="variableRef">A reference to the target variable.</param>
        public VariableView(ref T variableRef)
        {
            ptr = Interop.GetAddress(ref variableRef);
        }

        /// <summary>
        /// Constructs a new variable view.
        /// </summary>
        /// <param name="ptr">The target address of the variable.</param>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "ptr")]
        public VariableView(IntPtr ptr)
        {
            this.ptr = ptr;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true iff this view points to a valid location.
        /// </summary>
        public bool IsValid => ptr != IntPtr.Zero;

        /// <summary>
        /// Returns the in-memory address of the variable.
        /// </summary>
        public IntPtr Pointer => ptr;

        /// <summary>
        /// Accesses the stored value.
        /// </summary>
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Load(); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { Store(value); }
        }

        /// <summary>
        /// Returns a reference to the encapsulated variable.
        /// </summary>
        public ref T Ref => ref LoadRef();

        #endregion

        #region Methods

        /// <summary>
        /// Loads a reference to the variable as ref T.
        /// </summary>
        /// <returns>A reference to the internal variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T LoadRef()
        {
            return ref Interop.GetRef<T>(ptr);
        }

        /// <summary>
        /// Loads the variable as type T.
        /// </summary>
        /// <returns>The loaded variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Load()
        {
            return Interop.PtrToStructure<T>(ptr);
        }

        /// <summary>
        /// Stores the given value into the variable of type T.
        /// </summary>
        /// <param name="value">The value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Store(T value)
        {
            Interop.StructureToPtr(value, ptr);
        }

        /// <summary>
        /// Returns a sub-view to a particular sub-variable.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <param name="offsetInBytes">The offset of the sub variable in bytes.</param>
        /// <returns>The sub-variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VariableView<TOther> GetSubView<TOther>(int offsetInBytes)
            where TOther : struct
        {
            Debug.Assert(offsetInBytes >= 0, "Offset out of range");
            Debug.Assert(offsetInBytes + Interop.SizeOf<TOther>() <= VariableSize, "OutOfBounds sub view");
            return new VariableView<TOther>(
                Interop.LoadEffectiveAddress(ptr, 1, offsetInBytes));
        }

        /// <summary>
        /// Casts the current variable view into another variable type.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <returns>The casted variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VariableView<TOther> Cast<TOther>()
            where TOther : struct
        {
            Debug.Assert(VariableSize >= Interop.SizeOf<TOther>(), "OutOfBounds cast");
            return new VariableView<TOther>(ptr);
        }

        /// <summary>
        /// Copies the current value to the memory location of the given view.
        /// </summary>
        /// <param name="targetView">The target view.</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        public void CopyTo(VariableView<T> targetView)
        {
            targetView.Value = Value;
        }

        /// <summary>
        /// Copies the value from the memory location of the given view.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <remarks>The source view must be accessible from the this view (e.g. same accelerator).</remarks>
        public void CopyFrom(VariableView<T> sourceView)
        {
            Value = sourceView.Value;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given view is equal to the current view.
        /// </summary>
        /// <param name="other">The other view.</param>
        /// <returns>True, iff the given view is equal to the current view.</returns>
        public bool Equals(VariableView<T> other)
        {
            return other == this;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current view.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current view.</returns>
        public override bool Equals(object obj)
        {
            if (obj is VariableView<T>)
                return Equals((VariableView<T>)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this view.
        /// </summary>
        /// <returns>The hash code of this view.</returns>
        public override int GetHashCode()
        {
            return ptr.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this view.
        /// </summary>
        /// <returns>The string representation of this view.</returns>
        public override string ToString()
        {
            return ptr.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second views are the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second views are the same.</returns>
        public static bool operator ==(VariableView<T> first, VariableView<T> second)
        {
            return first.ptr == second.ptr;
        }

        /// <summary>
        /// Returns true iff the first and second view are not the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second view are not the same.</returns>
        public static bool operator !=(VariableView<T> first, VariableView<T> second)
        {
            return first.ptr != second.ptr;
        }

        #endregion
    }
}
