// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRContext.GC.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
#if VERIFICATION
using System.Diagnostics;
#endif
#if PARALLEL_PROCESSING || VERIFICATION
using System.Threading.Tasks;
#endif

namespace ILGPU.IR
{
    partial class IRContext
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GCIntrinsicType(TypeNode typeNode)
        {
            unifiedTypes.Add(typeNode, typeNode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GCOnTypes()
        {
            unifiedTypes.Clear();

            // Adjust generation of basic types
            for (int i = 1, e = basicValueTypes.Length; i < e; ++i)
                GCIntrinsicType(basicValueTypes[i]);

            // Recover intrinsic types
            GCIntrinsicType(VoidType);
            GCIntrinsicType(MemoryType);
            GCIntrinsicType(StringType);
            GCIntrinsicType(IndexType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GCIntoNextGeneration(
            TopLevelFunction topLevelFunction,
            IRBuilder builder)
        {
            var scope = Scope.Create(this, topLevelFunction);
            var rebuilder = builder.CreateRebuilder(
                scope,
                IRRebuilderFlags.KeepTypes);
            rebuilder.Rebuild();
        }

#if VERIFICATION
        private readonly struct VerifyFunctionAccessVisitor : FunctionCall.IFunctionArgumentVisitor
        {
            public VerifyFunctionAccessVisitor(List<TopLevelFunction> dirtyFunctions)
            {
                DirtyFunctions = new HashSet<TopLevelFunction>(dirtyFunctions);
            }

            public HashSet<TopLevelFunction> DirtyFunctions { get; }

            public void VisitFunctionArgument(FunctionValue functionValue)
            {
                if (functionValue is TopLevelFunction topLevelFunction)
                {
                    if (DirtyFunctions.Contains(topLevelFunction))
                        throw new InvalidOperationException("A non-dirty function cannot reference a dirty function");
                }
            }
        }
#endif

        /// <summary>
        /// Performs the actual GC process.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GCInternal()
        {
            Interlocked.Add(ref generationCounter, 1);

            var oldTopLevelFunctions = topLevelFunctions.ToArray();
            topLevelFunctions.Clear();

            var dirtyFunctions = new List<TopLevelFunction>(oldTopLevelFunctions.Length);
            var normalFunctions = new List<TopLevelFunction>(oldTopLevelFunctions.Length);

            foreach (var function in oldTopLevelFunctions)
            {
                if (function.HasTransformationFlags(TopLevelFunctionTransformationFlags.Dirty))
                    dirtyFunctions.Add(function);
                else
                {
                    normalFunctions.Add(function);

                    // Recover normal function
                    topLevelFunctions.Register(function.Handle, function);
                }
            }

#if VERIFICATION
            // Note: it cannot happen that a normal function refers to
            // a dirty function in our use cases. If this happens, the other
            // function should have been also marked as dirty.

            var verifyFunctionAccessVisitor = new VerifyFunctionAccessVisitor(dirtyFunctions);
            foreach (var function in normalFunctions)
            {
                var scope = Scope.Create(this, function);
                foreach (var node in scope)
                {
                    if (node.IsReplaced)
                        throw new InvalidOperationException("Cannot save a replaced node into the next generation");

                    if (node is FunctionCall call)
                        call.VisitFunctionArguments(ref verifyFunctionAccessVisitor);
                }
            }

            Parallel.ForEach(normalFunctions, function =>
            {
                function.Generation = CurrentGeneration;

                var scope = Scope.Create(this, function);
                foreach (var node in scope)
                    node.Generation = CurrentGeneration;
            });
#endif


            using (var builder = CreateBuilder(IRBuilderFlags.PreserveTopLevelFunctions))
            {
                // Prepare all dirty functions
                foreach (var function in dirtyFunctions)
                    builder.DeclareFunction(function.Declaration);

#if PARALLEL_PROCESSING
                Parallel.ForEach(dirtyFunctions, function =>
                        GCIntoNextGeneration(function, builder));
#else
                foreach (var function in dirtyFunctions)
                    GCIntoNextGeneration(function, builder);
#endif
            }
        }

        /// <summary>
        /// Rebuilds all nodes and clears up the IR.
        /// </summary>
        /// <remarks>
        /// This method must not be invoked in the context of other
        /// parallel operations using this context.
        /// </remarks>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods",
            Justification = "Users might want to force a global GC to free memory after an internal ILGPU GC run")]
        public void GC()
        {
            irLock.EnterWriteLock();
            try
            {
                GCInternal();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                irLock.ExitWriteLock();
            }

#if VERIFICATION
            Verify();
#endif

            if ((Flags & IRContextFlags.ForceSystemGC) == IRContextFlags.ForceSystemGC)
                System.GC.Collect();
        }

        /// <summary>
        /// Clears this context and removes all nodes
        /// </summary>
        public void Clear()
        {
            irLock.EnterWriteLock();
            try
            {
                topLevelFunctions.Clear();
                GCOnTypes();
            }
            finally
            {
                irLock.ExitWriteLock();
            }
        }
    }
}
