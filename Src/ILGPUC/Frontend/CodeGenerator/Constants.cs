// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Loads an int.
    /// </summary>
    /// <param name="value">The value.</param>
    private void Load(int value) =>
        Block.Push(Builder.CreatePrimitiveValue(Location, value));

    /// <summary>
    /// Loads a long.
    /// </summary>
    /// <param name="value">The value.</param>
    private void Load(long value) =>
        Block.Push(Builder.CreatePrimitiveValue(Location, value));

    /// <summary>
    /// Loads a float.
    /// </summary>
    /// <param name="value">The value.</param>
    private void Load(float value) =>
        Block.Push(Builder.CreatePrimitiveValue(Location, value));

    /// <summary>
    /// Loads a double.
    /// </summary>
    /// <param name="value">The value.</param>
    private void Load(double value) =>
        Block.Push(Builder.CreatePrimitiveValue(Location, value));

    /// <summary>
    /// Loads a string.
    /// </summary>
    /// <param name="value">The value.</param>
    private void LoadString(string value) =>
        Block.Push(Builder.CreatePrimitiveValue(Location, value));

    /// <summary>
    /// Loads a typed null reference (<c>ldnull</c>).
    /// </summary>
    /// <remarks>
    /// We push a fresh <see cref="IR.PureValues.NullValue"/> of a generic
    /// <c>void*</c> pointer type. Using a non-primitive type means
    /// <c>ModuleBuilder.CreateNull</c> returns a distinct instance rather
    /// than folding to a shared <c>PrimitiveValue(0)</c>, so multiple
    /// <c>ldnull</c> sites in one method don't collide in delegate tables.
    /// The chief consumer is method-group delegate construction:
    /// <c>ldnull ; ldftn Foo ; newobj Func&lt;&gt;::.ctor</c> — the null
    /// flows into <see cref="MakeNewDelegate"/> to mark a static target.
    /// Existing <see cref="MakeIntrinsicBranch"/> null-folding logic
    /// automatically collapses the compiler-generated delegate-cache
    /// diamond.
    /// </remarks>
    private void LoadNull()
    {
        var nullType = ModuleBuilder.CreatePointerType(
            ModuleBuilder.VoidType,
            MemoryAddressSpace.Generic);
        Block.Push(Builder.CreateNull(Location, nullType));
    }
}
