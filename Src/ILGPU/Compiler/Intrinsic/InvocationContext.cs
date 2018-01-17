// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: InvocationContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using System;
using System.Reflection;
using System.Text;

namespace ILGPU.Compiler.Intrinsic
{
    /// <summary>
    /// Represents an invocation context for compiler-known methods
    /// that are supported in the scope of ILGPU programs.
    /// </summary>
    public struct InvocationContext : IEquatable<InvocationContext>
    {
        #region Instance

        private Value[] methodArguments;

        /// <summary>
        /// Constructs a new invocation context.
        /// </summary>
        /// <param name="builder">The current instruction builder.</param>
        /// <param name="callerMethod">The caller.</param>
        /// <param name="method">The called method.</param>
        /// <param name="args">The method arguments.</param>
        /// <param name="codeGenerator">The associated code generator.</param>
        internal InvocationContext(
            LLVMBuilderRef builder,
            Method callerMethod,
            MethodBase method,
            Value[] args,
            CodeGenerator codeGenerator)
        {
            Builder = builder;
            CallerMethod = callerMethod;
            Method = method;
            methodArguments = args;
            CodeGenerator = codeGenerator;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current compile unit
        /// </summary>
        public CompileUnit Unit => CallerMethod.CompileUnit;

        /// <summary>
        /// Returns the current context.
        /// </summary>
        public Context Context => Unit.Context;

        /// <summary>
        /// Returns the LLVM context.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMContextRef LLVMContext => Context.LLVMContext;

        /// <summary>
        /// Returns the current instruction builder.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMBuilderRef Builder { get; }

        /// <summary>
        /// Represents the caller method.
        /// </summary>
        public Method CallerMethod { get; }

        /// <summary>
        /// Represents the targeted method.
        /// </summary>
        public MethodBase Method { get; }

        /// <summary>
        /// Returns the associated module.
        /// </summary>
        public Module Module => Method.Module;

        /// <summary>
        /// Returns the associated code generator.
        /// </summary>
        internal CodeGenerator CodeGenerator { get; }

        /// <summary>
        /// Returns the current compilation context.
        /// </summary>
        internal CompilationContext CompilationContext => Unit.CompilationContext;

        #endregion

        #region Methods

        /// <summary>
        /// Represents the arguments of the method invocation.
        /// </summary>
        public Value[] GetArgs()
        {
            return methodArguments;
        }

        /// <summary>
        /// Resolves the native LLVM arguments and returns the in form
        /// of an argument array.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMValueRef[] GetLLVMArgs()
        {
            var result = new LLVMValueRef[methodArguments.Length];
            for (int i = 0, e = result.Length; i < e; ++i)
                result[i] = methodArguments[i].LLVMValue;
            return result;
        }

        /// <summary>
        /// Returns the generic arguments of the used method.
        /// </summary>
        /// <returns>The generic arguments of the used method.</returns>
        public Type[] GetMethodGenericArguments()
        {
            return Method.GetGenericArguments();
        }

        /// <summary>
        /// Returns the generic arguments of the used method.
        /// </summary>
        /// <returns>The generic arguments of the used method.</returns>
        public Type[] GetTypeGenericArguments()
        {
            return Method.DeclaringType.GetGenericArguments();
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given invocation context is equal to the current invocation context.
        /// </summary>
        /// <param name="other">The other invocation context.</param>
        /// <returns>True, iff the given invocation context is equal to the current invocation context.</returns>
        public bool Equals(InvocationContext other)
        {
            return this == other;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current invocation context.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current invocation context.</returns>
        public override bool Equals(object obj)
        {
            if (obj is InvocationContext)
                return Equals((InvocationContext)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this invocation context.
        /// </summary>
        /// <returns>The hash code of this invocation context.</returns>
        public override int GetHashCode()
        {
            return Builder.GetHashCode() ^ Method.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this invocation context.
        /// </summary>
        /// <returns>The string representation of this invocation context.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Method.Name);
            builder.Append('(');
            if (methodArguments != null)
            {
                for (int i = 0, e = methodArguments.Length; i < e; ++i)
                {
                    builder.Append(methodArguments[i].ToString());
                    if (i + 1 < e)
                        builder.Append(", ");
                }
            }
            builder.Append(')');
            return builder.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second invocation contexts are the same.
        /// </summary>
        /// <param name="first">The first invocation context.</param>
        /// <param name="second">The second invocation context.</param>
        /// <returns>True, iff the first and second invocation contexts are the same.</returns>
        public static bool operator ==(InvocationContext first, InvocationContext second)
        {
            if (first.Method != second.Method)
                return false;
            var firstLength = first.methodArguments?.Length;
            var secondLength = second.methodArguments?.Length;
            if (firstLength != secondLength)
                return false;
            for (int i = 0; i < firstLength; ++i)
            {
                if (first.methodArguments[i] != second.methodArguments[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true iff the first and second invocation contexts are not the same.
        /// </summary>
        /// <param name="first">The first invocation context.</param>
        /// <param name="second">The second invocation context.</param>
        /// <returns>True, iff the first and second invocation contexts are not the same.</returns>
        public static bool operator !=(InvocationContext first, InvocationContext second)
        {
            if (first.Method != second.Method)
                return true;
            var firstLength = first.methodArguments?.Length;
            var secondLength = second.methodArguments?.Length;
            if (firstLength != secondLength)
                return true;
            for (int i = 0; i < firstLength; ++i)
            {
                if (first.methodArguments[i] != second.methodArguments[i])
                    return true;
            }
            return false;
        }

        #endregion
    }
}
