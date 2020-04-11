// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ObjectType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

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
        /// <param name="source">The original source type (or null).</param>
        protected ObjectType(Type source)
        {
            Source = source;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the original source type (may be null).
        /// </summary>
        public Type Source { get; }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.TryResolveManagedType(out Type)"/>
        public override bool TryResolveManagedType(out Type type)
        {
            type = Source;
            return type != null;
        }

        #endregion
    }
}
