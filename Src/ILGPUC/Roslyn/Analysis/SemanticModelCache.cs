// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SemanticModelCache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Hands out one semantic model per syntax tree, so that the analysis binds every
/// tree once and later queries reuse what earlier ones bound.
/// </summary>
/// <param name="compilation">The compilation.</param>
sealed class SemanticModelCache(Compilation compilation)
{
    private readonly Dictionary<SyntaxTree, SemanticModel> _models = [];

    /// <summary>
    /// Returns the semantic model of <paramref name="tree"/>.
    /// </summary>
    public SemanticModel this[SyntaxTree tree]
    {
        get
        {
            if (!_models.TryGetValue(tree, out var model))
                _models[tree] = model = compilation.GetSemanticModel(tree);
            return model;
        }
    }
}
