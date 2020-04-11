// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaAcceleratorId.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a single Cuda accelerator reference.
    /// </summary>
    public sealed class CudaAcceleratorId : AcceleratorId
    {
        #region Instance

        /// <summary>
        /// Constructs a new Cuda accelerator reference.
        /// </summary>
        /// <param name="deviceId">The Cuda device id.</param>
        public CudaAcceleratorId(int deviceId)
            : base(AcceleratorType.Cuda)
        {
            if (deviceId < 0)
                throw new ArgumentOutOfRangeException(nameof(deviceId));

            DeviceId = deviceId;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Cuda device id.
        /// </summary>
        public int DeviceId { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current accelerator id.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>
        /// True, if the given object is equal to the current accelerator id.
        /// </returns>
        public override bool Equals(object obj) =>
            obj is CudaAcceleratorId acceleratorId &&
            acceleratorId.DeviceId == DeviceId;

        /// <summary>
        /// Returns the hash code of this accelerator id.
        /// </summary>
        /// <returns>The hash code of this accelerator id.</returns>
        public override int GetHashCode() =>
            DeviceId ^ base.GetHashCode();

        /// <summary>
        /// Returns the string representation of this accelerator id.
        /// </summary>
        /// <returns>The string representation of this accelerator id.</returns>
        public override string ToString() => $"Device {DeviceId}, " + base.ToString();

        #endregion
    }
}
