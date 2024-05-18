// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IImportable.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.IR
{
    /// <summary>
    /// Describes an entity that can be imported from a specific other type.
    /// </summary>
    /// <typeparam name="TFrom">
    /// The type that this instance can import from.
    /// </typeparam>
    public interface IImportable<TFrom>
    {
        /// <summary>
        /// Imports data from another instance into this one.
        /// </summary>
        /// <param name="fromSource">
        /// The source of the data to import.
        /// </param>
        void Import(TFrom fromSource);
    }
}
