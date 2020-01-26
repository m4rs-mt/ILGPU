// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.Emitter.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
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
        public struct CommandEmitter : IDisposable
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
            /// Appends a specialized basic type suffix for mov instructions.
            /// </summary>
            /// <param name="basicValueType">The type suffix.</param>
            public void AppendRegisterMovementSuffix(BasicValueType basicValueType) =>
                AppendSuffix(ResolveRegisterMovementType(basicValueType));

            /// <summary>
            /// Appends the given command basic value type suffix.
            /// </summary>
            /// <param name="basicValueType">The type suffix.</param>
            public void AppendSuffix(BasicValueType basicValueType) =>
                AppendSuffix(GetBasicSuffix(basicValueType));

            /// <summary>
            /// Appends the given command postfix.
            /// </summary>
            /// <param name="suffix">The postfix.</param>
            public void AppendSuffix(string suffix)
            {
                stringBuilder.Append('.');
                stringBuilder.Append(suffix);
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
            /// Appends the constant value 'null' of the given type.
            /// </summary>
            /// <param name="kind">The register kind.</param>
            public void AppendNull(PTXRegisterKind kind)
            {
                switch (kind)
                {
                    case PTXRegisterKind.Float32:
                        AppendConstant(0.0f);
                        break;
                    case PTXRegisterKind.Float64:
                        AppendConstant(0.0);
                        break;
                    default:
                        AppendConstant(0);
                        break;
                }
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
            [CLSCompliant(false)]
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
                    intRef <<= 8;
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
                    longRef <<= 8;
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
        public readonly struct PredicateConfiguration
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
        public readonly struct PredicateScope : IDisposable
        {
            /// <summary>
            /// Constructs a new predicate scope.
            /// </summary>
            /// <param name="registerAllocator">The parent register allocator.</param>
            internal PredicateScope(PTXRegisterAllocator registerAllocator)
            {
                Debug.Assert(registerAllocator != null, "Invalid register allocator");

                RegisterAllocator = registerAllocator;
                PredicateRegister = registerAllocator.AllocateRegister(
                    new RegisterDescription(
                        BasicValueType.Int1,
                        PTXRegisterKind.Predicate));
            }

            /// <summary>
            /// Constructs a new predicate register.
            /// </summary>
            /// <param name="predicateRegister">The underlying predicate register.</param>
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
                PrimitiveRegister targetRegister) =>
                codeGenerator.ConvertPredicateToValue(
                    PredicateRegister,
                    targetRegister);

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
        public interface IComplexCommandEmitter
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
        public interface IComplexCommandEmitterWithOffsets
        {
            /// <summary>
            /// Emits a nested primitive command in the scope of a complex command chain.
            /// </summary>
            /// <param name="codeGenerator">The code generator.</param>
            /// <param name="command">The current command to emit.</param>
            /// <param name="primitiveRegister">The involved primitive register.</param>
            /// <param name="offset">The offset in bytes.</param>
            void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister primitiveRegister,
                int offset);
        }

        /// <summary>
        /// Emits a sequence of IO instructions.
        /// </summary>
        /// <typeparam name="T">The user state type.</typeparam>
        public interface IIOEmitter<T>
            where T : struct
        {
            /// <summary>
            /// Emits a new sequence of primitive IO instructions.
            /// </summary>
            /// <param name="codeGenerator">The code generator.</param>
            /// <param name="command">The current command to emit.</param>
            /// <param name="primitiveRegister">The involved primitive register.</param>
            /// <param name="userState">The current user state.</param>
            void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister primitiveRegister,
                T userState);
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
        public void EmitComplexCommand<TEmitter>(
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
        public void EmitComplexCommandWithOffsets<TEmitter>(
            string command,
            in TEmitter emitter,
            Register register,
            int offset = 0)
            where TEmitter : IComplexCommandEmitterWithOffsets
        {
            switch (register)
            {
                case PrimitiveRegister primitiveRegister:
                    emitter.Emit(this, command, primitiveRegister, offset);
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
                case StructureRegister structureRegister:
                    var structType = structureRegister.StructureType;
                    var offsets = ABI.GetOffsetsOf(structType);
                    for (int i = 0, e = structType.NumFields; i < e; ++i)
                    {
                        var fieldOffset = offsets[i];
                        EmitComplexCommandWithOffsets(
                            command,
                            emitter,
                            structureRegister.Children[i],
                            offset + fieldOffset);
                    }
                    break;
                case ArrayRegister arrayRegister:
                    var arrayType = arrayRegister.ArrayType;
                    var arrayElementSize = ABI.GetSizeOf(arrayType.ElementType);
                    for (int i = 0, e = arrayType.Length; i < e; ++i)
                    {
                        int fieldOffset = i * arrayElementSize;
                        EmitComplexCommandWithOffsets(
                            command,
                            emitter,
                            arrayRegister.Children[i],
                            fieldOffset);
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
        public PredicateScope ConvertToPredicateScope(PrimitiveRegister register)
        {
            if (register.Kind == PTXRegisterKind.Predicate)
                return new PredicateScope(register);

            Debug.Assert(
                register.Kind == PTXRegisterKind.Int32,
                "Invalid register kind");
            var scope = new PredicateScope(this);
            ConvertValueToPredicate(register, scope.PredicateRegister);
            return scope;
        }

        /// <summary>
        /// Converts the given predicate register to a default integer register.
        /// </summary>
        /// <param name="register">The source register.</param>
        /// <param name="targetRegister">The target register to write to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConvertPredicateToValue(
            PrimitiveRegister register,
            PrimitiveRegister targetRegister)
        {
            Debug.Assert(
                register.Kind == PTXRegisterKind.Predicate,
                "Invalid predicate register");
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Int32,
                "Invalid target register");
            using (var command = BeginCommand(
                PTXInstructions.GetSelectValueOperation(BasicValueType.Int32)))
            {
                command.AppendArgument(targetRegister);
                command.AppendConstant(1);
                command.AppendConstant(0);
                command.AppendArgument(register);
            }
        }

        /// <summary>
        /// Converts the given register to a predicate register scope.
        /// </summary>
        /// <param name="register">The register to convert.</param>
        /// <returns>The created predicate scope.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PrimitiveRegister ConvertValueToPredicate(PrimitiveRegister register)
        {
            if (register.Kind == PTXRegisterKind.Predicate)
                return register;

            Debug.Assert(
                register.Kind == PTXRegisterKind.Int32,
                "Invalid register kind");

            var targetRegister = AllocateRegister(
                new RegisterDescription(
                    BasicValueType.Int1,
                    PTXRegisterKind.Predicate));
            ConvertValueToPredicate(register, targetRegister);
            return targetRegister;
        }

        /// <summary>
        /// Converts the given register to a predicate value in the target register.
        /// </summary>
        /// <param name="register">The register to convert.</param>
        /// <param name="targetRegister">The target register.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConvertValueToPredicate(
            PrimitiveRegister register,
            PrimitiveRegister targetRegister)
        {
            Debug.Assert(
                register.Kind == PTXRegisterKind.Int32,
                "Invalid register kind");
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Predicate,
                "Invalid register kind");

            // Convert to predicate value
            using (var command = BeginCommand(
                PTXInstructions.GetCompareOperation(
                    CompareKind.NotEqual,
                    ArithmeticBasicValueType.UInt32)))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(register);
                command.AppendConstant(0);
            }
        }

        /// <summary>
        /// Emits a generic IO load operation.
        /// </summary>
        /// <typeparam name="TIOEmitter">The type of the load emitter.</typeparam>
        /// <typeparam name="T">The user state type.</typeparam>
        /// <param name="emitter">The emitter type.</param>
        /// <param name="command">The command to emit.</param>
        /// <param name="register">The register for emission.</param>
        /// <param name="userState">The user state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EmitIOLoad<TIOEmitter, T>(
            TIOEmitter emitter,
            string command,
            PrimitiveRegister register,
            T userState)
            where TIOEmitter : struct, IIOEmitter<T>
            where T : struct
        {
            PrimitiveRegister originalRegister = null;
            // We need a temporary 32bit register for predicate conversion at this point:
            // 1) load value into temporary register
            // 2) convert loaded value into predicate
            if (register.BasicValueType == BasicValueType.Int1)
            {
                originalRegister = register;
                register = AllocateInt32Register();
            }

            // Emit load
            emitter.Emit(this, command, register, userState);

            // We need a final predicate conversion
            if (originalRegister != null)
            {
                ConvertValueToPredicate(
                    register,
                    originalRegister);
                FreeRegister(register);
            }
        }

        /// <summary>
        /// Emits a generic IO load operation.
        /// </summary>
        /// <typeparam name="TIOEmitter">The type of the load emitter.</typeparam>
        /// <typeparam name="T">The user state type.</typeparam>
        /// <param name="emitter">The emitter type.</param>
        /// <param name="command">The command to emit.</param>
        /// <param name="register">THe register for emission.</param>
        /// <param name="userState">The user state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EmitIOStore<TIOEmitter, T>(
            TIOEmitter emitter,
            string command,
            PrimitiveRegister register,
            T userState)
            where TIOEmitter : struct, IIOEmitter<T>
            where T : struct
        {
            // We need a temporary 32bit register for predicate conversion at this point:
            // 1) convert current predicate into 32bit integer
            // 2) store the converted value from the temporary register
            PrimitiveRegister originalRegister = null;
            if (register.BasicValueType == BasicValueType.Int1)
            {
                originalRegister = register;
                register = AllocateInt32Register();

                // Convert predicate
                ConvertPredicateToValue(
                    originalRegister,
                    register);
            }

            // Emit store
            emitter.Emit(this, command, register, userState);

            // Free temp register
            if (originalRegister != null)
                FreeRegister(register);
        }

        /// <summary>
        /// Begins a new command.
        /// </summary>
        /// <param name="commandString">The command to begin.</param>
        /// <param name="predicate">The predicate under which to execute the command.</param>
        /// <returns>The created command emitter.</returns>
        public CommandEmitter BeginCommand(
            string commandString,
            PredicateConfiguration? predicate = null)
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
            Builder.Append(commandString);
            return new CommandEmitter(Builder);
        }

        /// <summary>
        /// Emits the given commmand.
        /// </summary>
        /// <param name="commandString">The command to emit.</param>
        /// <param name="predicate">The predicate under which to execute the command.</param>
        public void Command(
            string commandString,
            PredicateConfiguration? predicate = null)
        {
            using (BeginCommand(commandString, predicate)) { }
        }

        /// <summary>
        /// Emits a simple move command.
        /// </summary>
        /// <param name="source">The source register.</param>
        /// <param name="target">The target register.</param>
        /// <param name="predicate">The predicate under which to execute the command.</param>
        public void Move(
            PrimitiveRegister source,
            PrimitiveRegister target,
            PredicateConfiguration? predicate = null)
        {
            if (source.Kind == target.Kind &&
                source.RegisterValue == target.RegisterValue)
                return;

            using (var emitter = BeginMove(predicate))
            {
                emitter.AppendSuffix(target.BasicValueType);
                emitter.AppendArgument(target);
                emitter.AppendArgument(source);
            }
        }

        /// <summary>
        /// Begins a new move command.
        /// </summary>
        /// <param name="predicate">The predicate under which to execute the command.</param>
        /// <returns>The created command emitter.</returns>
        public CommandEmitter BeginMove(
            PredicateConfiguration? predicate = null) =>
            BeginCommand(
                PTXInstructions.MoveOperation,
                predicate);

        /// <summary>
        /// Moves the value of the specified intrinsic register to the target register.
        /// </summary>
        /// <param name="targetRegister">The target register.</param>
        /// <param name="registerKind">The intrinsic register kind.</param>
        /// <param name="dimension">The register dimension (if any).</param>
        public void MoveFromIntrinsicRegister(
            PrimitiveRegister targetRegister,
            PTXRegisterKind registerKind,
            int dimension = 0)
        {
            var intrinsicDescription = new RegisterDescription(
                BasicValueType.Int32,
                registerKind);
            Move(
                new PrimitiveRegister(intrinsicDescription, dimension),
                targetRegister);
        }

        /// <summary>
        /// Allocates a new target register and moves the value of the
        /// specified intrinsic register to the target register.
        /// </summary>
        /// <param name="registerKind">The intrinsic register kind.</param>
        /// <param name="dimension">The register dimension (if any).</param>
        public PrimitiveRegister MoveFromIntrinsicRegister(
            PTXRegisterKind registerKind,
            int dimension = 0)
        {
            var description = ResolveRegisterDescription(BasicValueType.Int32);
            var target = AllocateRegister(description);
            MoveFromIntrinsicRegister(target, registerKind, dimension);
            return target;
        }

        /// <summary>
        /// Allocates a new target register for the given value and
        /// moves the value of the specified intrinsic register to the target register.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="registerKind">The intrinsic register kind.</param>
        /// <param name="dimension">The register dimension (if any).</param>
        public PrimitiveRegister MoveFromIntrinsicRegister(
            Value value,
            PTXRegisterKind registerKind,
            int dimension = 0)
        {
            var register = MoveFromIntrinsicRegister(registerKind, dimension);
            Bind(value, register);
            return register;
        }

        #endregion
    }
}
