// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.Values.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using ILGPUC.Util;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPUC.Backends.PTX;

partial class PTXCodeGenerator
{
    /// <summary>
    /// Generates code for <see cref="MethodCall"/> values.
    /// </summary>
    public void GenerateCode(MethodCall methodCall)
    {
        const string ReturnValueName = "callRetVal";
        const string CallParamName = "callParam";

        var target = methodCall.Target;

        // Create call sequence
        Builder.AppendLine();
        Builder.AppendLine("\t{");

        for (int i = 0, e = methodCall.Count; i < e; ++i)
        {
            var argument = methodCall.Nodes[i];
            var paramName = CallParamName + i;
            Builder.Append('\t');
            AppendParamDeclaration(Builder, argument.Type, paramName);
            Builder.AppendLine(";");

            // Emit store param command
            var argumentRegister = Load(argument);
            EmitStoreParam(paramName, argumentRegister);
        }

        // Reserve a sufficient amount of memory
        var returnType = target.ReturnType;
        string callCommand = Uniforms.IsUniform(methodCall)
            ? PTXInstructions.UniformMethodCall
            : PTXInstructions.MethodCall;
        if (!returnType.IsVoidType)
        {
            Builder.Append('\t');
            AppendParamDeclaration(Builder, returnType, ReturnValueName);
            Builder.AppendLine(";");
            Builder.Append('\t');
            Builder.Append(callCommand);
            Builder.Append(' ');
            Builder.Append('(');
            Builder.Append(ReturnValueName);
            Builder.Append("), ");
        }
        else
        {
            Builder.Append('\t');
            Builder.Append(callCommand);
            Builder.Append(' ');
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

    /// <summary>
    /// Generates code for <see cref="Parameter"/> values.
    /// </summary>
    public void GenerateCode(Parameter parameter)
    {
        // Parameters are already assigned to registers
    }

    /// <summary>
    /// Generates code for <see cref="PhiValue"/> values.
    /// </summary>
    public void GenerateCode(PhiValue phiValue)
    {
        // Phi values are already assigned to registers
    }

    /// <summary>
    /// Generates code for <see cref="UnaryArithmeticValue"/> values.
    /// </summary>
    public void GenerateCode(UnaryArithmeticValue value)
    {
        var argument = LoadPrimitive(value.Value);
        var targetRegister = AllocateHardware(value);

        using var command = BeginCommand(
            PTXInstructions.GetArithmeticOperation(
                value.Kind,
                value.ArithmeticBasicValueType,
                Capabilities,
                FastMath));
        command.AppendArgument(targetRegister);
        command.AppendArgument(argument);
    }

    /// <summary>
    /// Generates code for <see cref="BinaryArithmeticValue"/> values.
    /// </summary>
    public void GenerateCode(BinaryArithmeticValue value)
    {
        var left = LoadPrimitive(value.Left);
        var right = LoadPrimitive(value.Right);

        var targetRegister = Allocate(value, left.Description);
        using var command = BeginCommand(
            PTXInstructions.GetArithmeticOperation(
                value.Kind,
                value.ArithmeticBasicValueType,
                Capabilities,
                FastMath));
        command.AppendArgument(targetRegister);
        command.AppendArgument(left);
        command.AppendArgument(right);
    }

    /// <summary>
    /// Generates code for <see cref="TernaryArithmeticValue"/> values.
    /// </summary>
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

    /// <summary>
    /// Generates code for <see cref="CompareValue"/> values.
    /// </summary>
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
                    Capabilities,
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
                        Capabilities,
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

    /// <summary>
    /// Generates code for <see cref="ConvertValue"/> values.
    /// </summary>
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

    /// <summary>
    /// Generates code for <see cref="FloatAsIntCast"/> values.
    /// </summary>
    public void GenerateCode(FloatAsIntCast value)
    {
        var source = LoadHardware(value.Value);
        if (source.Kind == PTXRegisterKind.Int16)
        {
            // Reuse the register, since int16 and fp16 registers are the same
            Bind(value, source);
        }
        else
        {
            Debug.Assert(
                source.Kind == PTXRegisterKind.Float32 ||
                source.Kind == PTXRegisterKind.Float64);

            var targetRegister = AllocateHardware(value);
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Int32 ||
                targetRegister.Kind == PTXRegisterKind.Int64);

            Move(source, targetRegister);
        }
    }

    /// <summary>
    /// Generates code for <see cref="IntAsFloatCast"/> values.
    /// </summary>
    public void GenerateCode(IntAsFloatCast value)
    {
        var source = LoadHardware(value.Value);
        if (source.Kind == PTXRegisterKind.Int16)
        {
            // Reuse the register, since int16 and fp16 registers are the same
            Bind(value, source);
        }
        else
        {
            Debug.Assert(
                source.Kind == PTXRegisterKind.Int32 ||
                source.Kind == PTXRegisterKind.Int64);

            var targetRegister = AllocateHardware(value);
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Float32 ||
                targetRegister.Kind == PTXRegisterKind.Float64);

            Move(source, targetRegister);
        }
    }

