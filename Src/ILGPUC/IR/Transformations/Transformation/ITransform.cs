// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ITransform.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Rewriting;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Represents an abstract transform.
/// </summary>
interface ITransform : IRewriter
{
    /// <summary>
    /// Replaces the given value with the given value.
    /// </summary>
    /// <param name="oldValue">The value to be replaced.</param>
    /// <param name="newValue">The value to replace.</param>
    void Replace(Value oldValue, Value? newValue);
}
