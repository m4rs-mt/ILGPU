// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File IntrinsicResolver.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using System;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Resolved required intrinic IR implementations.
    /// </summary>
    /// <typeparam name="TDelegate">The backend-specific delegate type.</typeparam>
    public sealed class IntrinsicResolver<TDelegate> :
        UnorderedTransformation<IntrinsicImplementationProvider<TDelegate>.IRSpecializationPhase>
        where TDelegate : Delegate
    {
        private readonly IntrinsicImplementationProvider<TDelegate> provider;

        /// <summary>
        /// Constructs a new intrinsic resolver.
        /// </summary>
        public IntrinsicResolver(IntrinsicImplementationProvider<TDelegate> implementationProvider)
        {
            provider = implementationProvider ?? throw new ArgumentNullException(nameof(implementationProvider));
        }

        /// <summary cref="UnorderedTransformation{TIntermediate}.CreateIntermediate"/>
        protected override IntrinsicImplementationProvider<TDelegate>.IRSpecializationPhase CreateIntermediate() =>
            provider.BeginIRSpecialization();

        /// <summary cref="UnorderedTransformation{TIntermediate}.FinishProcessing(TIntermediate)"/>
        protected override void FinishProcessing(IntrinsicImplementationProvider<TDelegate>.IRSpecializationPhase intermediate) =>
            intermediate.Dispose();

        /// <summary cref="UnorderedTransformation{TIntermediate}.PerformTransformation(Method.Builder, TIntermediate)"/>
        protected override bool PerformTransformation(
            Method.Builder builder,
            IntrinsicImplementationProvider<TDelegate>.IRSpecializationPhase specializationPhase)
        {
            // Check whether we are currently processing an intrinsic method
            var scope = builder.CreateScope();

            bool applied = false;
            // Analyze intrinsic nodes
            foreach (Value value in scope.Values)
            {
                if (value is MethodCall methodCall)
                    applied |= specializationPhase.RegisterIntrinsic(methodCall.Target);
                else
                    applied |= specializationPhase.RegisterIntrinsic(value);
            }

            return applied;
        }
    }
}
