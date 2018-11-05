// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionCalls.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single function call of the form
    /// f(a0, ..., an-1)
    /// </summary>
    public sealed class FunctionCall : InstantiatedValue
    {
        #region Nested Types

        /// <summary>
        /// A visitor interface to visit all call targets of this call.
        /// </summary>
        public interface ITargetVisitor
        {
            /// <summary>
            /// Visits the given call target (<see cref="FunctionValue"/> or <see cref="Parameter"/>).
            /// </summary>
            /// <param name="callTarget">The called node.</param>
            /// <returns>False, if the traversal should be stopped.</returns>
            bool VisitCallTarget(Value callTarget);
        }

        /// <summary>
        /// A visitor interface to visit all function arguments of this call.
        /// </summary>
        public interface IFunctionArgumentVisitor
        {
            /// <summary>
            /// Visits the given function argument.
            /// </summary>
            /// <param name="functionValue">The passed function value.</param>
            void VisitFunctionArgument(FunctionValue functionValue);
        }

        /// <summary>
        /// A simple target visitor that searches the first function.
        /// </summary>
        /// <typeparam name="T">The function call type.</typeparam>
        private struct FirstTargetVisitor<T> : ITargetVisitor
            where T : Value
        {
            /// <summary>
            /// Returns the first function target.
            /// </summary>
            public T FirstTarget { get; private set; }

            /// <summary cref="ITargetVisitor.VisitCallTarget(Value)"/>
            public bool VisitCallTarget(Value callTarget)
            {
                FirstTarget = callTarget as T;
                return FirstTarget == null;
            }
        }

        /// <summary>
        /// A simple target visitor that searches for the first non-local call.
        /// </summary>
        private struct NonLocalCallVisitor : ITargetVisitor
        {
            /// <summary>
            /// Returns the first non-local target.
            /// </summary>
            public Value Target { get; private set; }

            /// <summary cref="ITargetVisitor.VisitCallTarget(Value)"/>
            public bool VisitCallTarget(Value callTarget)
            {
                Target = callTarget as Parameter;
                if (Target == null)
                    Target = callTarget as TopLevelFunction;
                return Target == null;
            }
        }

        /// <summary>
        /// A parameter replacement visitor to detect replaced parameters.
        /// </summary>
        private struct GatherReplacedParametersVisitor : ITargetVisitor
        {
            /// <summary>
            /// Constructs a new replacement visitor.
            /// </summary>
            /// <param name="rebuilder">The associated rebuilder.</param>
            public GatherReplacedParametersVisitor(IRRebuilder rebuilder)
            {
                Debug.Assert(rebuilder != null, "Invalid rebuilder");
                Rebuilder = rebuilder;
                ReplacedParameters = ImmutableArray<int>.Empty;
            }

            /// <summary>
            /// Returns the associated rebuilder.
            /// </summary>
            public IRRebuilder Rebuilder { get; }

            /// <summary>
            /// Returns the replaced parameters.
            /// </summary>
            public ImmutableArray<int> ReplacedParameters { get; private set; }

            /// <summary cref="ITargetVisitor.VisitCallTarget(Value)"/>
            public bool VisitCallTarget(Value node)
            {
                if (!Rebuilder.TryLookupNewNode(node, out Value newNode))
                    return true;

                if (node is FunctionValue oldFunction &&
                    newNode is FunctionValue newFunction &&
                    newFunction as TopLevelFunction == null)
                {
                    var replacedParams = Rebuilder.ResolveReplacedParameters(oldFunction);
                    if (ReplacedParameters.IsDefaultOrEmpty)
                        ReplacedParameters = replacedParams;
#if DEBUG
                    // Verify
                    else
                    {
                        for (int i = 0, e = ReplacedParameters.Length; i < e; ++i)
                            Debug.Assert(ReplacedParameters[i] == replacedParams[i], "Invalid parameter replacement");
                    }
#else
                    return false;
#endif
                }

                return true;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new call.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="target">The jump target.</param>
        /// <param name="arguments">The arguments of the jump target.</param>
        /// <param name="voidType">The void type.</param>
        internal FunctionCall(
            ValueGeneration generation,
            ValueReference target,
            ImmutableArray<ValueReference> arguments,
            VoidType voidType)
            : base(generation, true)
        {
            Debug.Assert(
                target.Resolve() is FunctionValue || target.Type.IsFunctionType,
                "Invalid function type");

            Arguments = arguments;

            var builder = ImmutableArray.CreateBuilder<ValueReference>(arguments.Length + 1);
            builder.Add(target);
            builder.AddRange(arguments);
            Seal(builder.MoveToImmutable(), voidType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the jump target.
        /// </summary>
        public ValueReference Target => Nodes[0];

        /// <summary>
        /// Returns true iff this function has a valid jump target.
        /// </summary>
        public bool HasTarget => Nodes.Length > 0;

        /// <summary>
        /// Returns all referenced arguments.
        /// </summary>
        public ImmutableArray<ValueReference> Arguments { get; private set; }

        /// <summary>
        /// Returns the number of the associated arguments.
        /// </summary>
        public int NumArguments => Arguments.Length;

        /// <summary>
        /// Returns true if this call calls at least one top level function.
        /// </summary>
        public bool IsTopLevelCall
        {
            get
            {
                var visitor = new FirstTargetVisitor<TopLevelFunction>();
                VisitCallTargets(ref visitor);
                return visitor.FirstTarget != null;
            }
        }

        /// <summary>
        /// Returns true if this call calls at least one parameter.
        /// </summary>
        public bool IsParamCall
        {
            get
            {
                var visitor = new FirstTargetVisitor<Parameter>();
                VisitCallTargets(ref visitor);
                return visitor.FirstTarget != null;
            }
        }

        /// <summary>
        /// Returns true if this call calls at least one non-local function.
        /// </summary>
        public bool IsNonLocalCall
        {
            get
            {
                var visitor = new NonLocalCallVisitor();
                VisitCallTargets(ref visitor);
                return visitor.Target != null;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a reference to the requested argument.
        /// </summary>
        /// <param name="index">The argument index.</param>
        /// <returns>A reference to the requested argument.</returns>
        public ValueReference GetArgument(int index) => Arguments[index];

        /// <summary>
        /// Visits all target nodes using the given visitor.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <param name="visitor">The actual visitor.</param>
        public void VisitCallTargets<TVisitor>(ref TVisitor visitor)
            where TVisitor : ITargetVisitor =>
            VisitTargets(ref visitor, Target);

        /// <summary>
        /// Recursively visits all target nodes.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <param name="visitor">The actual visitor.</param>
        /// <param name="value">The current value.</param>
        private bool VisitTargets<TVisitor>(
            ref TVisitor visitor,
            Value value)
            where TVisitor : ITargetVisitor
        {
            switch (value)
            {
                case FunctionValue _:
                case Parameter _:
                    return visitor.VisitCallTarget(value);
                case Conditional conditional:
                    foreach (var node in conditional.Arguments)
                    {
                        if (!VisitTargets(ref visitor, node))
                            return false;
                    }
                    break;
            }
            return true;
        }

        /// <summary>
        /// Visits all function arguments using the given visitor.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <param name="visitor">The actual visitor.</param>
        public void VisitFunctionArguments<TVisitor>(ref TVisitor visitor)
            where TVisitor : IFunctionArgumentVisitor
        {
            foreach (var arg in Arguments)
                VisitArguments(ref visitor, arg);
        }

        /// <summary>
        /// Recursively visits all argument nodes.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <param name="visitor">The actual visitor.</param>
        /// <param name="value">The current value.</param>
        private void VisitArguments<TVisitor>(
            ref TVisitor visitor,
            Value value)
            where TVisitor : IFunctionArgumentVisitor
        {
            switch (value)
            {
                case FunctionValue functionValue:
                    visitor.VisitFunctionArgument(functionValue);
                    break;
                case Conditional conditional:
                    foreach (var node in conditional.Arguments)
                        VisitArguments(ref visitor, node);
                    break;
            }
        }

        /// <summary>
        /// Resolves the first function target.
        /// </summary>
        /// <returns>The first function target.</returns>
        public FunctionValue ResolveFirstFunctionTarget()
        {
            var visitor = new FirstTargetVisitor<FunctionValue>();
            VisitCallTargets(ref visitor);
            return visitor.FirstTarget;
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            var target = rebuilder.Rebuild(Target);

            // -> We have to ignore arguments that will not be accessed anymore
            // since the function might have been specialized with a parameter value.
            var visitor = new GatherReplacedParametersVisitor(rebuilder);
            VisitCallTargets(ref visitor);
            var replacedParameters = visitor.ReplacedParameters;

            var argumentBuilder = ImmutableArray.CreateBuilder<ValueReference>(Arguments.Length);
            ImmutableArray<ValueReference> arguments;
            if (replacedParameters.Length > 0)
            {
                for (int i = 0, j = 0, e = NumArguments, e2 = replacedParameters.Length; i < e; ++i)
                {
                    if (j < e2 && i == replacedParameters[j])
                        ++j;
                    else
                        argumentBuilder.Add(rebuilder.Rebuild(Arguments[i]));
                }
                arguments = argumentBuilder.ToImmutable();
            }
            else
            {
                foreach (var arg in Arguments)
                    argumentBuilder.Add(rebuilder.Rebuild(arg));
                arguments = argumentBuilder.MoveToImmutable();
            }

            return builder.CreateFunctionCall(target, arguments);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "call";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = new StringBuilder();
            result.Append(Target.ToString());
            result.Append('(');
            for (int i = 0, e = NumArguments; i < e; ++i)
            {
                result.Append(Arguments[i].ToString());
                if (i + 1 < e)
                    result.Append(", ");
            }
            result.Append(')');
            return result.ToString();
        }

        #endregion
    }
}
