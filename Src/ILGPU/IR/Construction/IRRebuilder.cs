// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRRebuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// Represents flags for an <see cref="IRRebuilder"/> instance.
    /// </summary>
    [Flags]
    public enum IRRebuilderFlags
    {
        /// <summary>
        /// The default flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Allows the rebuilding of top-level functions.
        /// Note that the rebuilt functions will not be top-level
        /// functions any more. Instead, they will be mapped to
        /// default functions. This is especially useful during
        /// function specialization.
        /// </summary>
        RebuildTopLevel = 1 << 0,

        /// <summary>
        /// Allows to keep types instead of building new ones.
        /// </summary>
        KeepTypes = 1 << 1,
    }

    /// <summary>
    /// Represents an IR rebuilder to rebuild parts of the IR.
    /// </summary>
    public sealed class IRRebuilder : IRTypeRebuilder
    {
        #region Nested Types

        private readonly struct FunctionInfo
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="builder"></param>
            /// <param name="replacedParameters"></param>
            public FunctionInfo(
                FunctionBuilder builder,
                ImmutableArray<int> replacedParameters)
            {
                FunctionBuilder = builder;
                ReplacedParameters = replacedParameters;
            }

            /// <summary>
            /// Returns the associated function builder.
            /// </summary>
            public FunctionBuilder FunctionBuilder { get; }

            /// <summary>
            /// Returns the list of replaced parameters.
            /// </summary>
            public ImmutableArray<int> ReplacedParameters { get; }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Maps old nodes to new nodes.
        /// </summary>
        private readonly Dictionary<Value, Value> toNewValueMapping =
            new Dictionary<Value, Value>();

        /// <summary>
        /// Maps new nodes to old nodes.
        /// </summary>
        private readonly Dictionary<Value, Value> toOldValueMapping =
            new Dictionary<Value, Value>();

        /// <summary>
        /// Maps old functions to new function builders.
        /// </summary>
        private readonly Dictionary<FunctionValue, FunctionInfo> functionMap =
            new Dictionary<FunctionValue, FunctionInfo>();

        /// <summary>
        /// Constructs a new IR rebuilder.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        /// <param name="scope">The parent scope.</param>
        /// <param name="flags">The rebuilder flags.</param>
        internal IRRebuilder(IRBuilder builder, Scope scope, IRRebuilderFlags flags)
            : base(builder, (flags & IRRebuilderFlags.KeepTypes) == IRRebuilderFlags.KeepTypes)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));

            // Create function stubs and map parameters for all functions
            var rebuildTopLevel = (flags & IRRebuilderFlags.RebuildTopLevel) ==
                IRRebuilderFlags.RebuildTopLevel;
            foreach (var function in scope.Functions)
            {
                if (function is TopLevelFunction topLevelFunction)
                {
                    FunctionBuilder functionBuilder;
                    var returnType = Rebuild(topLevelFunction.ReturnType);

                    if (rebuildTopLevel)
                    {
                        functionBuilder = Builder.CreateFunction(function.Name);
                        functionBuilder.AddTopLevelParameters(returnType);
                    }
                    else
                    {
                        var declaration = topLevelFunction.Declaration.Specialize(
                            returnType);
                        functionBuilder = Builder.CreateFunction(declaration);
                    }
                    RebuildFunctionStub(topLevelFunction, functionBuilder);
                }
                else
                    RebuildFunctionStub(function,
                        Builder.CreateFunction(function.Name));
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the associated entry point.
        /// </summary>
        public FunctionValue Entry => Scope.Entry as FunctionValue;

        /// <summary>
        /// Returns the new entry point.
        /// </summary>
        public FunctionValue NewEntry => functionMap[Entry].FunctionBuilder.FunctionValue;

        #endregion

        #region Methods

        /// <summary>
        /// Rebuilds all nodes in the current scope.
        /// </summary>
        public FunctionValue Rebuild()
        {
            // Rebuild nodes in post order (except functions)
            using (var postOrder = Scope.PostOrder)
            {
                while (postOrder.MoveNext())
                {
                    var node = postOrder.Current;
                    Rebuild(node);
                }
            }

            // Rebuild all functions
            foreach (var function in Scope.Functions)
            {
                Debug.Assert(
                    toNewValueMapping.ContainsKey(function),
                    "Function was not visited in postorder run");
                function.Rebuild(Builder, this);
            }

            return toNewValueMapping[Entry] as FunctionValue;
        }

        /// <summary>
        /// Tries to lookup the new node representation of the given old node.
        /// </summary>
        /// <param name="oldNode">The old node.</param>
        /// <param name="newNode">The new node.</param>
        /// <returns>True, iff a corresponding new node could be found.</returns>
        public bool TryLookupNewNode(Value oldNode, out Value newNode)
        {
            return toNewValueMapping.TryGetValue(oldNode, out newNode);
        }

        /// <summary>
        /// Tries to lookup the old node representation of the given new node.
        /// </summary>
        /// <param name="newNode">The new node.</param>
        /// <param name="oldNode">The old node.</param>
        /// <returns>True, iff a corresponding old node could be found.</returns>
        public bool TryLookupOldNode(Value newNode, out Value oldNode)
        {
            return toOldValueMapping.TryGetValue(newNode, out oldNode);
        }

        /// <summary>
        /// Maps the old node to the new node.
        /// </summary>
        /// <param name="oldNode">The old node.</param>
        /// <param name="newNode">The new node.</param>
        public void Map(Value oldNode, Value newNode)
        {
            if (oldNode == null)
                throw new ArgumentNullException(nameof(oldNode));
            if (newNode == null)
                throw new ArgumentNullException(nameof(newNode));
            MapInternal(oldNode, newNode);
        }

        /// <summary>
        /// Maps the old node to the new node.
        /// </summary>
        /// <param name="oldNode">The old node.</param>
        /// <param name="newNode">The new node.</param>
        internal void MapInternal(Value oldNode, Value newNode)
        {
            Debug.Assert(oldNode != null, "Invalid old node");
            Debug.Assert(newNode != null, "Invalid new node");
            toNewValueMapping[oldNode] = newNode;
            toOldValueMapping[newNode] = oldNode;
        }

        /// <summary>
        /// Registers a mapping from source to target.
        /// </summary>
        /// <param name="source">The source node.</param>
        /// <param name="target">The target node.</param>
        public void RegisterNodeMapping(Value source, Value target)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            MapInternal(source, target);
        }

        /// <summary>
        /// Imports the given node mapping.
        /// </summary>
        /// <typeparam name="TDictionary">The dictionary type.</typeparam>
        /// <param name="source">The source dictionary.</param>
        public void RegisterNodeMapping<TDictionary>(TDictionary source)
            where TDictionary : IReadOnlyDictionary<Value, Value>
        {
            foreach (var node in source)
                MapInternal(node.Key, node.Value);
        }

        /// <summary>
        /// Exports the internal node mapping to the given target dictionary.
        /// </summary>
        /// <typeparam name="TDictionary">The dictionary type.</typeparam>
        /// <param name="target">The target dictionary.</param>
        public void ExportNodeMapping<TDictionary>(TDictionary target)
            where TDictionary : IDictionary<Value, Value>
        {
            foreach (var node in toNewValueMapping)
                target[node.Key] = node.Value;
        }

        /// <summary>
        /// Rebuils a function stub.
        /// </summary>
        /// <param name="function">The source function.</param>
        /// <param name="newFunction">The target builder.</param>
        private void RebuildFunctionStub(
            FunctionValue function,
            FunctionBuilder newFunction)
        {
            var attachedParameters = function.AttachedParameters;
            var replacedParameters = ImmutableArray.CreateBuilder<int>(attachedParameters.Length);
            for (int i = 0, e = attachedParameters.Length; i < e; ++i)
            {
                var paramRef = function.AttachedParameters[i];
                var directTarget = paramRef.DirectTarget;

                // Check for replaced parameters
                if (directTarget.IsReplaced)
                {
                    replacedParameters.Add(i);
                    continue;
                }

                // Check for dead parameters.
                using (var usesEnumerator = directTarget.Uses.GetEnumerator())
                {
                    if (!usesEnumerator.MoveNext())
                    {
                        replacedParameters.Add(i);
                        continue;
                    }
                }

                var directParameter = directTarget as Parameter;
                Parameter newParam;
                if (function.IsTopLevel && i < 2)
                    newParam = newFunction[i];
                else
                {
                    var newParamType = Rebuild(directTarget.Type);
                    newParam = newFunction.AddParameter(newParamType, directParameter?.Name);
                }
                MapInternal(directTarget, newParam);
            }
            newFunction.SealParameters();
            var parameters = replacedParameters.Count == attachedParameters.Length ?
                replacedParameters.MoveToImmutable() :
                replacedParameters.ToImmutable();
            functionMap.Add(function, new FunctionInfo(newFunction, parameters));
        }

