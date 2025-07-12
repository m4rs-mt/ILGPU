// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ObjectType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.Types;

/// <summary>
/// Represents an abstract object value.
/// </summary>
/// <param name="typeContext">The parent type context.</param>
abstract class ObjectType(IRTypeContext typeContext) : TypeNode(typeContext)
{
    /// <inheritdoc/>
    public override bool IsObjectType => true;
}
