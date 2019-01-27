// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IDebugInformationEnumerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents an abstract item of a <see cref="IDebugInformationEnumerator{T}"/>.
    /// </summary>
    public interface IDebugInformationEnumeratorValue
    {
        /// <summary>
        /// Returns true if this information is valid.
        /// </summary>
        bool IsValid { get; }
    }

    /// <summary>
    /// Represents a debug-information enumerator.
    /// </summary>
    /// <typeparam name="T">The enumerator type.</typeparam>
    public interface IDebugInformationEnumerator<T>
        where T : struct, IDebugInformationEnumeratorValue
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