#if VERIFICATION
        /// <summary>
        /// Verifies access to the given node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        private void VerifyAccess(Value node)
        {
            Debug.Assert(node is TopLevelFunction || Scope.Contains(node));
        }
#endif

        /// <summary>
        /// Resolves a list of replaced parameters for the given function.
        /// </summary>
        /// <param name="function">The function to resolve.</param>
        /// <returns>The resolved replaced parameters.</returns>
        public ImmutableArray<int> ResolveReplacedParameters(FunctionValue function)
        {
            Debug.Assert(
                function != null && functionMap.ContainsKey(function),
                "Invalid function to resolve");
            return functionMap[function].ReplacedParameters;
        }

        /// <summary>
        /// Resolves a function builder for the given function.
        /// </summary>
        /// <param name="function">The function to resolve.</param>
        /// <returns>The resolved function builder.</returns>
        public FunctionBuilder ResolveFunctionBuilder(FunctionValue function)
        {
            Debug.Assert(
                function != null && functionMap.ContainsKey(function),
                "Invalid function to resolve");
            return functionMap[function].FunctionBuilder;
        }

        /// <summary>
        /// Rebuilds to given source node using lookup tables.
        /// </summary>
        /// <param name="source">The source node.</param>
        /// <returns>The new node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Value Rebuild(Value source)
        {
            Debug.Assert(!source.IsReplaced, "Trying to rebuild a replaced node");
#if VERIFICATION
            VerifyAccess(source);
#endif
            if (TryLookupNewNode(source, out Value node))
                return node;
            Debug.Assert(!(source is Parameter), "Invalid recursive parameter rebuilding process");
            if (source is FunctionValue sourceFunction)
            {
                if (sourceFunction is TopLevelFunction topLevelFunction &&
                    !functionMap.ContainsKey(sourceFunction))
                    return Builder.DeclareFunction(topLevelFunction.Declaration);
                node = functionMap[sourceFunction].FunctionBuilder.FunctionValue;
            }
            else
                node = source.Rebuild(Builder, this);
            MapInternal(source, node);
            return node;
        }

        /// <summary>
        /// Rebuilds to given source node using lookup tables and
        /// returns the resolved casted to a specific type.
        /// </summary>
        /// <typeparam name="T">The target type to cast the new node to.</typeparam>
        /// <param name="source">The source node.</param>
        /// <returns>The new node.</returns>
        public T RebuildAs<T>(Value source)
            where T : Value =>
            Rebuild(source) as T;

        #endregion
    }
}
