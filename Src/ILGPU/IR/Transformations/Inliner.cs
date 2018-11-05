// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Inliner.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents an abstract inlining configuration.
    /// </summary>
    public interface IInliningConfiguration
    {
        /// <summary>
        /// Returns true if the given callee function can be inlined.
        /// </summary>
        /// <param name="caller">The caller.</param>
        /// <param name="functionCall">The actual function call.</param>
        /// <param name="callee">The callee function.</param>
        /// <param name="calleeEntry">The landscape node of the callee function (if any).</param>
        /// <returns>True, if the given calle function can be inlined.</returns>
        bool CanInline(
            FunctionLandscape.Entry caller,
            FunctionCall functionCall,
            TopLevelFunction callee,
            FunctionLandscape.Entry calleeEntry);
    }

    /// <summary>
    /// Represents an inling configuration to inline all functions
    /// (except those marked with NoInlining).
    /// </summary>
    public readonly struct AggressiveInliningConfiguration : IInliningConfiguration
    {
        /// <summary cref="IInliningConfiguration.CanInline(FunctionLandscape.Entry, FunctionCall, TopLevelFunction, FunctionLandscape.Entry)"/>
        public bool CanInline(
            FunctionLandscape.Entry caller,
            FunctionCall functionCall,
            TopLevelFunction callee,
            FunctionLandscape.Entry calleeEntry)
        {
            // Try to find an aggressive inlining attribute
            if (callee.HasFlags(TopLevelFunctionFlags.NoInlining))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Represents a default (but slightly aggressive) inlining configuration.
    /// </summary>
    public readonly struct DefaultInliningConfiguration : IInliningConfiguration
    {
        private const int MaxNumFunctions = 14;

        /// <summary cref="IInliningConfiguration.CanInline(FunctionLandscape.Entry, FunctionCall, TopLevelFunction, FunctionLandscape.Entry)"/>
        public bool CanInline(
            FunctionLandscape.Entry caller,
            FunctionCall functionCall,
            TopLevelFunction callee,
            FunctionLandscape.Entry calleeEntry)
        {
            // Try to find an aggressive inlining attribute
            if (callee.HasFlags(TopLevelFunctionFlags.NoInlining))
                return false;
            if (callee.HasFlags(TopLevelFunctionFlags.AggressiveInlining))
                return true;

            if (callee.AllNumUses < 2)
                return true;

            if (calleeEntry != null)
                return calleeEntry.NumFunctions < MaxNumFunctions;
            return false;
        }
    }

    /// <summary>
    /// Represents a no inlining configuration.
    /// </summary>
    public readonly struct NoInliningConfiguration : IInliningConfiguration
    {
        /// <summary cref="IInliningConfiguration.CanInline(FunctionLandscape.Entry, FunctionCall, TopLevelFunction, FunctionLandscape.Entry)"/>
        public bool CanInline(
            FunctionLandscape.Entry caller,
            FunctionCall functionCall,
            TopLevelFunction callee,
            FunctionLandscape.Entry calleeEntry) => false;
    }

    /// <summary>
    /// Represents a function inliner.
    /// </summary>
    public sealed class Inliner<TConfiguration> : OrderedTransformation
        where TConfiguration : IInliningConfiguration
    {
        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags =
            TransformationFlags.MergeCallChains |
            TransformationFlags.InferAddressSpaces |
            TransformationFlags.TransformToCPS;

        private readonly TConfiguration configuration;

        /// <summary>
        /// Constructs a new specializer.
        /// </summary>
        public Inliner(in TConfiguration inliningConfiguration)
            : base(TransformationFlags.Inlining, FollowUpFlags)
        {
            configuration = inliningConfiguration;
        }

        /// <summary>
        /// Represents a visitor to inline function call targets.
        /// </summary>
        private struct FunctionCallVisitor : Scope.IFunctionCallVisitor
        {
            private readonly TConfiguration configuration;

            public FunctionCallVisitor(
                IRBuilder builder,
                FunctionLandscape landscape,
                FunctionLandscape.Entry caller,
                in TConfiguration inliningConfiguration)
            {
                Builder = builder;
                Landscape = landscape;
                Caller = caller;
                configuration = inliningConfiguration;
                Applied = false;
            }

            /// <summary>
            /// The associated builder.
            /// </summary>
            public IRBuilder Builder { get; }

            /// <summary>
            /// The associated function landscape.
            /// </summary>
            public FunctionLandscape Landscape { get; }

            /// <summary>
            /// The asssociated caller.
            /// </summary>
            public FunctionLandscape.Entry Caller { get; }

            /// <summary>
            /// True, if at least one call could be inlined.
            /// </summary>
            public bool Applied { get; private set; }

            /// <summary cref="Scope.IFunctionCallVisitor.Visit(FunctionCall)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Visit(FunctionCall call)
            {
                var callTarget = call.Target.Resolve();
                if (callTarget is TopLevelFunction functionValue &&
                    CanInline(call, functionValue))
                {
                    var updatedFunction = Builder.DeclareFunction(functionValue.Declaration);
                    Builder.SpecializeCall(
                        Caller.Function,
                        call,
                        updatedFunction);
                    Applied = true;
                }
                return true;
            }

            /// <summary>
            /// Returns true if the given function requires an inlining operation.
            /// </summary>
            /// <param name="call">The current call.</param>
            /// <param name="callee">The callee.</param>
            /// <returns>True, if the given function requires an inling operation.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CanInline(FunctionCall call, TopLevelFunction callee)
            {
                // Check for lambda closures
                if (callee.IsHigherOrder)
                    return true;

                // Check inlining configuration
                Landscape.TryGetEntry(callee, out FunctionLandscape.Entry entry);
                return configuration.CanInline(
                    Caller,
                    call,
                    callee,
                    entry);
            }
        }

        /// <summary cref="OrderedTransformation.PerformTransformation(IRBuilder, FunctionLandscape, FunctionLandscape.Entry)"/>
        protected override bool PerformTransformation(
            IRBuilder builder,
            FunctionLandscape landscape,
            FunctionLandscape.Entry currentEntry)
        {
            if (!currentEntry.HasReferences)
                return false;

            var visitor = new FunctionCallVisitor(
                builder,
                landscape,
                currentEntry,
                configuration);
            currentEntry.Scope.VisitFunctionCalls(ref visitor);
            return visitor.Applied;
        }
    }
}
