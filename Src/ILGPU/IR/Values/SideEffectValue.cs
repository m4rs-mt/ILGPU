// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                          Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: SideEffectValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract value with side effects.
    /// </summary>
    public abstract class SideEffectValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new value with side effects.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal SideEffectValue(in ValueInitializer initializer)
            : base(initializer)
        { }

        /// <summary>
        /// Constructs a new value with side effects.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="staticType">The static type.</param>
        internal SideEffectValue(in ValueInitializer initializer, TypeNode staticType)
            : base(initializer, staticType)
        { }

        #endregion
    }

    /// <summary>
    /// A value with side effects that depends on the control flow of the program.
    /// </summary>
    public abstract class ControlFlowValue : SideEffectValue
    {
        #region Instance

        /// <summary>
        /// Constructs a value that depends on the control flow of the program.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal ControlFlowValue(in ValueInitializer initializer)
            : base(initializer)
        { }

        /// <summary>
        /// Constructs a value that depends on the control flow of the program.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="staticType">The static type.</param>
        internal ControlFlowValue(in ValueInitializer initializer, TypeNode staticType)
            : base(initializer, staticType)
        { }

        #endregion
    }
}
