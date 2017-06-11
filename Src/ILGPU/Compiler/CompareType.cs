// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CompareType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Compiler
{
    /// <summary>
    /// Represents the comparison type of two value.
    /// </summary>
    enum CompareType : byte
    {
        Equal,
        NotEqual,
        GreaterEqual,
        GreaterThan,
        LessEqual,
        LessThan,
    }
}
