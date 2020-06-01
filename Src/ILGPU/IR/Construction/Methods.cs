// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Methods.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Runtime.CompilerServices;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new call node builder.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The jump target.</param>
        /// <returns>A call builder.</returns>
        public MethodCall.Builder CreateCall(Location location, Method target) =>
            new MethodCall.Builder(this, location, target);

        /// <summary>
        /// Creates a new method call.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The method target.</param>
        /// <param name="values">The argument values.</param>
        /// <returns>The created method call value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        /// <summary>
        /// Declares a method.
        /// </summary>
        /// <param name="declaration">The method declaration.</param>
        /// <param name="created">True, if the method has been created.</param>
        /// <returns>The declared method.</returns>
        public Method DeclareMethod(
            in MethodDeclaration declaration,
            out bool created) =>
            Context.Declare(declaration, out created);
    }
}
