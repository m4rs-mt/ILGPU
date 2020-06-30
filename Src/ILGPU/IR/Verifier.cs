﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Verifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// An IR verification result.
    /// </summary>
    internal sealed class VerificationResult : IDumpable
    {
        #region Instance

        /// <summary>
        /// The internal mapping of error messages.
        /// </summary>
        private readonly Dictionary<Node, string> errors =
            new Dictionary<Node, string>();

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of collected errors.
        /// </summary>
        public int Count => errors.Count;

        /// <summary>
        /// Returns true if there are any errors.
        /// </summary>
        public bool HasErrors => Count > 0;

        #endregion

        #region Methods

        /// <summary>
        /// Dumps all errors to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public void Dump(TextWriter textWriter)
        {
            foreach (var entry in errors)
            {
                textWriter.WriteLine(
                    entry.Key.FormatErrorMessage(entry.Value));
            }
        }

        /// <summary>
        /// Reports a new error.
        /// </summary>
        /// <param name="node">The associated node the message belongs to.</param>
        /// <param name="verifier">The verifier class.</param>
        /// <param name="stage">The verification stage.</param>
        public void ReportError(Node node, Type verifier, string stage)
        {
            errors[node] = $"{verifier.Name} in {stage}";

            // Break in the case of an attached debugger
            Debugger.Break();
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns a string representation of all errors.
        /// </summary>
        /// <returns>The string representation of all errors.</returns>
        public override string ToString()
        {
            using var builder = new StringWriter();
            Dump(builder);
            return builder.ToString();
        }

        #endregion
    }

    /// <summary>
    /// A verifier to verify the structure of an IR method.
    /// </summary>
    internal class Verifier
    {
        #region Nested Types

        //
        // Note that these classes do not use optimized or specialized data structures
        // or algorithms in an efficient way to avoid unnecessary programming issues.
        //

        /// <summary>
        /// An abstract verifier.
        /// </summary>
        private abstract class VerifierBase
        {
            #region Instance

            /// <summary>
            /// Constructs a new verifier base.
            /// </summary>
            /// <param name="method">The method to verify.</param>
            /// <param name="result">The verification result.</param>
            protected VerifierBase(Method method, VerificationResult result)
            {
                Method = method ?? throw new ArgumentNullException(nameof(method));
                Result = result ?? throw new ArgumentNullException(nameof(result));
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the method to verify.
            /// </summary>
            public Method Method { get; }

            /// <summary>
            /// The verification result.
            /// </summary>
            public VerificationResult Result { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Reports a new error.
            /// </summary>
            /// <param name="node">The target node.</param>
            /// <param name="stage">The verification stage.</param>
            protected void Error(Node node, [CallerMemberName] string stage = "") =>
                Result.ReportError(node, GetType(), stage);

            protected void Assert(
                Node node,
                bool condition,
                [CallerMemberName] string stage = "")
            {
                if (!condition)
                    Result.ReportError(node, GetType(), stage);
            }

            /// <summary>
            /// Performs the verification step.
            /// </summary>
            public abstract void Verify();

            #endregion
        }

        /// <summary>
        /// Verifiers the general structure of the control flow.
        /// </summary>
        private sealed class ControlFlowVerifier : VerifierBase
        {
            #region Instance

            private readonly List<BasicBlock> blocksInRpo =
                new List<BasicBlock>();
            private readonly HashSet<BasicBlock> containedBlocks =
                new HashSet<BasicBlock>();
            private readonly Dictionary<BasicBlock, HashSet<BasicBlock>> predecessors =
                new Dictionary<BasicBlock, HashSet<BasicBlock>>();
            private readonly Dictionary<BasicBlock, HashSet<BasicBlock>> successors =
                new Dictionary<BasicBlock, HashSet<BasicBlock>>();

            /// <summary>
            /// Constructs a new control-flow verifier.
            /// </summary>
            /// <param name="method">The method to verify.</param>
            /// <param name="result">The verification result.</param>
            public ControlFlowVerifier(Method method, VerificationResult result)
                : base(method, result)
            { }

            #endregion

            #region Methods

            /// <summary>
            /// Creates new predecessor and successor ink sets.
            /// </summary>
            /// <param name="block">The current block.</param>
            private void CreateLinkSets(BasicBlock block)
            {
                if (!predecessors.ContainsKey(block))
                    predecessors.Add(block, new HashSet<BasicBlock>());
                if (!successors.ContainsKey(block))
                    successors.Add(block, new HashSet<BasicBlock>());
            }

            /// <summary>
            /// Adds a <paramref name="successor"/> to the given
            /// <paramref name="block"/>.
            /// </summary>
            /// <param name="block">The current block.</param>
            /// <param name="successor">The successor to add.</param>
            private void AddSuccessor(BasicBlock block, BasicBlock successor)
            {
                CreateLinkSets(block);
                CreateLinkSets(successor);

                successors[block].Add(successor);
                predecessors[successor].Add(block);
            }

            /// <summary>
            /// Computes the post order of the given block recursively.
            /// </summary>
            /// <param name="block">The current block.</param>
            private void ComputePostOrder(BasicBlock block)
            {
                // Skip visited blocks
                if (!containedBlocks.Add(block))
                    return;
                CreateLinkSets(block);

                // Iterate over all successors to enumerate their children
                block.AssertNoControlFlowUpdate();
                foreach (var successor in block.Successors)
                {
                    AddSuccessor(block, successor);
                    ComputePostOrder(successor);
                }

                // Register our block in the post-order list
                blocksInRpo.Add(block);
            }

            /// <summary>
            /// Verifies the existence and the order of all blocks.
            /// </summary>
            private void VerifyBlocks()
            {
                var blocks = Method.Blocks;
                Assert(Method, blocks.Count == blocksInRpo.Count);

                int index = 0;
                foreach (var block in blocks)
                    Assert(Method, block == blocksInRpo[index++]);
            }

            /// <summary>
            /// Verifies all links.
            /// </summary>
            private void VerifyLinks()
            {
                foreach (var block in blocksInRpo)
                {
                    var successorSet = successors[block];
                    foreach (var successor in block.CurrentSuccessors)
                        Assert(block, successorSet.Contains(successor));
                    foreach (var successor in block.Successors)
                        Assert(block, successorSet.Contains(successor));

                    var predecessorSet = predecessors[block];
                    foreach (var predecessor in block.Predecessors)
                        Assert(block, predecessorSet.Contains(predecessor));
                }
            }

            /// <summary>
            /// Verifies all exit blocks.
            /// </summary>
            private void VerifyExitBlock()
            {
                var exitBlock = Method.Blocks.FindExitBlock();
                Assert(Method, exitBlock != null);

                foreach (var block in Method.Blocks)
                {
                    if (block.Successors.Length < 1)
                        Assert(block, block == exitBlock);
                }
            }

            /// <summary>
            /// Performs the control-flow verification step.
            /// </summary>
            public override void Verify()
            {
                ComputePostOrder(Method.EntryBlock);
                blocksInRpo.Reverse();

                VerifyBlocks();
                VerifyLinks();
                VerifyExitBlock();
            }

            #endregion
        }

        /// <summary>
        /// Verifiers the general SSA value properties of the program.
        /// </summary>
        private sealed class ValueVerifier : VerifierBase
        {
            #region Instance

            private readonly HashSet<Value> values = new HashSet<Value>();
            private readonly Dictionary<BasicBlock, HashSet<Value>> mapping =
                new Dictionary<BasicBlock, HashSet<Value>>();

            /// <summary>
            /// Constructs a new value verifier.
            /// </summary>
            /// <param name="method">The method to verify.</param>
            /// <param name="result">The verification result.</param>
            public ValueVerifier(Method method, VerificationResult result)
                : base(method, result)
            { }

            #endregion

            #region Methods

            /// <summary>
            /// Verifies all value operands for defined an unbound values.
            /// </summary>
            private void VerifyValues()
            {
                foreach (var block in Method.Blocks)
                    mapping.Add(block, new HashSet<Value>());

                foreach (var param in Method.Parameters)
                {
                    values.Add(param);
                    mapping[Method.EntryBlock].Add(param);
                }

                foreach (var block in Method.Blocks)
                {
                    block.AssertNoControlFlowUpdate();
                    var valueSet = mapping[block];

                    // Bind value and check for defined operands
                    foreach (Value value in block)
                    {
                        Assert(value, values.Add(value));
                        Assert(value, valueSet.Add(value));

                        if (value is PhiValue)
                            continue;

                        // Check for defined nodes
                        foreach (Value node in value.Nodes)
                        {
                            if (node is UndefinedValue)
                                continue;
                            Assert(value, values.Contains(node));
                        }
                    }
                }
            }

            /// <summary>
            /// Verifies all value-block associations.
            /// </summary>
            private void VerifyValueBlockAssociations()
            {
                // Verify the global block value collection
                foreach (var value in Method.Values)
                {
                    bool foundBlock = mapping.TryGetValue(
                        value.BasicBlock,
                        out var blockValues);
                    Assert(value, foundBlock);
                    if (!foundBlock)
                        continue;
                    Assert(value.BasicBlock, blockValues.Contains(value));
                }
            }

            /// <summary>
            /// Tries to find a value recursively in all predecessors.
            /// </summary>
            /// <param name="visited">The set of visited nodes.</param>
            /// <param name="currentBlock">The current block.</param>
            /// <param name="toFind">The value to find.</param>
            /// <returns>True, if the value could be found.</returns>
            private bool FindValueRecursive(
                HashSet<BasicBlock> visited,
                BasicBlock currentBlock,
                Value toFind)
            {
                if (!visited.Add(currentBlock))
                    return false;

                if (mapping[currentBlock].Contains(toFind))
                    return true;

                foreach (var predecessor in currentBlock.Predecessors)
                {
                    if (FindValueRecursive(visited, predecessor, toFind))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Verifies all phi node references.
            /// </summary>
            private void VerifyPhis() =>
                Method.Blocks.ForEachValue<PhiValue>(phiValue =>
                {
                    var visited = new HashSet<BasicBlock>();
                    for (int i = 0, e = phiValue.Nodes.Length; i < e; ++i)
                    {
                        Value value = phiValue.Nodes[i];
                        var source = phiValue.Sources[i];

                        // Ensure that the source is there
                        Assert(phiValue, mapping.ContainsKey(source));

                        // Try to resolve a value on this path
                        if (value is UndefinedValue)
                            continue;

                        visited.Clear();
                        Assert(
                            phiValue,
                            FindValueRecursive(
                                visited,
                                source,
                                value));
                    }
                });

            /// <summary>
            /// Performs the SSA-value verification step.
            /// </summary>
            public override void Verify()
            {
                VerifyValues();
                VerifyValueBlockAssociations();
                VerifyPhis();
            }

            #endregion
        }

        /// <summary>
        /// A verifier that does not perform any verification steps.
        /// </summary>
        private sealed class NoVerifier : Verifier
        {
            private static readonly VerificationResult NoResult =
                new VerificationResult();

            /// <summary>
            /// Performs no verification step.
            /// </summary>
            public override void Verify(Method method) { }

            /// <summary>
            /// Returns an empty verification result.
            /// </summary>
            public override VerificationResult VerifyToResult(Method method) =>
                NoResult;

            /// <summary>
            /// Performs a verification step for all methods that should be verified.
            /// </summary>
            /// <typeparam name="TPredicate">The collection predicate.</typeparam>
            /// <param name="methods">The methods to verify.</param>
            public override void Verify<TPredicate>(MethodCollection<TPredicate> methods)
            { }
        }

        #endregion

        #region Static

        /// <summary>
        /// Returns a verifier instance that verifies all IR methods.
        /// </summary>
        public static readonly Verifier Instance = new Verifier();

        /// <summary>
        /// Returns an empty verifier that does not perform any verification steps.
        /// </summary>
        public static readonly Verifier Empty = new NoVerifier();

        /// <summary>
        /// Verifies the given IR method.
        /// </summary>
        /// <param name="method">The method to verify.</param>
        /// <returns>The created verification result object.</returns>
        public static VerificationResult ApplyVerification(Method method)
        {
            var result = new VerificationResult();

            void Verify<T>() where T : VerifierBase
            {
                var instance = Activator.CreateInstance(typeof(T), method, result)
                    as VerifierBase;
                instance.Verify();
            }

            Verify<ControlFlowVerifier>();
            Verify<ValueVerifier>();

            return result;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a verifier instance.
        /// </summary>
        private Verifier() { }

        #endregion

        #region Methods

        /// <summary>
        /// Verifies the given IR method and throws an
        /// <see cref="InvalidOperationException"/> in case of an error.
        /// </summary>
        /// <param name="method">The method to verify.</param>
        public virtual void Verify(Method method)
        {
            var result = VerifyToResult(method);
            if (result.HasErrors)
            {
                result.DumpToError();
                throw method.GetInvalidOperationException(result.ToString());
            }
        }

        /// <summary>
        /// Verifies the given IR method.
        /// </summary>
        /// <param name="method">The method to verify.</param>
        /// <returns>The created verification result object.</returns>
        public virtual VerificationResult VerifyToResult(Method method) =>
            ApplyVerification(method);

        /// <summary>
        /// Performs a verification step for all methods that should be verified.
        /// </summary>
        /// <typeparam name="TPredicate">The collection predicate.</typeparam>
        /// <param name="methods">The methods to verify.</param>
        public virtual void Verify<TPredicate>(MethodCollection<TPredicate> methods)
            where TPredicate : IMethodCollectionPredicate
        {
            foreach (var method in methods)
                Verify(method);
        }

        #endregion
    }

}
