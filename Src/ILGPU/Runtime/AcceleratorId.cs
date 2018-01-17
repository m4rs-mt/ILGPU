// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorId.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a single accelerator reference.
    /// </summary>
    [Serializable]
    public struct AcceleratorId : IEquatable<AcceleratorId>
    {
        #region Instance

        /// <summary>
        /// Constructs a new accelerator id.
        /// </summary>
        /// <param name="type">The accelerator type.</param>
        /// <param name="deviceId">The referenced device id.</param>
        public AcceleratorId(AcceleratorType type, int deviceId)
        {
            if (deviceId < 0)
                throw new ArgumentOutOfRangeException(nameof(deviceId));
            AcceleratorType = type;
            DeviceId = deviceId;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Retunrs the type of the associated accelerator.
        /// </summary>
        public AcceleratorType AcceleratorType { get; }

        /// <summary>
        /// Retunrs the device id of the associated accelerator.
        /// </summary>
        public int DeviceId { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given accelerator id is equal to the current accelerator id.
        /// </summary>
        /// <param name="other">The other accelerator id.</param>
        /// <returns>True, iff the given accelerator id is equal to the current accelerator id.</returns>
        public bool Equals(AcceleratorId other)
        {
            return this == other;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current accelerator id.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current accelerator id.</returns>
        public override bool Equals(object obj)
        {
            if (obj is AcceleratorId)
                return Equals((AcceleratorId)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this accelerator id.
        /// </summary>
        /// <returns>The hash code of this accelerator id.</returns>
        public override int GetHashCode()
        {
            return DeviceId ^ (int)AcceleratorType;
        }

        /// <summary>
        /// Returns the string representation of this accelerator id.
        /// </summary>
        /// <returns>The string representation of this accelerator id.</returns>
        public override string ToString()
        {
            return $"Id: {DeviceId} [Type: {AcceleratorType}]";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second accelerator id are the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second accelerator id are the same.</returns>
        public static bool operator ==(AcceleratorId first, AcceleratorId second)
        {
            return first.AcceleratorType == second.AcceleratorType &&
                first.DeviceId == second.DeviceId;
        }

        /// <summary>
        /// Returns true iff the first and second accelerator id are not the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second accelerator id are not the same.</returns>
        public static bool operator !=(AcceleratorId first, AcceleratorId second)
        {
            return first.AcceleratorType != second.AcceleratorType ||
                first.DeviceId != second.DeviceId;
        }

        #endregion
    }
}
