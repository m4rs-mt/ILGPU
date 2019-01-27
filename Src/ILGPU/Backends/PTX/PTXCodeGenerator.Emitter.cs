// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.Emitter.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
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
            public void AppendArgument(PrimitiveRegister argument)
            {
                AppendArgument();
                stringBuilder.Append('%');
                stringBuilder.Append(GetStringRepresentation(argument));
            }

            /// <summary>
            /// Append the value given register argument.
            /// </summary>
            /// <param name="argument">The register argument.</param>
            public void AppendArgumentValue(PrimitiveRegister argument)
            {
                AppendArgument();
                stringBuilder.Append('[');
                stringBuilder.Append('%');
                stringBuilder.Append(GetStringRepresentation(argument));
                stringBuilder.Append(']');
            }

            /// <summary>
            /// Append the value given register argument.
            /// </summary>
            /// <param name="argument">The register argument.</param>
            /// <param name="offset">The offset in bytes.</param>
            public void AppendArgumentValue(PrimitiveRegister argument, int offset)
            {
                AppendArgument();
                stringBuilder.Append('[');
                stringBuilder.Append('%');
                stringBuilder.Append(GetStringRepresentation(argument));
                AppendOffset(offset);
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
                if (offset < 1)
                    return;

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
                PrimitiveRegister predicateRegister,
                bool isTrue)
            {
                Debug.Assert(
                    predicateRegister.Kind == PTXRegisterKind.Predicate,
                    "Invalid predicate register");
                PredicateRegister = predicateRegister;
                IsTrue = isTrue;
            }

            /// <summary>
            /// The predicate register.
            /// </summary>
            public PrimitiveRegister PredicateRegister { get; }

            /// <summary>
            /// Branch if the predicate register is true.
            /// </summary>
            public bool IsTrue { get; }
        }

        /// <summary>
        /// Represents a scoped predicate-register allocation.
        /// </summary>
        protected readonly struct PredicateScope : IDisposable
        {
            public PredicateScope(PTXRegisterAllocator registerAllocator)
            {
                Debug.Assert(registerAllocator != null, "Invalid register allocator");

                RegisterAllocator = registerAllocator;
                PredicateRegister = registerAllocator.AllocateRegister(PTXRegisterKind.Predicate);
            }

            public PredicateScope(PrimitiveRegister predicateRegister)
            {
                Debug.Assert(predicateRegister != null, "Invalid register allocator");
                Debug.Assert(
                    predicateRegister.Kind == PTXRegisterKind.Predicate,
                    "Invalid predicate register");
                RegisterAllocator = null;
                PredicateRegister = predicateRegister;
            }

            /// <summary>
            /// The associated register allocator.
            /// </summary>
            public PTXRegisterAllocator RegisterAllocator { get; }

            /// <summary>
            /// The allocated predicate register.
            /// </summary>
            public PrimitiveRegister PredicateRegister { get; }

            /// <summary>
            /// Resolves a new predicate configuration.
            /// </summary>
            public PredicateConfiguration GetConfiguration(bool isTrue) =>
                new PredicateConfiguration(PredicateRegister, isTrue);

            /// <summary>
            /// Converts the underlying predicate register to a
            /// default target register.
            /// </summary>
            /// <param name="codeGenerator">The target code generator.</param>
            /// <param name="targetRegister">The target register to write to.</param>
            public void ConvertToValue(
                PTXCodeGenerator codeGenerator,
                PrimitiveRegister targetRegister)
            {
                using (var command = codeGenerator.BeginCommand(
                    Instructions.GetSelectValueOperation(BasicValueType.Int32)))
                {
                    command.AppendArgument(targetRegister);
                    command.AppendConstant(1);
                    command.AppendConstant(0);
                    command.AppendArgument(PredicateRegister);
                }
            }

            /// <summary>
            /// Frees the allocated predicate register.
            /// </summary>
            public void Dispose()
            {
                // Release the predicate register
                RegisterAllocator?.FreeRegister(PredicateRegister);
            }
        }

        /// <summary>
        /// Enapsulates a complex command emission process.
        /// </summary>
        protected interface IComplexCommandEmitter
        {
            /// <summary>
            /// Emits a nested primitive command in the scope of a complex command chain.
            /// </summary>
            /// <param name="commandEmitter">The command emitter.</param>
            /// <param name="registers">All involved primitive registers.</param>
            void Emit(CommandEmitter commandEmitter, PrimitiveRegister[] registers);
        }

        /// <summary>
        /// Enapsulates a complex command emission process.
        /// </summary>
        protected interface IComplexCommandEmitterWithOffsets
        {
            /// <summary>
            /// Emits a nested primitive command in the scope of a complex command chain.
            /// </summary>
            /// <param name="commandEmitter">The command emitter.</param>
            /// <param name="register">The involved primitive registers.</param>
            /// <param name="offset">The offset in bytes.</param>
            void Emit(
                CommandEmitter commandEmitter,
                PrimitiveRegister register,
                int offset);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Emits a complex command that might depend on non-primitive registers.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="command">The generic command to emit.</param>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="registers">All involved registers.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EmitComplexCommand<TEmitter>(
            string command,
            in TEmitter emitter,
            params Register[] registers)
            where TEmitter : IComplexCommandEmitter
        {
            // Iterate over all child registers and create commands
            switch (registers[0])
            {
                case PrimitiveRegister _:
                    // Invoke final emitter
                    var primitiveRegisters = new PrimitiveRegister[registers.Length];
                    for (int i = 0, e = registers.Length; i < e; ++i)
                        primitiveRegisters[i] = registers[i] as PrimitiveRegister;
                    using (var commandEmitter = BeginCommand(command))
                        emitter.Emit(commandEmitter, primitiveRegisters);
                    break;
                case ViewImplementationRegister _:
                    var lengthRegisters = new PrimitiveRegister[registers.Length];
                    var pointerRegisters = new PrimitiveRegister[registers.Length];
                    for (int i = 0, e = registers.Length; i < e; ++i)
                    {
                        var currentViewRegister = registers[i] as ViewImplementationRegister;
                        lengthRegisters[i] = currentViewRegister.Length;
                        pointerRegisters[i] = currentViewRegister.Pointer;
                    }
                    using (var commandEmitter = BeginCommand(command))
                        emitter.Emit(commandEmitter, pointerRegisters);
                    using (var commandEmitter = BeginCommand(command))
                        emitter.Emit(commandEmitter, lengthRegisters);
                    break;
                case CompoundRegister compoundRegister:
                    var elementRegisters = new Register[registers.Length];
                    for (int i = 0, e = compoundRegister.NumChildren; i < e; ++i)
                    {
                        for (int j = 0, e2 = registers.Length; j < e2; ++j)
                            elementRegisters[j] = (registers[j] as CompoundRegister).Children[i];
                        EmitComplexCommand(command, emitter, elementRegisters);
                    }
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// A specialized version of <see cref="EmitComplexCommand{TEmitter}(string, in TEmitter, RegisterAllocator{PTXRegisterKind}.Register[])"/>.
        /// This version uses a single register and uses internal ABI-specific offset computations
        /// to resolve the correct offset in bytes within a structure.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="command">The generic command to emit.</param>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="register">The involved register.</param>
        /// <param name="offset">The current offset in bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EmitComplexCommandWithOffsets<TEmitter>(
            string command,
            in TEmitter emitter,
            Register register,
            int offset = 0)
            where TEmitter : IComplexCommandEmitterWithOffsets
        {
            switch (register)
            {
                case PrimitiveRegister primitiveRegister:
                    using (var commandEmitter = BeginCommand(command))
                        emitter.Emit(commandEmitter, primitiveRegister, offset);
                    break;
                case ViewImplementationRegister viewRegister:
                    // We have to emit two load operations
                    EmitComplexCommandWithOffsets(
                        command,
                        emitter,
                        viewRegister.Pointer,
                        offset + 0);
                    EmitComplexCommandWithOffsets(
                        command,
                        emitter,
                        viewRegister.Length,
                        offset + ABI.PointerSize);
                    break;
                case CompoundRegister compoundRegister:
                    var structType = compoundRegister.Type as StructureType;
                    var offsets = ABI.GetOffsetsOf(structType);
                    for (int i = 0, e = structType.NumChildren; i < e; ++i)
                    {
                        var fieldOffset = offsets[i];
                        EmitComplexCommandWithOffsets(
                            command,
                            emitter,
                            compoundRegister.Children[i],
                            offset + fieldOffset);
                    }
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Converts the given register to a predicate register scope.
        /// </summary>
        /// <param name="register">The register to convert.</param>
        /// <returns>The created predicate scope.</returns>
        protected PredicateScope ConvertToPredicateScope(PrimitiveRegister register)
        {
            if (register.Kind == PTXRegisterKind.Predicate)
                return new PredicateScope(register);

            Debug.Assert(
                register.Kind == PTXRegisterKind.Int32,
                "Invalid register kind");
            var scope = new PredicateScope(this);

            // Convert to predicate value
            using (var command = BeginCommand(
                Instructions.GetCompareOperation(
                    CompareKind.NotEqual,
                    ArithmeticBasicValueType.UInt32)))
            {
                command.AppendArgument(scope.PredicateRegister);
                command.AppendArgument(register);
                command.AppendConstant(0);
            }
            return scope;
        }

        /// <summary>
        /// Emits a simple move command.
        /// </summary>
        /// <param name="source">The source register.</param>
        /// <param name="target">The target register.</param>
        /// <param name="predicate">The predicate under which to execute the command.</param>
        protected void Move(
            PrimitiveRegister source,
            PrimitiveRegister target,
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
                Builder.Append(GetStringRepresentation(predicateValue.PredicateRegister));
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
