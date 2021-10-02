// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ObjectType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents an abstract object value.
    /// </summary>
    public abstract class ObjectType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new object type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        protected ObjectType(IRTypeContext typeContext)
            : base(typeContext)
        { }

        #endregion
    }
}
