// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugSetter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Configures debug assertions and IO operations in the IR based on
/// <see cref="CompilationProperties"/>. Runs as the first pass in the global
/// optimization pipeline so that subsequent passes operate on clean, assertion-free IR
/// in Release mode.
/// </summary>
/// <param name="args">The transformation args.</param>
sealed class DebugSetter(TransformationArgs args) : Transformation(args)
{
    /// <summary>
    /// Registers value mappings that remove debug/IO operations when disabled.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        if (!Properties.EnableAssertions)
            MapBasicBlockValue<DebugAssertOperation>((_, _) => null);
        if (!Properties.EnableIOOperations)
            MapBasicBlockValue<WriteToOutput>((_, _) => null);
    }
}
