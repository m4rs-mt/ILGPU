// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Methods.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using ValueList = ILGPU.Util.InlineList<ILGPUC.IR.Values.ValueReference>;

namespace ILGPUC.IR.Construction;

partial class IRBuilder
{
    /// <summary>
    /// Creates a new call node builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="target">The jump target.</param>
    /// <returns>A call builder.</returns>
    public MethodCall.Builder CreateCall(Location location, Method target) =>
        new(this, location, target);

    /// <summary>
    /// Creates a new method call.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="target">The method target.</param>
    /// <param name="values">The argument values.</param>
    /// <returns>The created method call value.</returns>
    internal MethodCall CreateCall(
        Location location,
        Method target,
        ref ValueList values)
    {
        location.AssertNotNull(target);
        return Append(new MethodCall(
            GetInitializer(location),
            target,
            ref values));
    }

    /// <summary>
    /// Creates a new phi node builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The given node type.</param>
    /// <returns>The created phi builder.</returns>
    public PhiValue.Builder CreatePhi(Location location, TypeNode type) =>
        CreatePhi(location, type, 0);

    /// <summary>
    /// Creates a new phi node builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The given node type.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <returns>The created phi builder.</returns>
    public PhiValue.Builder CreatePhi(
        Location location,
        TypeNode type,
        int capacity)
    {
        location.AssertNotNull(type);

        var phiNode = CreatePhiValue(new PhiValue(
            GetInitializer(location),
            type));
        return new PhiValue.Builder(phiNode, capacity);
    }
}
