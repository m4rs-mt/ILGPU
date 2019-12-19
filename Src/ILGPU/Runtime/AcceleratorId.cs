// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorId.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a single accelerator reference.
    /// </summary>
    [Serializable]
    public abstract class AcceleratorId
    {
        #region Instance

        /// <summary>
        /// Constructs a new accelerator id.
        /// </summary>
        /// <param name="type">The accelerator type.</param>
        protected AcceleratorId(AcceleratorType type)
        {
            AcceleratorType = type;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the type of the associated accelerator.
        /// </summary>
        public AcceleratorType AcceleratorType { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current accelerator id.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current accelerator id.</returns>
        public override bool Equals(object obj) =>
            obj is AcceleratorId acceleratorId &&
            acceleratorId.AcceleratorType == AcceleratorType;

        /// <summary>
        /// Returns the hash code of this accelerator id.
        /// </summary>
        /// <returns>The hash code of this accelerator id.</returns>
        public override int GetHashCode() => (int)AcceleratorType;

        /// <summary>
        /// Returns the string representation of this accelerator id.
        /// </summary>
        /// <returns>The string representation of this accelerator id.</returns>
        public override string ToString() =>
            $"Type: {AcceleratorType}";

        #endregion
    }
}
