// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: AlgorithmObject.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Represents the base class for all objects that need to
    /// reference an accelerator.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class AlgorithmObject : AcceleratorObject
    {
        #region Instance

        /// <summary>
        /// Constructs a new context object.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected AlgorithmObject(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }
}
