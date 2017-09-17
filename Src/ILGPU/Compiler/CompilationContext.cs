// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CompilationContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace ILGPU
{
    /// <summary>
    /// Represents a single compilation context. It stores information about
    /// all methods that are currently being processed.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    sealed class CompilationContext
    {
        #region Instance

        private readonly HashSet<MethodBase> calledMethods = new HashSet<MethodBase>();
        private readonly Stack<MethodBase> callStack = new Stack<MethodBase>();

        /// <summary>
        /// Constructs a new compilation context.
        /// </summary>
        public CompilationContext()
        {
            NotSupportedILInstructionHandler = (sender, opCode) =>
            {
                switch (opCode)
                {
                    case ILOpCode.Ldftn:
                        throw GetNotSupportedException(
                            ErrorMessages.NotSupportedILInstructionPossibleLambda,
                            opCode.ToString());
                    default:
                        throw GetNotSupportedException(
                            ErrorMessages.NotSupportedILInstruction,
                            opCode.ToString());
                }
            };
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns an event handler that can the handle <see cref="DisassemblerContext.NotSupportedILInstruction"/>
        /// event of a <see cref="DisassemblerContext"/>.
        /// </summary>
        public EventHandler<ILOpCode> NotSupportedILInstructionHandler { get; }

        /// <summary>
        /// Returns the current method that is being processed.
        /// </summary>
        public MethodBase CurrentMethod => callStack.Count > 0 ? callStack.Peek() : null;

        #endregion

        #region Methods

        /// <summary>
        /// Enters the given method.
        /// </summary>
        /// <param name="method">The method to enter.</param>
        public void EnterMethod(MethodBase method)
        {
            Debug.Assert(method != null, "Invalid method");
            if (calledMethods.Contains(method))
                throw GetNotSupportedException(ErrorMessages.NotSupportedRecursiveProgram);
            calledMethods.Add(method);
            callStack.Push(method);
        }

        /// <summary>
        /// Leaves the given method.
        /// </summary>
        /// <param name="method">The method to leave.</param>
        public void LeaveMethod(MethodBase method)
        {
            Debug.Assert(method != null, "Invalid method");
            Debug.Assert(calledMethods.Contains(method), "Method not registered");
            Debug.Assert(callStack.Peek() == method, "Method not on top of stack");
            callStack.Pop();
            calledMethods.Remove(method);
        }

        /// <summary>
        /// Computes a new string containing call-stack information.
        /// </summary>
        /// <param name="target">The target builder.</param>
        /// <param name="prefix">The prefix of each call-stack entry.</param>
        private void ComputeCallStackString(StringBuilder target, string prefix)
        {
            Debug.Assert(target != null, "Invalid target");
            foreach (var entry in callStack)
            {
                target.Append(prefix);
                target.Append(entry.DeclaringType.Name);
                target.Append('.');
                target.AppendLine(entry.Name);
            }
        }

        /// <summary>
        /// Constructs a new <see cref="ArgumentException"/> based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new <see cref="ArgumentException"/>.</returns>
        public ArgumentException GetArgumentException(
            string message,
            params object[] args)
        {
            return GetException<ArgumentException>(message, args);
        }

        /// <summary>
        /// Constructs a new <see cref="NotSupportedException"/> based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new <see cref="NotSupportedException"/>.</returns>
        public NotSupportedException GetNotSupportedException(
            string message,
            params object[] args)
        {
            return GetException<NotSupportedException>(message, args);
        }

        /// <summary>
        /// Constructs a new <see cref="InvalidOperationException"/> based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new <see cref="InvalidOperationException"/>.</returns>
        public InvalidOperationException GetInvalidOperationException(
            string message,
            params object[] args)
        {
            return GetException<InvalidOperationException>(message, args);
        }

        /// <summary>
        /// Constructs a new <see cref="InvalidOperationException"/> that refers to an
        /// invalid IL code.
        /// </summary>
        /// <returns>A new <see cref="InvalidOperationException"/>.</returns>
        public InvalidOperationException GetInvalidILCodeException()
        {
            return GetException<InvalidOperationException>(ErrorMessages.InvalidILCode);
        }

        /// <summary>
        /// Constructs a new exception of the given type based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <typeparam name="TException">The exception type.</typeparam>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new exception of type <typeparamref name="TException"/>.</returns>
        public TException GetException<TException>(
            string message,
            params object[] args)
            where TException : Exception
        {
            var builder = new StringBuilder();
            builder.AppendFormat(message, args);
            var currentMethod = CurrentMethod;
            if (currentMethod != null)
            {
                builder.AppendLine();
                builder.Append("Current method: ");
                builder.Append(CurrentMethod.DeclaringType.Name);
                builder.Append('.');
                builder.AppendLine(CurrentMethod.Name);
                builder.AppendLine("Method callstack:");
                ComputeCallStackString(builder, "\t");
            }
            var instance = Activator.CreateInstance(
                typeof(TException),
                builder.ToString()) as TException;
            return instance;
        }

        #endregion
    }
}
