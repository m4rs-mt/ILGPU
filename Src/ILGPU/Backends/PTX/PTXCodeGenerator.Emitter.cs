// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.Emitter.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Backends.PTX
{
    partial class PTXCodeGenerator
    {
        #region Nested Types

        /// <summary>
        /// Represents a general PTX command emitter.
        /// </summary>
        protected struct CommandEmitter : IDisposable
        {
            #region Instance

            private readonly StringBuilder stringBuilder;
            private bool argMode;
            private int argumentCount;

            /// <summary>
            /// Constructs a new command emitter using the given target.
            /// </summary>
            /// <param name="target">The target builder.</param>
            public CommandEmitter(StringBuilder target)
            {
                stringBuilder = target;
                argumentCount = 0;
                argMode = false;
            }

            #endregion

            #region Methods

            /// <summary>
            /// Appends the given non-local address space.
            /// </summary>
            /// <param name="addressSpace">The address space.</param>
            public void AppendNonLocalAddressSpace(MemoryAddressSpace addressSpace)
            {
                if (addressSpace == MemoryAddressSpace.Local)
                    return;
                AppendAddressSpace(addressSpace);
            }

            /// <summary>
            /// Appends the given address space
            /// </summary>
            /// <param name="addressSpace">The address space.</param>
            public void AppendAddressSpace(MemoryAddressSpace addressSpace)
            {
                switch (addressSpace)
                {
                    case MemoryAddressSpace.Global:
                        stringBuilder.Append(".global");
                        break;
                    case MemoryAddressSpace.Shared:
                        stringBuilder.Append(".shared");
                        break;
                    case MemoryAddressSpace.Local:
                        stringBuilder.Append(".local");
                        break;
                    default:
                        break;
                }
            }

            /// <summary>
            /// Appends the given command postfix.
            /// </summary>
            /// <param name="postFix">The postfix.</param>
            public void AppendPostFix(string postFix)
            {
                stringBuilder.Append('.');
                stringBuilder.Append(postFix);
            }

            /// <summary>
            /// Appends code to finish an appended argument.
            /// </summary>
            private void AppendArgument()
            {
                if (!argMode)
                {
                    stringBuilder.Append('\t');
                    argMode = true;
                }
                if (argumentCount > 0)
                    stringBuilder.Append(", ");
                ++argumentCount;
            }

            /// <summary>
            /// Append the given register argument.
            /// </summary>
            /// <param name="argument">The register argument.</param>
            public void AppendArgument(PTXRegister argument)
            {
                AppendArgument();
                stringBuilder.Append('%');
                stringBuilder.Append(argument.ToString());
            }

            /// <summary>
            /// Append the value given register argument.
            /// </summary>
            /// <param name="argument">The register argument.</param>
            public void AppendArgumentValue(PTXRegister argument)
            {
                AppendArgument();
                stringBuilder.Append('[');
                stringBuilder.Append('%');
                stringBuilder.Append(argument.ToString());
                stringBuilder.Append(']');
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(long value)
            {
                AppendArgument();
                stringBuilder.Append(value);
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(ulong value)
            {
                AppendArgument();
                stringBuilder.Append(value);
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(float value)
            {
                var intRef = Unsafe.As<float, uint>(ref value);
                AppendArgument();
                stringBuilder.Append("0f");
                for (int i = 0; i < 4; ++i)
                {
                    var part = (intRef >> 24) & 0xff;
                    stringBuilder.Append(part.ToString("X2"));
                    intRef = intRef << 8;
                }
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(double value)
            {
                var longRef = Unsafe.As<double, ulong>(ref value);
                AppendArgument();
                stringBuilder.Append("0d");
                for (int i = 0; i < 8; ++i)
                {
                    var part = (longRef >> 56) & 0xff;
                    stringBuilder.Append(part.ToString("X2"));
                    longRef = longRef << 8;
                }
            }

            /// <summary>
            /// Appends an offset computation.
            /// </summary>
            /// <param name="offset">The constant offset in bytes.</param>
            public void AppendOffset(int offset)
            {
                Debug.Assert(argMode, "Invalid arg mode");
                stringBuilder.Append('+');
                stringBuilder.Append(offset);
            }

            /// <summary>
            /// Appends a reference to the given label.
            /// </summary>
            /// <param name="label">The label.</param>
            public void AppendLabel(string label)
            {
                AppendArgument();
                stringBuilder.Append(label);
            }

            /// <summary>
            /// Appends the given raw value.
            /// </summary>
            /// <param name="value">The raw value.</param>
            public void AppendRawValue(string value)
            {
                AppendArgument();
                stringBuilder.Append('[');
                stringBuilder.Append(value);
                stringBuilder.Append(']');
            }

            /// <summary>
            /// Appends the given raw value.
            /// </summary>
            /// <param name="value">The raw value.</param>
            /// <param name="offset">The offset in bytes.</param>
            public void AppendRawValue(string value, int offset)
            {
                AppendArgument();
                stringBuilder.Append('[');
                stringBuilder.Append(value);
                if (offset > 0)
                {
                    stringBuilder.Append("+");
                    stringBuilder.Append(offset);
                }
                stringBuilder.Append(']');
            }

            /// <summary>
            /// Appends the given value reference.
            /// </summary>
            /// <param name="valueReference">The value reference.</param>
            public void AppendRawValueReference(string valueReference)
            {
                AppendArgument();
                stringBuilder.Append(valueReference);
            }

            #endregion

            #region IDisposable

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose()
            {
                stringBuilder.Append(';');
                stringBuilder.AppendLine();
            }

            #endregion
        }

        /// <summary>
        /// Represents a predicate-register configuration.
        /// </summary>
        protected readonly struct PredicateConfiguration
        {
            /// <summary>
            /// Constructs a new predicate configuration.
            /// </summary>
            /// <param name="predicateRegister">The predicate register to test.</param>
            /// <param name="isTrue">Branch if the predicate register is true.</param>
            public PredicateConfiguration(
                PTXRegister predicateRegister,
                bool isTrue)
            {
                PredicateRegister = predicateRegister;
                IsTrue = isTrue;
            }

            /// <summary>
            /// The predicate register.
            /// </summary>
            public PTXRegister PredicateRegister { get; }

            /// <summary>
            /// Branch if the predicate register is true.
            /// </summary>
            public bool IsTrue { get; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Emits a simple move command.
        /// </summary>
        /// <param name="source">The source register.</param>
        /// <param name="target">The target register.</param>
        /// <param name="predicate">The predicate under which to execute the command.</param>
        protected void Move(
            PTXRegister source,
            PTXRegister target,
            PredicateConfiguration? predicate = null)
        {
            if (source.Kind == target.Kind &&
                source.RegisterValue == target.RegisterValue)
                return;

            using (var emitter = BeginCommand(
                Instructions.MoveOperation,
                PTXType.GetPTXType(target.Kind),
                predicate))
            {
                emitter.AppendArgument(target);
                emitter.AppendArgument(source);
            }
        }

        /// <summary>
        /// Begins a new command.
        /// </summary>
        /// <param name="command">The command to begin.</param>
        /// <param name="postfix">The command postfix.</param>
        /// <param name="predicate">The predicate under which to execute the command.</param>
        /// <returns>The created command emitter.</returns>
        protected CommandEmitter BeginCommand(
            string command,
            string postfix = null,
            in PredicateConfiguration? predicate = null)
        {
            Builder.Append('\t');
            if (predicate.HasValue)
            {
                Builder.Append('@');
                var predicateValue = predicate.Value;
                if (!predicateValue.IsTrue)
                    Builder.Append('!');
                Builder.Append('%');
                Builder.Append(predicateValue.PredicateRegister.ToString());
                Builder.Append(' ');
            }
            Builder.Append(command);
            var emitter = new CommandEmitter(Builder);
            if (!string.IsNullOrEmpty(postfix))
                emitter.AppendPostFix(postfix);
            return emitter;
        }

        /// <summary>
        /// Begins a new command.
        /// </summary>
        /// <param name="command">The command to begin.</param>
        /// <param name="postfix">The command postfix.</param>
        /// <returns>The created command emitter.</returns>
        protected CommandEmitter BeginCommand(string command, string postfix) =>
            BeginCommand(command, postfix, null);

        /// <summary>
        /// Begins a new command.
        /// </summary>
        /// <param name="command">The command to begin.</param>
        /// <returns>The created command emitter.</returns>
        protected CommandEmitter BeginCommand(string command) =>
            BeginCommand(command, null, null);

        /// <summary>
        /// Emits the given commmand.
        /// </summary>
        /// <param name="command">The command to emit.</param>
        /// <param name="postfix">The command postfix (if any).</param>
        protected void Command(string command, string postfix)
        {
            using (BeginCommand(command, postfix)) { }
        }

        #endregion
    }
}
