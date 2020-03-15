// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ClassValues.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using System.Collections.Immutable;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// An abstract class value.
    /// </summary>
    public abstract class ClassValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract class value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        internal ClassValue(BasicBlock basicBlock, TypeNode initialType)
            : base(basicBlock, initialType)
        { }

        #endregion
    }

    /// <summary>
    /// Represents an operation on object values.
    /// </summary>
    public abstract class ClassOperationValue : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract object operation.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="values">All child values.</param>
        /// <param name="initialType">The initial node type.</param>
        internal ClassOperationValue(
            BasicBlock basicBlock,
            ImmutableArray<ValueReference> values,
            TypeNode initialType)
            : base(basicBlock, values, initialType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the object value to load from.
        /// </summary>
        public ValueReference ObjectValue => this[0];

        /// <summary>
        /// Returns the object type.
        /// </summary>
        public ObjectType ObjectType => ObjectValue.Type as ObjectType;

        #endregion
    }
}
