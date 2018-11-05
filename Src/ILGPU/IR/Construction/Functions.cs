// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Functions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a temp call target to wrap the actual one in
        /// order to product a temporary function without arguments.
        /// </summary>
        /// <param name="actualTarget">The target to wrap.</param>
        /// <param name="arguments">The call arguments.</param>
        /// <returns>The wrapped temp target.</returns>
        public FunctionValue CreateTempTarget(
            Value actualTarget,
            ImmutableArray<ValueReference> arguments)
        {
            var target = actualTarget as FunctionValue;
            Debug.Assert(
                target != null && target as TopLevelFunction == null,
                "Not supported temp target");
            var builder = CreateFunction(target.Name + "_temp");
            var tempCall = CreateFunctionCall(actualTarget, arguments);
            return builder.Seal(tempCall);
        }

        /// <summary>
        /// Creates a new conditional call node.
        /// </summary>
        /// <param name="conditional">The branch condition.</param>
        /// <param name="trueTarget">The false branch target.</param>
        /// <param name="falseTarget">The true branch target.</param>
        /// <param name="arguments">The target arguments.</param>
        /// <returns>A function call.</returns>
        public FunctionCall CreateFunctionCall(
            Value conditional,
            Value trueTarget,
            Value falseTarget,
            ImmutableArray<ValueReference> arguments)
        {
            Debug.Assert(conditional != null, "Invalid conditional reference");
            Debug.Assert(trueTarget != null, "Invalid true target value");
            Debug.Assert(falseTarget != null, "Invalid false target value");

            var trueType = trueTarget.Type as FunctionType;
            Debug.Assert(trueType != null, "Invalid true target type");

            var falseType = falseTarget.Type as FunctionType;
            Debug.Assert(falseType != null, "Invalid false target type");

            if (trueType != falseType)
            {
                Debug.Assert(
                    trueType.NumChildren == 0 || falseType.NumChildren == 0,
                    "Incompatible branch targets");

                // We need to create a dummy call target that performs the actual
                // calls to the target functions.
                if (trueType.NumChildren == arguments.Length)
                    trueTarget = CreateTempTarget(trueTarget, arguments);
                else
                    falseTarget = CreateTempTarget(falseTarget, arguments);
                arguments = ImmutableArray<ValueReference>.Empty;
            }

            conditional = CreatePredicate(
                conditional,
                trueTarget,
                falseTarget);
            return CreateFunctionCall(conditional, arguments);
        }

        /// <summary>
        /// Creates a new conditional call node.
        /// </summary>
        /// <param name="selectionValue">The conditional selection value.</param>
        /// <param name="targets">The selection targets.</param>
        /// <param name="arguments">The target arguments.</param>
        /// <returns>A function call.</returns>
        public FunctionCall CreateFunctionCall(
            Value selectionValue,
            ImmutableArray<ValueReference> targets,
            ImmutableArray<ValueReference> arguments)
        {
            var predicate = CreateSelectPredicate(
                selectionValue,
                targets);
            return CreateFunctionCall(predicate, arguments);
        }

        /// <summary>
        /// Creates a new call node.
        /// </summary>
        /// <param name="target">The jump target.</param>
        /// <param name="arguments">The target arguments.</param>
        /// <returns>A function call.</returns>
        public FunctionCall CreateFunctionCall(
            Value target,
            ImmutableArray<ValueReference> arguments)
        {
            Debug.Assert(target != null, "Invalid target node");
            Debug.Assert(
                target as FunctionValue != null || target.Type.IsFunctionType,
                "Invalid function target");

            return Context.CreateInstantiated(new FunctionCall(
                Generation,
                target,
                arguments,
                VoidType));
        }

        /// <summary>
        /// Creates a nested function.
        /// </summary>
        /// <returns>A function builder.</returns>
        public FunctionBuilder CreateFunction() =>
            CreateFunction((string)null);

        /// <summary>
        /// Creates a nested function.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <returns>A function builder.</returns>
        public FunctionBuilder CreateFunction(string name)
        {
            return new FunctionBuilder(this,
                Context.CreateInstantiated(new FunctionValue(
                    Generation,
                    name)));
        }

        /// <summary>
        /// Creates a top-level function that corresponds to the given declaration.
        /// </summary>
        /// <param name="declaration">The function declaration.</param>
        /// <param name="newlyCreated">True, iff the function builder has been created.</param>
        /// <returns>A function builder.</returns>
        public FunctionBuilder CreateFunction(
            FunctionDeclaration declaration,
            out bool newlyCreated)
        {
            if (declaration.ReturnType == null)
                throw new ArgumentNullException(nameof(declaration));

            lock (syncRoot)
            {
                Context.PrepareFunctionDeclaration(ref declaration);
                Debug.Assert(declaration.HasHandle, "Invalid handle");
                if (newlyCreated = !functionMapping.TryGetData(declaration.Handle, out FunctionBuilder builder))
                {
                    Debug.Assert(declaration.ReturnType != null, "Invalid return type");
                    builder = new FunctionBuilder(
                        this,
                        Context.CreateInstantiated(new TopLevelFunction(
                            Generation, declaration)),
                        declaration.ReturnType);

                    functionMapping.Register(declaration.Handle, builder);
                }
                return builder;
            }
        }

        /// <summary>
        /// Creates a top-level function that corresponds to the given declaration.
        /// </summary>
        /// <param name="declaration">The function declaration.</param>
        /// <returns>A function builder.</returns>
        public FunctionBuilder CreateFunction(in FunctionDeclaration declaration) =>
            CreateFunction(declaration, out bool _);

        /// <summary>
        /// Declares a top-level function.
        /// </summary>
        /// <param name="declaration">The function declaration.</param>
        /// <returns>The declared top-level function.</returns>
        public TopLevelFunction DeclareFunction(in FunctionDeclaration declaration)
        {
            lock (syncRoot)
            {
                if (PreserveTopLevelFunctions &&
                    Context.TryGetFunction(declaration.Handle, out TopLevelFunction functionValue))
                    return functionValue;
                return CreateFunction(declaration).FunctionValue as TopLevelFunction;
            }
        }

        /// <summary>
        /// Creates a parameter with the given index and type information.
        /// </summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="name">The parameter name (for debugging purposes).</param>
        /// <returns>The created parameter.</returns>
        internal Parameter CreateParam(TypeNode type, string name) =>
            Context.CreateInstantiated(new Parameter(
                Generation,
                type,
                name));

        /// <summary>
        /// Tries to create a top-level function builder that corresponds to the managed method.
        /// </summary>
        /// <param name="methodBase">The managed method to create.</param>
        /// <param name="builder">The resolved builder (in case of success).</param>
        /// <returns>The referenced top-level function.</returns>
        public TopLevelFunction TryCreateFunctionBuilder(
            MethodBase methodBase,
            out FunctionBuilder builder)
        {
            lock (syncRoot)
            {
                if (functionMapping.TryGetHandle(methodBase, out FunctionHandle handle))
                {
                    builder = functionMapping[handle];
                    return builder.FunctionValue as TopLevelFunction;
                }
                if (PreserveTopLevelFunctions &&
                    Context.TryGetFunction(methodBase, out TopLevelFunction functionValue))
                {
                    builder = null;
                    return functionValue;
                }

                var returnType = CreateType(methodBase.GetReturnType());
                builder = CreateFunction(
                    new FunctionDeclaration(
                        FunctionHandle.Empty,
                        returnType,
                        methodBase));
                return builder.FunctionValue as TopLevelFunction;
            }
        }

        /// <summary>
        /// Declares a top-level function based on a managed method.
        /// </summary>
        /// <param name="methodBase">The managed method to declare.</param>
        /// <returns>The declared top-level function.</returns>
        public TopLevelFunction DeclareFunction(MethodBase methodBase) =>
            TryCreateFunctionBuilder(methodBase, out FunctionBuilder _);
    }
}
