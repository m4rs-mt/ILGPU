// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.Rewriting;

/// <summary>
/// A basic type rewriter.
/// </summary>
interface IBaseTypeRewriter : IGenerationObject
{
    /// <summary>
    /// Rewrites the given field span while consuming the structure type.
    /// </summary>
    /// <param name="structureType">The source structure type.</param>
    /// <param name="fieldSpan">The source field span to rewrite.</param>
    /// <returns>The rewritten field span.</returns>
    FieldSpan Rewrite(StructureType structureType, FieldSpan fieldSpan);
}

/// <summary>
/// A full featured type rewriter for type values.
/// </summary>
interface ITypeRewriter : IBaseTypeRewriter
{
    /// <summary>
    /// Returns the parent module builder.
    /// </summary>
    ModuleBuilder ModuleBuilder { get; }

    /// <summary>
    /// Rewrites the given value.
    /// </summary>
    /// <param name="oldValue">The value to rebuild.</param>
    /// <returns>The rebuilt value.</returns>
    TypeValue Rewrite(TypeValue oldValue);
}
