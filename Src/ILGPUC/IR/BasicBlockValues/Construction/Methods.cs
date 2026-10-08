// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Methods.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using System;

namespace ILGPUC.IR.BasicBlockValues.Construction;

partial class BasicBlockBuilder
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
        ref ValueBuilderList values)
    {
        location.AssertNotNull(target);
        return Append(new MethodCall(GetInitializer(location), target, ref values));
    }

    /// <summary>
    /// Creates a new phi node builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The given node type.</param>
    /// <returns>The created phi builder.</returns>
    public PhiValue.Builder CreatePhi(Location location, TypeValue type) =>
        CreatePhi(location, type, 2);

    /// <summary>
    /// Creates a new phi node builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The given node type.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <param name="argumentMapper">The argument mapper to use.</param>
    /// <returns>The created phi builder.</returns>
    public virtual PhiValue.Builder CreatePhi(
        Location location,
        TypeValue type,
        int capacity,
        Func<Value, Value>? argumentMapper = null)
    {
        location.AssertNotNull(type);

        var phiNode = new PhiValue(GetPhiInitializer(location), type);
        Append(phiNode);
        return new PhiValue.Builder(this, phiNode, capacity, argumentMapper);
    }
}