    /// <summary>
    /// Emits complex predicate instructions.
    /// </summary>
    private readonly struct PredicateEmitter(PrimitiveRegister predicateRegister) :
        IComplexCommandEmitter
    {
        /// <summary>
        /// Gets the actual select command.
        /// </summary>
        public string AdjustCommand(string command, PrimitiveRegister[] registers) =>
            PTXInstructions.GetSelectValueOperation(registers[0].BasicValueType);

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
            commandEmitter.AppendArgument(predicateRegister);
        }
    }

    /// <summary>
    /// Generates code for <see cref="Predicate"/> values.
    /// </summary>
    public void GenerateCode(Predicate predicate)
    {
        var condition = LoadPrimitive(predicate.Condition);
        var trueValue = Load(predicate.TrueValue);
        var falseValue = Load(predicate.FalseValue);

        var targetRegister = Allocate(predicate);
        if (predicate.BasicValueType == BasicValueType.Int1)
        {
            // We need a specific sequence of instructions for predicate registers
            var conditionRegister = EnsureHardwareRegister(condition);
            using (var statement1 = BeginMove(
                new PredicateConfiguration(conditionRegister, true)))
            {
                statement1.AppendSuffix(BasicValueType.Int1);
                statement1.AppendArgument(
                    targetRegister.AsNotNullCast<PrimitiveRegister>());
                statement1.AppendArgument(
                    trueValue.AsNotNullCast<PrimitiveRegister>());
            }

            using var statement2 = BeginMove(
                new PredicateConfiguration(conditionRegister, false));
            statement2.AppendSuffix(BasicValueType.Int1);
            statement2.AppendArgument(
                targetRegister.AsNotNullCast<PrimitiveRegister>());
            statement2.AppendArgument(
                falseValue.AsNotNullCast<PrimitiveRegister>());
        }
        else
        {
            EmitComplexCommand(
                string.Empty,
                new PredicateEmitter(condition),
                targetRegister,
                trueValue,
                falseValue);
        }
    }

    /// <summary>
    /// Generates code for <see cref="GenericAtomic"/> values.
    /// </summary>
    public void GenerateCode(GenericAtomic atomic)
    {
        var target = LoadHardware(atomic.Target);
        var value = LoadPrimitive(atomic.Value);

        var requiresResult =
            atomic.Uses.HasAny ||
            atomic.Kind == GenericAtomicKind.Exchange;
        var atomicOperation = PTXInstructions.GetAtomicOperation(
            atomic.Kind,
            requiresResult);
        var suffix = PTXInstructions.GetAtomicOperationSuffix(
            atomic.Kind,
            atomic.ArithmeticBasicValueType);

        var targetRegister = requiresResult ? AllocateHardware(atomic) : default;
        using var command = BeginCommand(atomicOperation);
        command.AppendNonLocalAddressSpace(
            atomic.Target.Type.AsNotNullCast<AddressSpaceType>().AddressSpace);
        command.AppendSuffix(suffix);
        if (requiresResult)
            command.AppendArgument(targetRegister.AsNotNull());
        command.AppendArgumentValue(target);
        command.AppendArgument(value);
    }

    /// <summary>
    /// Generates code for <see cref="AtomicCAS"/> values.
    /// </summary>
    public void GenerateCode(AtomicCAS atomicCAS)
    {
        var target = LoadHardware(atomicCAS.Target);
        var value = LoadPrimitive(atomicCAS.Value);
        var compare = LoadPrimitive(atomicCAS.CompareValue);

        var targetRegister = AllocateHardware(atomicCAS);

        using var command = BeginCommand(PTXInstructions.AtomicCASOperation);
        command.AppendNonLocalAddressSpace(
            atomicCAS.Target.Type.AsNotNullCast<AddressSpaceType>().AddressSpace);
        command.AppendSuffix(atomicCAS.BasicValueType);
        command.AppendArgument(targetRegister);
        command.AppendArgumentValue(target);
        command.AppendArgument(value);
        command.AppendArgument(compare);
    }

    /// <summary>
    /// Generates code for <see cref="Alloca"/> values.
    /// </summary>
    public void GenerateCode(Alloca alloca)
    {
        // Ignore alloca
    }

    /// <summary>
    /// Emits complex load instructions.
    /// </summary>
    private readonly struct LoadEmitter(
        PointerType sourceType,
        HardwareRegister addressRegister) : IVectorizedCommandEmitter
    {
        private readonly struct IOEmitter(
            PointerType sourceType,
            HardwareRegister addressRegister) : IIOEmitter<int>
        {
            /// <summary>
            /// Emits nested loads.
            /// </summary>
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister register,
                int offset)
            {
                using var commandEmitter = codeGenerator.BeginCommand(command);
                commandEmitter.AppendAddressSpace(sourceType.AddressSpace);
                commandEmitter.AppendSuffix(
                    ResolveIOType(register.BasicValueType));
                commandEmitter.AppendArgument(register);
                commandEmitter.AppendArgumentValue(addressRegister, offset);
            }
        }

        public void Emit(
            PTXCodeGenerator codeGenerator,
            string command,
            PrimitiveRegister register,
            int offset) =>
            codeGenerator.EmitIOLoad(
                new IOEmitter(sourceType, addressRegister),
                command,
                register.AsNotNullCast<HardwareRegister>(),
                offset);

        public void Emit(
            PTXCodeGenerator codeGenerator,
            string command,
            PrimitiveRegister[] primitiveRegisters,
            int offset)
        {
            using var commandEmitter = codeGenerator.BeginCommand(command);
            commandEmitter.AppendAddressSpace(sourceType.AddressSpace);
            commandEmitter.AppendVectorSuffix(primitiveRegisters.Length);
            commandEmitter.AppendSuffix(
                ResolveIOType(primitiveRegisters[0].BasicValueType));
            commandEmitter.AppendVectorArgument(primitiveRegisters);
            commandEmitter.AppendArgumentValue(addressRegister, offset);
        }
    }

    /// <summary>
    /// Generates code for <see cref="Load"/> values.
    /// </summary>
    public void GenerateCode(Load load)
    {
        var address = LoadHardware(load.Source);
        var sourceType = load.Source.Type.AsNotNullCast<PointerType>();
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
    private readonly struct StoreEmitter(
        PointerType targetType,
        HardwareRegister addressRegister) : IVectorizedCommandEmitter
    {
        private readonly struct IOEmitter(
            PointerType targetType,
            HardwareRegister addressRegister) : IIOEmitter<int>
        {
            /// <summary>
            /// Emits nested stores.
            /// </summary>
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister register,
                int offset)
            {
                using var commandEmitter = codeGenerator.BeginCommand(command);
                commandEmitter.AppendAddressSpace(targetType.AddressSpace);
                commandEmitter.AppendSuffix(
                    ResolveIOType(register.BasicValueType));
                commandEmitter.AppendArgumentValue(addressRegister, offset);
                commandEmitter.AppendArgument(register);
            }
        }

        public void Emit(
            PTXCodeGenerator codeGenerator,
            string command,
            PrimitiveRegister register,
            int offset) =>
            codeGenerator.EmitIOStore(
                new IOEmitter(targetType, addressRegister),
                command,
                register,
                offset);

        public void Emit(
            PTXCodeGenerator codeGenerator,
            string command,
            PrimitiveRegister[] primitiveRegisters,
            int offset)
        {
            using var commandEmitter = codeGenerator.BeginCommand(command);
            commandEmitter.AppendAddressSpace(targetType.AddressSpace);
            commandEmitter.AppendVectorSuffix(primitiveRegisters.Length);
            commandEmitter.AppendSuffix(
                ResolveIOType(primitiveRegisters[0].BasicValueType));
            commandEmitter.AppendArgumentValue(addressRegister, offset);
            commandEmitter.AppendVectorArgument(primitiveRegisters);
        }
    }

    /// <summary>
    /// Generates code for <see cref="Store"/> values.
    /// </summary>
    public void GenerateCode(Store store)
    {
        var address = LoadHardware(store.Target);
        var targetType = store.Target.Type.AsNotNullCast<PointerType>();
        var value = Load(store.Value);

        EmitVectorizedCommand(
            store.Target,
            targetType.ElementType.Alignment,
            PTXInstructions.StoreOperation,
            new StoreEmitter(targetType, address),
            value);
    }

    /// <summary>
    /// Generates code for <see cref="LoadFieldAddress"/> values.
    /// </summary>
    public void GenerateCode(LoadFieldAddress value)
    {
        var source = LoadPrimitive(value.Source);
        var fieldOffset = value.StructureType.GetOffset(
            value.FieldSpan.Access);

        if (fieldOffset != 0)
        {
            var targetRegister = AllocateHardware(value);
            using var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Add,
                    PointerArithmeticType,
                    Capabilities,
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

    /// <summary>
    /// Generates code for <see cref="AlignTo"/> values.
    /// </summary>
    public void GenerateCode(AlignTo value)
    {
        // Load the 32-bit or 64-bit base pointer
        var ptr = LoadHardware(value.Source);
        var arithmeticBasicValueType =
            value.Source.BasicValueType.GetArithmeticBasicValueType(true);

        // Load the alignment value into a register
        var alignment = LoadPrimitive(value.AlignmentInBytes);

        // var baseOffset = (int)ptr & (alignmentInBytes - 1);
        var tempRegister = AllocateRegister(ptr.Description);

        // Get the specialized and and convert operations
        var andOperation = PTXInstructions.GetArithmeticOperation(
            BinaryArithmeticKind.And,
            arithmeticBasicValueType,
            Capabilities,
            FastMath);
        var convertOperation = PTXInstructions.GetConvertOperation(
            alignment.BasicValueType.GetArithmeticBasicValueType(
                isUnsigned: false),
            tempRegister.BasicValueType.GetArithmeticBasicValueType(
                isUnsigned: true));

        // Check for a predefined alignment constant
        bool hasConstantAlignment;
        if (hasConstantAlignment = value.TryGetAlignmentConstant(
            out int alignmentConstant))
        {
            // Emit a specialized instruction using an inline constant
            using var command = BeginCommand(andOperation);
            command.AppendArgument(tempRegister);
            command.AppendArgument(ptr);
            command.AppendConstant(alignmentConstant);
        }
        else
        {
            // Convert the alignment information if necessary
            if (tempRegister.Kind != alignment.Kind)
            {
                using var convert = BeginCommand(convertOperation);
                convert.AppendArgument(tempRegister);
                convert.AppendArgument(alignment);
            }

            // Compute the actual alignment mask
            using (var alignmentMinusOne = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Sub,
                    arithmeticBasicValueType,
                    Capabilities,
                    FastMath)))
            {
                alignmentMinusOne.AppendArgument(tempRegister);
                alignmentMinusOne.AppendArgument(tempRegister);
                alignmentMinusOne.AppendConstant(1);
            }

            // Compute the actual temp register contents
            using var command = BeginCommand(andOperation);
            command.AppendArgument(tempRegister);
            command.AppendArgument(ptr);
            command.AppendArgument(tempRegister);
        }

        // if (baseOffset == 0) ...
        using var predicate = new PredicateScope(this);
        using (var command = BeginCommand(
            PTXInstructions.GetCompareOperation(
                CompareKind.Equal,
                CompareFlags.None,
                arithmeticBasicValueType)))
        {
            command.AppendArgument(predicate.PredicateRegister);
            command.AppendArgument(tempRegister);
            command.AppendConstant(0);
        }

        // Allocate the target register
        var targetRegister = AllocateHardware(value);

        // Use the same value as before the case of baseOffset = 0
        Move(
            ptr,
            targetRegister,
            predicate: predicate.GetConfiguration(true));

        // We need a temporary register to store the converted alignment
        var alignmentOffsetRegister = AllocateRegister(ptr.Description);
        if (!hasConstantAlignment && alignmentOffsetRegister.Kind != alignment.Kind)
        {
            using var convert = BeginCommand(
                convertOperation,
                predicate: predicate.GetConfiguration(false));
            convert.AppendArgument(alignmentOffsetRegister);
            convert.AppendArgument(alignment);
        }
        else
        {
            // Move the alignment constant into the offset register
            using var move = BeginMove(
                predicate: predicate.GetConfiguration(false));
            move.AppendArgument(alignmentOffsetRegister);
            move.AppendConstant(alignmentConstant);
        }

        // Compute the alignment offset:
        // baseOffset = alignment - baseOffset
        using (var command = BeginCommand(
            PTXInstructions.GetArithmeticOperation(
                BinaryArithmeticKind.Sub,
                arithmeticBasicValueType,
                Capabilities,
                FastMath),
            predicate: predicate.GetConfiguration(false)))
        {
            command.AppendArgument(tempRegister);
            command.AppendArgument(alignmentOffsetRegister);
            command.AppendArgument(tempRegister);
        }

        // Adjust the given pointer if baseOffset != 0
        using (var command = BeginCommand(
            PTXInstructions.GetArithmeticOperation(
                BinaryArithmeticKind.Add,
                arithmeticBasicValueType,
                Capabilities,
                FastMath),
            predicate: predicate.GetConfiguration(false)))
        {
            command.AppendArgument(targetRegister);
            command.AppendArgument(ptr);
            command.AppendArgument(tempRegister);
        }

        Free(tempRegister);
        Free(alignmentOffsetRegister);
    }

    /// <summary>
    /// Generates code for <see cref="PrimitiveValue"/> values.
    /// </summary>
    public void GenerateCode(PrimitiveValue value)
    {
        // Check whether we are loading an FP16 value. In this case, we have to
        // move the resulting constant into a register since the PTX compiler
        // expects a converted FP16 value in the scope of a register.
        var description = ResolveRegisterDescription(value.Type);
        var register = new ConstantRegister(description, value);
        if (value.BasicValueType == BasicValueType.Float16)
            Bind(value, EnsureHardwareRegister(register));
        else
            Bind(value, register);
    }

    /// <summary>
    /// Generates code for <see cref="StringValue"/> values.
    /// </summary>
    public void GenerateCode(StringValue value)
    {
        // Check for already existing global constant
        var key = (value.Encoding, value.String);
        if (!_stringConstants.TryGetValue(key, out string? stringBinding))
        {
            stringBinding = "__strconst" + value.Id;
            _stringConstants.Add(key, stringBinding);
        }

        // Move the value into the target register
        var register = AllocateHardware(value);
        using (var command = BeginMove())
        {
            command.AppendSuffix(register.Description.BasicValueType);
            command.AppendArgument(register);
            command.AppendRawValueReference(stringBinding);
        }

        // Convert the string value into the generic address space
        // string (global) -> string (generic) (in place conversion)
        CreateAddressSpaceCast(
            register,
            register,
            MemoryAddressSpace.Global,
            MemoryAddressSpace.Generic);
    }

    /// <summary>
    /// Emits complex null values.
    /// </summary>
    private readonly struct NullEmitter : IComplexCommandEmitter
    {
        /// <summary>
        /// Returns the same command.
        /// </summary>
        public string AdjustCommand(string command, PrimitiveRegister[] registers) =>
            command;

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

    /// <summary>
    /// Generates code for <see cref="NullValue"/> values.
    /// </summary>
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

    /// <summary>
    /// Generates code for <see cref="StructureValue"/> values.
    /// </summary>
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

    /// <summary>
    /// Generates code for <see cref="GetField"/> values.
    /// </summary>
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
                    value.Type.AsNotNullCast<StructureType>(),
                    childRegisters.MoveToImmutable()));
        }
    }

    /// <summary>
    /// Generates code for <see cref="SetField"/> values.
    /// </summary>
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

    /// <summary>
    /// Generates code for <see cref="GridIndexValue"/> values.
    /// </summary>
    public void GenerateCode(GridIndexValue value) =>
        MoveFromIntrinsicRegister(
            value,
            PTXRegisterKind.Ctaid,
            (int)value.Dimension);

    /// <summary>
    /// Generates code for <see cref="GroupIndexValue"/> values.
    /// </summary>
    public void GenerateCode(GroupIndexValue value) =>
        MoveFromIntrinsicRegister(
            value,
            PTXRegisterKind.Tid,
            (int)value.Dimension);

    /// <summary>
    /// Generates code for <see cref="GridDimensionValue"/> values.
    /// </summary>
    public void GenerateCode(GridDimensionValue value) =>
        MoveFromIntrinsicRegister(
            value,
            PTXRegisterKind.NctaId,
            (int)value.Dimension);

    /// <summary>
    /// Generates code for <see cref="GroupDimensionValue"/> values.
    /// </summary>
    public void GenerateCode(GroupDimensionValue value) =>
        MoveFromIntrinsicRegister(
            value,
            PTXRegisterKind.NtId,
            (int)value.Dimension);

    /// <summary>
    /// Generates code for <see cref="LaneIdxValue"/> values.
    /// </summary>
    public void GenerateCode(LaneIdxValue value) =>
        MoveFromIntrinsicRegister(
            value,
            PTXRegisterKind.LaneId);

    /// <summary>
    /// Generates code for <see cref="DynamicMemoryLengthValue"/> values.
    /// </summary>
    public void GenerateCode(DynamicMemoryLengthValue value)
    {
        if (value.AddressSpace != MemoryAddressSpace.Shared)
            throw new InvalidCodeGenerationException();

        // Load the dynamic memory size (in bytes) from the PTX special register
        // and divide by the size in bytes of the array element.
        var lengthRegister = AllocateHardware(value);
        var dynamicMemorySizeRegister = MoveFromIntrinsicRegister(
            PTXRegisterKind.DynamicSharedMemorySize);

        using var command = BeginCommand(
            PTXInstructions.GetArithmeticOperation(
                BinaryArithmeticKind.Div,
                ArithmeticBasicValueType.UInt32,
                Capabilities,
                false));
        command.AppendArgument(lengthRegister);
        command.AppendArgument(dynamicMemorySizeRegister);
        command.AppendConstant(value.ElementType.Size);
    }

    /// <summary>
    /// Generates code for <see cref="LanguageEmitValue"/> values.
    /// </summary>
    public void GenerateCode(LanguageEmitValue emit)
    {
        // Ignore non-PTX instructions.
        if (emit.Kind != LanguageEmitKind.PTX)
            return;

        // Load argument registers.
        var registers = InlineList<PrimitiveRegister>.Create(emit.Nodes.Length);

        for (var argumentIdx = 0; argumentIdx < emit.Count; argumentIdx++)
        {
            var argument = emit.Nodes[argumentIdx];

            if (emit.UsingRefParams)
            {
                // If there is an input, initialize with the supplied argument value.
                var pointerType = argument.Type.AsNotNullCast<PointerType>();
                var pointerElementType = pointerType.ElementType;

                var targetRegister = AllocateRegister(
                    ResolveRegisterDescription(pointerElementType));
                registers.Add(targetRegister);

                if (emit.IsInputArgument(argumentIdx))
                {
                    var address = LoadHardware(argument);
                    EmitVectorizedCommand(
                        argument,
                        pointerElementType.Alignment,
                        PTXInstructions.LoadOperation,
                        new LoadEmitter(pointerType, address),
                        targetRegister);
                }
            }
            else
            {
                // If there is an output, allocate a new register to store the value.
                registers.Add(
                    emit.IsOutputArgument(argumentIdx)
                    ? AllocateRegister(ResolveRegisterDescription(
                        argument.Type.AsNotNullCast<PointerType>().ElementType))
                    : LoadPrimitive(argument));
            }
        }

        // Emit the PTX assembly string
        Builder.Append('\t');

        using (var emitter = new CommandEmitter(Builder, string.Empty, string.Empty))
        {
            foreach (var expression in emit.Expressions)
            {
                if (expression.HasArgument)
                {
                    emitter.AppendArgument(registers[expression.Argument]);
                }
                else
                {
                    emitter.AppendRawString(expression.String.AsNotNull());
                }
            }
        }

        // For each output argument, write the value to the address.
        for (var argumentIdx = 0; argumentIdx < emit.Count; argumentIdx++)
        {
            if (emit.IsOutputArgument(argumentIdx))
            {
                var outputArgument = emit.Nodes[argumentIdx];
                var address = LoadHardware(outputArgument);
                var targetType = outputArgument.Type.AsNotNullCast<PointerType>();
                var newValue = registers[argumentIdx];

                EmitVectorizedCommand(
                    outputArgument,
                    targetType.ElementType.Alignment,
                    PTXInstructions.StoreOperation,
                    new StoreEmitter(targetType, address),
                    newValue);
            }
        }
    }
}
