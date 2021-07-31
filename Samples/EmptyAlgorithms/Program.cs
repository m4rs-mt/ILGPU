// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2019 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;

namespace Empty
{
    class Program
    {
        /// <summary>
        /// Initializes an ILGPU context and the ILGPU.Algorithms library.
        /// </summary>
        static void Main()
        {
            // Every application needs an instantiated global ILGPU context
            // The context builder can be configured to enable the algorithms library
            using (var context = Context.Create(builder => builder.Default().EnableAlgorithms()))
            {
            }
        }
    }
}
