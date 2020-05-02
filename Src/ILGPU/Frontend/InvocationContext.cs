// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: InvocationContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace ILGPU.Frontend
{
    /// <summary>
    /// Represents an invocation context for compiler-known methods
    /// that are supported in the scope of ILGPU programs.
    /// </summary>
    public readonly struct InvocationContext : ILocation
    {
        #region Instance

        /// <summary>
        /// Constructs a new invocation context.
        /// </summary>
        /// <param name="codeGenerator">The associated code generator.</param>
        /// <param name="location">The current location.</param>
        /// <param name="block">The current block.</param>
        /// <param name="callerMethod">The caller.</param>
        /// <param name="method">The called method.</param>
        /// <param name="arguments">The method arguments.</param>
        internal InvocationContext(
            CodeGenerator codeGenerator,
            Location location,
            Block block,
            MethodBase callerMethod,
            MethodBase method,
            ImmutableArray<ValueReference> arguments)
        {
            CodeGenerator = codeGenerator;
            Location = location;
            Block = block;
            CallerMethod = callerMethod;
            Method = method;
            Arguments = arguments;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated code generator.
        /// </summary>
        internal CodeGenerator CodeGenerator { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// Return the current basic block.
        /// </summary>
        internal Block Block { get; }

        /// <summary>
        /// Returns the current IR context.
        /// </summary>
        public IRContext Context => CodeGenerator.Context;

        /// <summary>
        /// Returns the current IR builder.
        /// </summary>
        public IRBuilder Builder => Block.Builder;

        /// <summary>
        /// Represents the caller method.
        /// </summary>
        public MethodBase CallerMethod { get; }

        /// <summary>
        /// Represents the targeted method.
        /// </summary>
        public MethodBase Method { get; }

        /// <summary>
        /// Returns the associated module.
        /// </summary>
        public Module Module => Method.Module;

        /// <summary>
        /// Returns the call arguments.
        /// </summary>
        public ImmutableArray<ValueReference> Arguments { get; }

        /// <summary>
        /// Returns the number of arguments.
        /// </summary>
        public int NumArguments => Arguments.Length;

        /// <summary>
        /// Returns the argument with the given index.
        /// </summary>
        /// <param name="index">The argument index.</param>
        /// <returns>The argument with the given index.</returns>
        public ValueReference this[int index] => Arguments[index];

        #endregion

        #region Methods

        /// <summary>
        /// Formats an error message to include specific exception information.
        /// </summary>
        public string FormatErrorMessage(string message) =>
            Location.FormatErrorMessage(message);

        /// <summary>
        /// Returns the generic arguments of the used method.
        /// </summary>
        /// <returns>The generic arguments of the used method.</returns>
        public Type[] GetMethodGenericArguments() => Method.GetGenericArguments();

        /// <summary>
        /// Returns the generic arguments of the used method.
        /// </summary>
        /// <returns>The generic arguments of the used method.</returns>
        public Type[] GetTypeGenericArguments() =>
            Method.DeclaringType.GetGenericArguments();

        /// <summary>
        /// Declares a new top-level function.
        /// </summary>
        /// <param name="methodBase">The method to declare.</param>
        /// <returns>The declared top-level function.</returns>
        public Method DeclareFunction(MethodBase methodBase)
        {
            if (methodBase == null)
                throw Location.GetArgumentNullException(nameof(methodBase));
            return CodeGenerator.DeclareMethod(methodBase);
        }

        /// <summary>
        /// Remaps the current target to the given one.
        /// </summary>
        /// <param name="targetMethod">The new target method.</param>
        /// <param name="arguments">The target arguments.</param>
        /// <returns>The remapped context.</returns>
        public InvocationContext Remap(
            MethodBase targetMethod,
            ImmutableArray<ValueReference> arguments)
        {
            if (targetMethod == null)
                throw Location.GetArgumentNullException(nameof(targetMethod));
            return new InvocationContext(
                CodeGenerator,
                Location,
                Block,
                CallerMethod,
                targetMethod,
                arguments);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this invocation context.
        /// </summary>
        /// <returns>The string representation of this invocation context.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Method.Name);
            builder.Append('(');
            if (Arguments.Length > 0)
            {
                for (int i = 0, e = Arguments.Length; i < e; ++i)
                {
                    builder.Append(Arguments[i].ToString());
                    if (i + 1 < e)
                        builder.Append(", ");
                }
            }
            builder.Append(')');
            return builder.ToString();
        }

        #endregion
    }
}
