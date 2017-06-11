// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: DeviceTypes.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Compiler.Intrinsic
{
    /// <summary>
    /// Represents the base interface for custom device types that require
    /// a specific translation.
    /// </summary>
    public interface IDeviceTypes
    {
        /// <summary>
        /// Maps the given type to an LLVM type.
        /// </summary>
        /// <param name="type">The type to map.</param>
        /// <returns>The mapped output type, or null iff the type could not be mapped.</returns>
        MappedType MapType(Type type);
    }
}
