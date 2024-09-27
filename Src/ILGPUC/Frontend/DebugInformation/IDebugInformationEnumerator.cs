// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IDebugInformationEnumerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents a debug-information enumerator.
    /// </summary>
    /// <typeparam name="T">The enumerator type.</typeparam>
    public interface IDebugInformationEnumerator<T>
    {
        /// <summary>
        /// Returns the current object.
        /// </summary>
        T Current { get; }

        /// <summary>
        /// Moves the enumerator forward to the given instruction offset.
        /// </summary>
        /// <param name="offset">The instruction offset in bytes.</param>
        bool MoveTo(int offset);
    }
}
