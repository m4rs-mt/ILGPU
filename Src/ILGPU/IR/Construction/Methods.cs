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
using System.Collections.Immutable;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new call node.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The jump target.</param>
        /// <param name="arguments">The target arguments.</param>
        /// <returns>A function call.</returns>
        public ValueReference CreateCall(
            Location location,
            Method target,
            ImmutableArray<ValueReference> arguments) =>
            Append(new MethodCall(
                GetInitializer(location),
                target,
                arguments));

        /// <summary>
        /// Creates a new phi node builder.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The given node type.</param>
        /// <returns>The created phi builder.</returns>
        public PhiValue.Builder CreatePhi(Location location, TypeNode type)
        {
            location.AssertNotNull(type);

            var phiNode = CreatePhiValue(new PhiValue(
                GetInitializer(location),
                type));
            return new PhiValue.Builder(phiNode);
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
