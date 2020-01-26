// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: VariableView.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// Represents a general view to a variable.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct VariableView<T>
        where T : struct
    {
        #region Constants

        /// <summary>
        /// Represents the native size of a single element.
        /// </summary>
        public static readonly int VariableSize = Interop.SizeOf<T>();

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new variable view.
        /// </summary>
        /// <param name="baseView">The base view.</param>
        public VariableView(ArrayView<T> baseView)
        {
            Debug.Assert(baseView.IsValid && baseView.Length == 1, "Invalid base view");
            BaseView = baseView;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the base view.
        /// </summary>
        public ArrayView<T> BaseView { get; }

        /// <summary>
        /// Returns true iff this view points to a valid location.
        /// </summary>
        public bool IsValid => BaseView.IsValid;

        /// <summary>
        /// Accesses the stored value.
        /// </summary>
        public ref T Value => ref BaseView[0];

        #endregion

        #region Methods

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

        /// <summary>
        /// Creates a sub view into this view.
        /// </summary>
        /// <param name="offsetInBytes"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VariableView<TOther> GetSubView<TOther>(int offsetInBytes)
            where TOther : struct
        {
            Debug.Assert(offsetInBytes >= 0, "Offset out of range");
            Debug.Assert(offsetInBytes + Interop.SizeOf<TOther>() <= VariableSize, "OutOfBounds sub view");

            var rawView = BaseView.Cast<byte>();
            var subView = rawView.GetSubView(offsetInBytes);
            var finalView = subView.Cast<TOther>();
            return new VariableView<TOther>(finalView);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this view.
        /// </summary>
        /// <returns>The string representation of this view.</returns>
        public override string ToString() => BaseView.ToString();

        #endregion
    }
}
