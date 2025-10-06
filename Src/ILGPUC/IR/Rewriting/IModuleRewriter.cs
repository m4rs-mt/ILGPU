// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IModuleRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;

namespace ILGPUC.IR.Rewriting;

/// <summary>
/// Represents an abstract module rewriter.
/// </summary>
interface IModuleRewriter : IRewriter<ModuleBuilder>
{
    /// <summary>
    /// Returns the underlying new module.
    /// </summary>
    Module Module { get; }

    /// <summary>
    /// Returns the old module.
    /// </summary>
    Module OldModule { get; }
}
