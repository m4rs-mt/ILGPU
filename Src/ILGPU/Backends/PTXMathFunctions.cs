// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXMathFunctions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a math-function mapping for the PTX backend.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class PTXMathFunctionAttribute : Attribute
    {
        public PTXMathFunctionAttribute(string name, bool boolAsInt32 = false)
        {
            Name = name;
            BoolAsInt32 = boolAsInt32;
        }

        public string Name { get; }

        public bool BoolAsInt32 { get; }
    }

    /// <summary>
    /// Represents a fast-math-function mapping for the PTX backend.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class PTXFastMathFunctionAttribute : Attribute
    {
        public PTXFastMathFunctionAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
