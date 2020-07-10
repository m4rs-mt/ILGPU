// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: CuBlasPointerModeHandler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// An abstract handler to adapt the current pointer mode of a
    /// <see cref="CuBlas{TPointerModeHandler}"/> instance.
    /// </summary>
    /// <typeparam name="THandler">The handler type itself.</typeparam>
    public interface ICuBlasPointerModeHandler<THandler>
        where THandler : struct, ICuBlasPointerModeHandler<THandler>
    {
        /// <summary>
        /// Updates the pointer mode to be compatible with the given one.
        /// </summary>
        /// <param name="parent">The parent instance to use.</param>
        /// <param name="pointerMode">The new pointer mode to use.</param>
        void UpdatePointerMode(CuBlas<THandler> parent, CuBlasPointerMode pointerMode);
    }

    /// <summary>
    /// A utility class that holds pre-defined pointer mode handlers that can be used in
    /// combination with the type <see cref="CuBlas{TPointerModeHandler}"/>.
    /// </summary>
    public static class CuBlasPointerModeHandlers
    {
        #region Nested Types

        /// <summary>
        /// A custom handler type that automatically updates the pointer mode to be compatible
        /// with the requested pointer mode.
        /// </summary>
        public readonly struct AutomaticMode : ICuBlasPointerModeHandler<AutomaticMode>
        {
            /// <summary cref="ICuBlasPointerModeHandler{THandler}.UpdatePointerMode(CuBlas{THandler}, CuBlasPointerMode)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void UpdatePointerMode(CuBlas<AutomaticMode> parent, CuBlasPointerMode pointerMode)
            {
                // Setup the internal pointer mode ot the desired one
                parent.PointerMode = pointerMode;
            }
        }

        /// <summary>
        /// A custom handler type that does not automatically update the pointer mode.
        /// </summary>
        public readonly struct ManualMode : ICuBlasPointerModeHandler<ManualMode>
        {
            /// <summary cref="ICuBlasPointerModeHandler{THandler}.UpdatePointerMode(CuBlas{THandler}, CuBlasPointerMode)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void UpdatePointerMode(CuBlas<ManualMode> parent, CuBlasPointerMode pointerMode)
            {
                // Do nothing
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Returns an automatic pointer mode handler that switches the underlying pointer
        /// mode of the <see cref="CuBlas{TPointerModeHandler}"/> class automatically.
        /// </summary>
        public static AutomaticMode Automatic { get; } = default;

        /// <summary>
        /// Returns a manual pointer mode handler that does not change the underlying pointer
        /// mode of the <see cref="CuBlas{TPointerModeHandler}"/> class.
        /// </summary>
        public static ManualMode Manual { get; } = default;

        #endregion
    }
}
