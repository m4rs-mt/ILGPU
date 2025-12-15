// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IMethodRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;

namespace ILGPUC.IR.Rewriting;

/// <summary>
/// An abstract rewriter for methods.
/// </summary>
interface IMethodRewriter : IRewriter<MethodBuilder>, ITypeRewriter
{
    /// <summary>
    /// Returns the underlying new method.
    /// </summary>
    Method Method { get; }

    /// <summary>
    /// Returns the old method.
    /// </summary>
    Method OldMethod { get; }
}
