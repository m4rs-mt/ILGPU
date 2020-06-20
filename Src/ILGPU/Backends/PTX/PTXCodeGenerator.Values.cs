// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.Values.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.PTX
{
    partial class PTXCodeGenerator
    {
        /// <summary cref="IBackendCodeGenerator.GenerateCode(MethodCall)"/>
        public void GenerateCode(MethodCall methodCall)
        {
            const string ReturnValueName = "callRetVal";
            const string CallParamName = "callParam";

            var target = methodCall.Target;

            // Create call sequence
            Builder.AppendLine();
            Builder.AppendLine("\t{");

            Builder.AppendLine("\t.reg .b32 temp_param_reg;");

            for (int i = 0, e = methodCall.Count; i < e; ++i)
            {
                var argument = methodCall.Nodes[i];
                var paramName = CallParamName + i;
                Builder.Append("\t");
                AppendParamDeclaration(Builder, argument.Type, paramName);
                Builder.AppendLine(";");

                // Emit store param command
                var argumentRegister = Load(argument);
                EmitStoreParam(paramName, argumentRegister);
            }

            // Reserve a sufficient amount of memory
            var returnType = target.ReturnType;
            if (!returnType.IsVoidType)
            {
                Builder.Append("\t");
                AppendParamDeclaration(Builder, returnType, ReturnValueName);
                Builder.AppendLine(";");
                Builder.Append("\tcall ");
                Builder.Append("(");
                Builder.Append(ReturnValueName);
                Builder.Append("), ");
            }
            else
            {
                Builder.Append("\tcall ");
            }
            Builder.Append(GetMethodName(target));
            Builder.AppendLine(", (");
            for (int i = 0, e = methodCall.Count; i < e; ++i)
            {
                Builder.Append("\t\t");
                Builder.Append(CallParamName);
                Builder.Append(i);
                if (i + 1 < e)
                    Builder.AppendLine(",");
                else
                    Builder.AppendLine();
            }
            Builder.AppendLine("\t);");

            if (!returnType.IsVoidType)
            {
                // Allocate target register for the return type and load the data
                var returnRegister = Allocate(methodCall);
                EmitLoadParam(ReturnValueName, returnRegister);
            }
            Builder.AppendLine("\t}");
            Builder.AppendLine();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Parameter)"/>
        public void GenerateCode(Parameter parameter)
        {
            // Parameters are already assigned to registers
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PhiValue)"/>
        public void GenerateCode(PhiValue phiValue)
        {
            // Phi values are already assigned to registers
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(UnaryArithmeticValue)"/>
        public void GenerateCode(UnaryArithmeticValue value)
        {
            var argument = LoadPrimitive(value.Value);
            var targetRegister = AllocateHardware(value);

            using var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    value.Kind,
                    value.ArithmeticBasicValueType,
                    FastMath));
            command.AppendArgument(targetRegister);
            command.AppendArgument(argument);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(BinaryArithmeticValue)"/>
        public void GenerateCode(BinaryArithmeticValue value)
        {
            var left = LoadPrimitive(value.Left);
            var right = LoadPrimitive(value.Right);

            var targetRegister = Allocate(value, left.Description);
            using var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    value.Kind,
                    value.ArithmeticBasicValueType,
                    FastMath));
            command.AppendArgument(targetRegister);
            command.AppendArgument(left);
            command.AppendArgument(right);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(TernaryArithmeticValue)"/>
        public void GenerateCode(TernaryArithmeticValue value)
        {
            var first = LoadPrimitive(value.First);
            var second = LoadPrimitive(value.Second);
            var third = LoadPrimitive(value.Third);


            var targetRegister = Allocate(value, first.Description);
            using var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    value.Kind,
                    value.ArithmeticBasicValueType));
            command.AppendArgument(targetRegister);
            command.AppendArgument(first);
            command.AppendArgument(second);
            command.AppendArgument(third);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(CompareValue)"/>
        public void GenerateCode(CompareValue value)
        {
            var left = LoadPrimitive(value.Left);
            var right = LoadPrimitive(value.Right);

            var targetRegister = AllocateHardware(value);
            if (left.Kind == PTXRegisterKind.Predicate)
            {
                // Predicate registers require a special treatment
                using (var command = BeginCommand(
                    PTXInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Xor,
                        ArithmeticBasicValueType.UInt1,
                        false)))
                {
                    command.AppendArgument(targetRegister);
                    command.AppendArgument(left);
                    command.AppendArgument(right);
                }

                if (value.Kind == CompareKind.Equal)
                {
                    using var command = BeginCommand(
                        PTXInstructions.GetArithmeticOperation(
                            UnaryArithmeticKind.Not,
                            ArithmeticBasicValueType.UInt1,
                            false));
                    command.AppendArgument(targetRegister);
                    command.AppendArgument(targetRegister);
                }
            }
            else
            {
                using var command = BeginCommand(
                    PTXInstructions.GetCompareOperation(
                        value.Kind,
                        value.Flags,
                        value.CompareType));
                command.AppendArgument(targetRegister);
                command.AppendArgument(left);
                command.AppendArgument(right);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(ConvertValue)"/>
        public void GenerateCode(ConvertValue value)
        {
            var sourceValue = LoadPrimitive(value.Value);

            var convertOperation = PTXInstructions.GetConvertOperation(
                value.SourceType,
                value.TargetType);

            var targetRegister = AllocateHardware(value);
            using var command = BeginCommand(convertOperation);
            command.AppendArgument(targetRegister);
            command.AppendArgument(sourceValue);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PointerCast)"/>
        public void GenerateCode(PointerCast value) => Alias(value, value.Value);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(FloatAsIntCast)"/>
        public void GenerateCode(FloatAsIntCast value)
        {
            var source = LoadHardware(value.Value);
            Debug.Assert(
                source.Kind == PTXRegisterKind.Float32 ||
                source.Kind == PTXRegisterKind.Float64);

            var targetRegister = AllocateHardware(value);
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Int32 ||
                targetRegister.Kind == PTXRegisterKind.Int64);

            Move(source, targetRegister);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(IntAsFloatCast)"/>
        public void GenerateCode(IntAsFloatCast value)
        {
            var source = LoadHardware(value.Value);
            Debug.Assert(
                source.Kind == PTXRegisterKind.Int32 ||
                source.Kind == PTXRegisterKind.Int64);

            var targetRegister = AllocateHardware(value);
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Float32 ||
                targetRegister.Kind == PTXRegisterKind.Float64);

            Move(source, targetRegister);
        }

        /// <summary>
        /// Emits complex predicate instructions.
        /// </summary>
        private readonly struct PredicateEmitter : IComplexCommandEmitter
        {
            public PredicateEmitter(PrimitiveRegister predicateRegister)
            {
                PredicateRegister = predicateRegister;
            }

            /// <summary>
            /// The current source type.
            /// </summary>
            public PrimitiveRegister PredicateRegister { get; }

            /// <summary>
            /// Emits nested predicates.
            /// </summary>
            public void Emit(
                CommandEmitter commandEmitter,
                PrimitiveRegister[] registers)
            {
                commandEmitter.AppendArgument(registers[0]);
                commandEmitter.AppendArgument(registers[1]);
                commandEmitter.AppendArgument(registers[2]);
                commandEmitter.AppendArgument(PredicateRegister);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Predicate)"/>
        public void GenerateCode(Predicate predicate)
        {
            var condition = LoadPrimitive(predicate.Condition);
            var trueValue = Load(predicate.TrueValue);
            var falseValue = Load(predicate.FalseValue);

            var targetRegister = Allocate(predicate);
            EmitComplexCommand(
                PTXInstructions.GetSelectValueOperation(predicate.BasicValueType),
                new PredicateEmitter(condition),
                targetRegister,
                trueValue,
                falseValue);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GenericAtomic)"/>
        public void GenerateCode(GenericAtomic atomic)
        {
            var target = LoadHardware(atomic.Target);
            var value = LoadPrimitive(atomic.Value);

            var requiresResult =
                atomic.Uses.HasAny ||
                atomic.Kind == AtomicKind.Exchange;
            var atomicOperation = PTXInstructions.GetAtomicOperation(
                atomic.Kind,
                requiresResult);
            var suffix = PTXInstructions.GetAtomicOperationSuffix(
                atomic.Kind,
                atomic.ArithmeticBasicValueType);

            var targetRegister = requiresResult ? AllocateHardware(atomic) : default;
            using var command = BeginCommand(atomicOperation);
            command.AppendNonLocalAddressSpace(
                (atomic.Target.Type as AddressSpaceType).AddressSpace);
            command.AppendSuffix(suffix);
            if (requiresResult)
                command.AppendArgument(targetRegister);
            command.AppendArgumentValue(target);
            command.AppendArgument(value);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(AtomicCAS)"/>
        public void GenerateCode(AtomicCAS atomicCAS)
        {
            var target = LoadHardware(atomicCAS.Target);
            var value = LoadPrimitive(atomicCAS.Value);
            var compare = LoadPrimitive(atomicCAS.CompareValue);

            var targetRegister = AllocateHardware(atomicCAS);

            using var command = BeginCommand(PTXInstructions.AtomicCASOperation);
            command.AppendNonLocalAddressSpace(
                (atomicCAS.Target.Type as AddressSpaceType).AddressSpace);
            command.AppendSuffix(atomicCAS.BasicValueType);
            command.AppendArgument(targetRegister);
            command.AppendArgumentValue(target);
            command.AppendArgument(value);
            command.AppendArgument(compare);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Alloca)"/>
        public void GenerateCode(Alloca alloca)
        {
            // Ignore alloca
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(MemoryBarrier)"/>
        public void GenerateCode(MemoryBarrier barrier)
        {
            var command = PTXInstructions.GetMemoryBarrier(barrier.Kind);
            Command(command);
        }

        /// <summary>
        /// Emits complex load instructions.
        /// </summary>
        private readonly struct LoadEmitter : IVectorizedCommandEmitter
        {
            private readonly struct IOEmitter : IIOEmitter<int>
            {
                public IOEmitter(
                    PointerType sourceType,
                    HardwareRegister addressRegister)
                {
                    SourceType = sourceType;
                    AddressRegister = addressRegister;
                }

                /// <summary>
                /// The current source type.
                /// </summary>
                public PointerType SourceType { get; }

                /// <summary>
                /// Returns the associated address register.
                /// </summary>
                public HardwareRegister AddressRegister { get; }

                /// <summary>
                /// Emits nested loads.
                /// </summary>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister register,
                    int offset)
                {
                    using var commandEmitter = codeGenerator.BeginCommand(command);
                    commandEmitter.AppendAddressSpace(SourceType.AddressSpace);
                    commandEmitter.AppendSuffix(register.BasicValueType);
                    commandEmitter.AppendArgument(register);
                    commandEmitter.AppendArgumentValue(AddressRegister, offset);
                }
            }

            public LoadEmitter(
                PointerType sourceType,
                HardwareRegister addressRegister)
            {
                Emitter = new IOEmitter(sourceType, addressRegister);
            }

            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private IOEmitter Emitter { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister register,
                int offset) =>
                codeGenerator.EmitIOLoad(
                    Emitter,
                    command,
                    register as HardwareRegister,
                    offset);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister[] primitiveRegisters,
                int offset)
            {
                using var commandEmitter = codeGenerator.BeginCommand(command);
                commandEmitter.AppendAddressSpace(Emitter.SourceType.AddressSpace);
                commandEmitter.AppendVectorSuffix(primitiveRegisters.Length);
                commandEmitter.AppendSuffix(primitiveRegisters[0].BasicValueType);
                commandEmitter.AppendVectorArgument(primitiveRegisters);
                commandEmitter.AppendArgumentValue(Emitter.AddressRegister, offset);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Load)"/>
        public void GenerateCode(Load load)
        {
            var address = LoadHardware(load.Source);
            var sourceType = load.Source.Type as PointerType;
            var targetRegister = Allocate(load);

            EmitVectorizedCommand(
                load.Source,
                sourceType.ElementType.Alignment,
                PTXInstructions.LoadOperation,
                new LoadEmitter(sourceType, address),
                targetRegister);
        }

        /// <summary>
        /// Emits complex store instructions.
        /// </summary>
        private readonly struct StoreEmitter : IVectorizedCommandEmitter
        {
            private readonly struct IOEmitter : IIOEmitter<int>
            {
                public IOEmitter(
                    PointerType targetType,
                    HardwareRegister addressRegister)
                {
                    TargetType = targetType;
                    AddressRegister = addressRegister;
                }

                /// <summary>
                /// The current source type.
                /// </summary>
                public PointerType TargetType { get; }

                /// <summary>
                /// Returns the associated address register.
                /// </summary>
                public HardwareRegister AddressRegister { get; }

                /// <summary>
                /// Emits nested stores.
                /// </summary>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister register,
                    int offset)
                {
                    using var commandEmitter = codeGenerator.BeginCommand(command);
                    commandEmitter.AppendAddressSpace(TargetType.AddressSpace);
                    commandEmitter.AppendSuffix(register.BasicValueType);
                    commandEmitter.AppendArgumentValue(AddressRegister, offset);
                    commandEmitter.AppendArgument(register);
                }
            }

            public StoreEmitter(
                PointerType targetType,
                HardwareRegister addressRegister)
            {
                Emitter = new IOEmitter(targetType, addressRegister);
            }

            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private IOEmitter Emitter { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister register,
                int offset) =>
                codeGenerator.EmitIOStore(
                    Emitter,
                    command,
                    register,
                    offset);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister[] primitiveRegisters,
                int offset)
            {
                using var commandEmitter = codeGenerator.BeginCommand(command);
                commandEmitter.AppendAddressSpace(Emitter.TargetType.AddressSpace);
                commandEmitter.AppendVectorSuffix(primitiveRegisters.Length);
                commandEmitter.AppendSuffix(primitiveRegisters[0].BasicValueType);
                commandEmitter.AppendArgumentValue(Emitter.AddressRegister, offset);
                commandEmitter.AppendVectorArgument(primitiveRegisters);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Store)"/>
        public void GenerateCode(Store store)
        {
            var address = LoadHardware(store.Target);
            var targetType = store.Target.Type as PointerType;
            var value = Load(store.Value);

            EmitVectorizedCommand(
                store.Target,
                targetType.ElementType.Alignment,
                PTXInstructions.StoreOperation,
                new StoreEmitter(targetType, address),
                value);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(LoadFieldAddress)"/>
        public void GenerateCode(LoadFieldAddress value)
        {
            var source = LoadPrimitive(value.Source);
            var fieldOffset = value.StructureType.GetOffset(
                value.FieldSpan.Access);

            if (fieldOffset != 0)
            {
                var targetRegister = AllocatePlatformRegister(
                    value,
                    out RegisterDescription _);
                using var command = BeginCommand(
                    PTXInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Add,
                        Backend.PointerArithmeticType,
                        false));
                command.AppendArgument(targetRegister);
                command.AppendArgument(source);
                command.AppendConstant(fieldOffset);
            }
            else
            {
                Alias(value, value.Source);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PrimitiveValue)"/>
        public void GenerateCode(PrimitiveValue value)
        {
            var description = ResolveRegisterDescription(value.Type);
            Bind(value, new ConstantRegister(description, value));
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(StringValue)"/>
        public void GenerateCode(StringValue value)
        {
            // Check for already existing global constant
            if (!stringConstants.TryGetValue(value.String, out string stringBinding))
            {
                stringBinding = "__strconst" + value.Id;
                stringConstants.Add(value.String, stringBinding);
            }

            var register = AllocatePlatformRegister(
                value,
                out RegisterDescription description);
            using var command = BeginMove();
            command.AppendSuffix(description.BasicValueType);
            command.AppendArgument(register);
            command.AppendRawValueReference(stringBinding);
        }

        /// <summary>
        /// Emits complex null values.
        /// </summary>
        private readonly struct NullEmitter : IComplexCommandEmitter
        {
            /// <summary>
            /// Emits nested null values.
            /// </summary>
            public void Emit(
                CommandEmitter commandEmitter,
                PrimitiveRegister[] registers)
            {
                var primaryRegister = registers[0];

                commandEmitter.AppendRegisterMovementSuffix(
                    primaryRegister.BasicValueType);
                commandEmitter.AppendArgument(primaryRegister);
                commandEmitter.AppendNull(primaryRegister.Kind);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(NullValue)"/>
        public void GenerateCode(NullValue value)
        {
            switch (value.Type)
            {
                case VoidType _:
                    // Ignore void type nulls
                    break;
                default:
                    var targetRegister = Allocate(value);
                    EmitComplexCommand(
                        PTXInstructions.MoveOperation,
                        new NullEmitter(),
                        targetRegister);
                    break;
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(StructureValue)"/>
        public void GenerateCode(StructureValue value)
        {
            var childRegisters = ImmutableArray.CreateBuilder<Register>(value.Count);
            for (int i = 0, e = value.Count; i < e; ++i)
                childRegisters.Add(Load(value[i]));
            Bind(
                value,
                new CompoundRegister(
                    value.StructureType,
                    childRegisters.MoveToImmutable()));
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GetField)"/>
        public void GenerateCode(GetField value)
        {
            var source = LoadAs<CompoundRegister>(value.ObjectValue);
            if (!value.FieldSpan.HasSpan)
            {
                Bind(value, source.Children[value.FieldSpan.Index]);
            }
            else
            {
                int span = value.FieldSpan.Span;
                var childRegisters = ImmutableArray.CreateBuilder<Register>(span);
                for (int i = 0; i < span; ++i)
                    childRegisters.Add(source.Children[i + value.FieldSpan.Index]);
                Bind(
                    value,
                    new CompoundRegister(
                        value.Type as StructureType,
                        childRegisters.MoveToImmutable()));
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(SetField)"/>
        public void GenerateCode(SetField value)
        {
            var source = LoadAs<CompoundRegister>(value.ObjectValue);
            var type = value.StructureType;
            var childRegisters = ImmutableArray.CreateBuilder<Register>(type.NumFields);
            for (int i = 0, e = type.NumFields; i < e; ++i)
                childRegisters.Add(source.Children[i]);

            if (!value.FieldSpan.HasSpan)
            {
                childRegisters[value.FieldSpan.Index] = Load(value.Value);
            }
            else
            {
                var structureValue = LoadAs<CompoundRegister>(value.Value);
                for (int i = 0; i < value.FieldSpan.Span; ++i)
                {
                    childRegisters[i + value.FieldSpan.Index] =
                        structureValue.Children[i];
                }
            }
            Bind(
                value,
                new CompoundRegister(
                    value.StructureType,
                    childRegisters.MoveToImmutable()));
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GridIndexValue)"/>
        public void GenerateCode(GridIndexValue value) =>
            MoveFromIntrinsicRegister(
                value,
                PTXRegisterKind.Ctaid,
                (int)value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GroupIndexValue)"/>
        public void GenerateCode(GroupIndexValue value) =>
            MoveFromIntrinsicRegister(
                value,
                PTXRegisterKind.Tid,
                (int)value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GridDimensionValue)"/>
        public void GenerateCode(GridDimensionValue value) =>
            MoveFromIntrinsicRegister(
                value,
                PTXRegisterKind.NctaId,
                (int)value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GroupDimensionValue)"/>
        public void GenerateCode(GroupDimensionValue value) =>
            MoveFromIntrinsicRegister(
                value,
                PTXRegisterKind.NtId,
                (int)value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(WarpSizeValue)"/>
        public void GenerateCode(WarpSizeValue value) =>
            throw new InvalidCodeGenerationException();

        /// <summary cref="IBackendCodeGenerator.GenerateCode(LaneIdxValue)"/>
        public void GenerateCode(LaneIdxValue value) =>
            MoveFromIntrinsicRegister(
                value,
                PTXRegisterKind.LaneId);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PredicateBarrier)"/>
        public void GenerateCode(PredicateBarrier barrier)
        {
            var targetRegister = AllocateHardware(barrier);
            var sourcePredicate = LoadPrimitive(barrier.Predicate);
            switch (barrier.Kind)
            {
                case PredicateBarrierKind.And:
                case PredicateBarrierKind.Or:
                    using (var command = BeginCommand(
                        PTXInstructions.GetPredicateBarrier(barrier.Kind)))
                    {
                        command.AppendArgument(targetRegister);
                        command.AppendConstant(0);
                        command.AppendArgument(sourcePredicate);
                    }
                    break;
                case PredicateBarrierKind.PopCount:
                    using (var command = BeginCommand(
                        PTXInstructions.GetPredicateBarrier(barrier.Kind)))
                    {
                        command.AppendArgument(targetRegister);
                        command.AppendConstant(0);
                        command.AppendArgument(sourcePredicate);
                    }
                    break;
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Barrier)"/>
        public void GenerateCode(Barrier barrier)
        {
            using var command = BeginCommand(PTXInstructions.GetBarrier(barrier.Kind));
            switch (barrier.Kind)
            {
                case BarrierKind.WarpLevel:
                    command.AppendConstant(
                        PTXInstructions.AllThreadsInAWarpMemberMask);
                    break;
                case BarrierKind.GroupLevel:
                    command.AppendConstant(0);
                    break;
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        /// <summary>
        /// Represents an abstract emitter of warp shuffle masks.
        /// </summary>
        private interface IShuffleEmitter
        {
            /// <summary>
            /// Emits a new warp mask.
            /// </summary>
            /// <param name="commandEmitter">The current command emitter.</param>
            void EmitWarpMask(CommandEmitter commandEmitter);
        }

        /// <summary>
        /// Creates a new shuffle operation.
        /// </summary>
        /// <typeparam name="TShuffleEmitter">The emitter type.</typeparam>
        /// <param name="shuffle">The current shuffle operation.</param>
        /// <param name="shuffleEmitter">The shuffle emitter.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EmitShuffleOperation<TShuffleEmitter>(
            ShuffleOperation shuffle,
            in TShuffleEmitter shuffleEmitter)
            where TShuffleEmitter : IShuffleEmitter
        {
            var variable = LoadPrimitive(shuffle.Variable);
            var delta = LoadPrimitive(shuffle.Origin);

            var targetRegister = Allocate(shuffle, variable.Description);

            var shuffleOperation = PTXInstructions.GetShuffleOperation(shuffle.Kind);
            using var command = BeginCommand(shuffleOperation);
            command.AppendArgument(targetRegister);
            command.AppendArgument(variable);
            command.AppendArgument(delta);

            // Invoke the shuffle emitter
            shuffleEmitter.EmitWarpMask(command);

            command.AppendConstant(PTXInstructions.AllThreadsInAWarpMemberMask);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Broadcast)"/>
        public void GenerateCode(Broadcast broadcast) =>
            throw new InvalidCodeGenerationException();

        /// <summary>
        /// Emits warp masks of <see cref="WarpShuffle"/> operations.
        /// </summary>
        private readonly struct WarpShuffleEmitter : IShuffleEmitter
        {
            /// <summary>
            /// The basic mask that has be combined with an 'or' command
            /// in case of a <see cref="ShuffleKind.Xor"/> or a
            /// <see cref="ShuffleKind.Down"/> shuffle instruction.
            /// </summary>
            public const int XorDownMask = 0x1f;

            /// <summary>
            /// The amount of bits the basic mask has to be shifted to
            /// the left.
            /// </summary>
            public const int BaseMaskShiftAmount = 8;

            /// <summary>
            /// Constructs a new shuffle emitter.
            /// </summary>
            /// <param name="shuffleKind">The current shuffle kind.</param>
            public WarpShuffleEmitter(ShuffleKind shuffleKind)
            {
                ShuffleKind = shuffleKind;
            }

            /// <summary>
            /// The shuffle kind.
            /// </summary>
            public ShuffleKind ShuffleKind { get; }

            /// <summary cref="IShuffleEmitter.EmitWarpMask(CommandEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitWarpMask(CommandEmitter commandEmitter)
            {
                if (ShuffleKind == ShuffleKind.Up)
                    commandEmitter.AppendConstant(0);
                else
                    commandEmitter.AppendConstant(XorDownMask);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(WarpShuffle)"/>
        public void GenerateCode(WarpShuffle shuffle) =>
            EmitShuffleOperation(
                shuffle,
                new WarpShuffleEmitter(shuffle.Kind));

        /// <summary>
        /// Emits warp masks of <see cref="SubWarpShuffle"/> operations.
        /// </summary>
        private readonly struct SubWarpShuffleEmitter : IShuffleEmitter
        {
            /// <summary>
            /// Constructs a new shuffle emitter.
            /// </summary>
            /// <param name="warpMaskRegister">The current mask register.</param>
            public SubWarpShuffleEmitter(PrimitiveRegister warpMaskRegister)
            {
                WarpMaskRegister = warpMaskRegister;
            }

            /// <summary>
            /// Returns the current mask register.
            /// </summary>
            public PrimitiveRegister WarpMaskRegister { get; }

            /// <summary cref="IShuffleEmitter.EmitWarpMask(CommandEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitWarpMask(CommandEmitter commandEmitter) =>
                commandEmitter.AppendArgument(WarpMaskRegister);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(SubWarpShuffle)"/>
        public void GenerateCode(SubWarpShuffle shuffle)
        {
            // Compute the actual warp mask
            var width = LoadPrimitive(shuffle.Width);

            // Create basic mask
            var baseRegister = AllocateRegister(width.Description);
            using (var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Sub,
                    ArithmeticBasicValueType.UInt32,
                    false)))
            {
                command.AppendArgument(baseRegister);
                command.AppendConstant(PTXBackend.WarpSize);
                command.AppendArgument(width);
            }

            // Shift mask
            var maskRegister = AllocateRegister(width.Description);
            using (var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Shl,
                    ArithmeticBasicValueType.UInt32,
                    false)))
            {
                command.AppendArgument(maskRegister);
                command.AppendArgument(baseRegister);
                command.AppendConstant(WarpShuffleEmitter.BaseMaskShiftAmount);
            }
            FreeRegister(baseRegister);

            // Adjust mask register
            if (shuffle.Kind != ShuffleKind.Up)
            {
                var adjustedMaskRegister = AllocateRegister(width.Description);
                using (var command = BeginCommand(
                    PTXInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Or,
                        ArithmeticBasicValueType.UInt32,
                        false)))
                {
                    command.AppendArgument(adjustedMaskRegister);
                    command.AppendArgument(maskRegister);
                    command.AppendConstant(WarpShuffleEmitter.XorDownMask);
                }

                FreeRegister(maskRegister);
                maskRegister = adjustedMaskRegister;
            }

            EmitShuffleOperation(
                shuffle,
                new SubWarpShuffleEmitter(maskRegister));
            FreeRegister(maskRegister);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(DebugOperation)"/>
        public void GenerateCode(DebugOperation debug) =>
            Debug.Assert(false, "Invalid debug node -> should have been removed");
    }
}
