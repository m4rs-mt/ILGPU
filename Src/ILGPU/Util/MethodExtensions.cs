// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: MethodExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Reflection;

namespace ILGPU.Util
{
    /// <summary>
    /// Extensions for methods.
    /// </summary>
    static class MethodExtensions
    {
        /// <summary>
        /// Returns a parameter offset of 1 for instance methods and 0 for static methods.
        /// </summary>
        /// <param name="method">The method to compute the parameter offset for.</param>
        /// <returns>A parameter offset of 1 for instance methods and 0 for static methods.</returns>
        public static int GetParameterOffset(this MethodBase method)
        {
            return method.IsStatic ? 0 : 1;
        }
    }
}
