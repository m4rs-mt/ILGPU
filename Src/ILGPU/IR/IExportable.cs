// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IExportable.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.IR
{
    /// <summary>
    /// Describes an entity that can be exported to a specific other type.
    /// </summary>
    /// <typeparam name="TTo">
    /// The type that this instance can export to. 
    /// </typeparam>
    public interface IExportable<TTo>
    {
        /// <summary>
        /// Exports this instance to the specified destination type.
        /// </summary>
        /// <returns>
        /// The exported instance.
        /// </returns>
        TTo Export();
    }
}
