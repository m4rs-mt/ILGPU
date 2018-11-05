// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.Values.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Diagnostics;

namespace ILGPU.Backends.PTX
{
    partial class PTXCodeGenerator
    {
        #region Value Visitor

        /// <summary cref="IValueVisitor.Visit(FunctionValue)"/>
        public void Visit(FunctionValue functionValue)
        {
            if (!functionValue.Mark(functionMarker))
                return;

            // Mark entry label
            MarkLabel(blockLookup[functionValue]);
        }

        /// <summary cref="IValueVisitor.Visit(FunctionCall)"/>
        public void Visit(FunctionCall functionCall)
        {
            if (functionCall.IsTopLevelCall)
                MakeGlobalCall(functionCall);
            else
                MakeLocalCall(functionCall);
        }

        /// <summary cref="IValueVisitor.Visit(Parameter)"/>
        public void Visit(Parameter parameter)
        {
            // Parameters are already assigned to registers
#if DEBUG
            Load(parameter);
#endif
        }

        /// <summary cref="IValueVisitor.Visit(UnaryArithmeticValue)"/>
        public void Visit(UnaryArithmeticValue value)
        {
            var argument = Load(value.Value);

            var resolved = Instructions.TryGetArithmeticOperation(
                value.Kind,
                value.ArithmeticBasicValueType,
                FastMath,
                out string operation);
            Debug.Assert(resolved, "Invalid arithmetic operation");

            var ptxType = PTXType.GetPTXType(value.BasicValueType);
            var targetRegister = Allocate(value, ptxType.RegisterKind);
            using (var command = BeginCommand(operation))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(argument);
            }
        }

        /// <summary cref="IValueVisitor.Visit(BinaryArithmeticValue)"/>
        public void Visit(BinaryArithmeticValue value)
        {
            var left = Load(value.Left);
            var right = Load(value.Right);

            var resolved = Instructions.TryGetArithmeticOperation(
                value.Kind,
                value.ArithmeticBasicValueType,
                FastMath,
                out string operation);
            Debug.Assert(resolved, "Invalid arithmetic operation");

            var targetRegister = Allocate(value, left.Kind);
            using (var command = BeginCommand(operation))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(left);
                command.AppendArgument(right);
            }
        }

        /// <summary cref="IValueVisitor.Visit(TernaryArithmeticValue)"/>
        public void Visit(TernaryArithmeticValue value)
        {
            var first = Load(value.First);
            var second = Load(value.Second);
            var third = Load(value.Third);

            var resolved = Instructions.TryGetArithmeticOperation(
                value.Kind,
                value.ArithmeticBasicValueType,
                out string operation);
            Debug.Assert(resolved, "Invalid arithmetic operation");

            var targetRegister = Allocate(value, first.Kind);
            using (var command = BeginCommand(operation))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(first);
                command.AppendArgument(second);
                command.AppendArgument(third);
            }
        }

        /// <summary cref="IValueVisitor.Visit(CompareValue)"/>
        public void Visit(CompareValue value)
        {
            var left = Load(value.Left);
            var right = Load(value.Right);

            var predicateRegister = Allocate(value, PTXRegisterKind.Predicate);
            var compareOperation = Instructions.GetCompareOperation(
                value.Kind,
                value.CompareType);

            using (var command = BeginCommand(compareOperation))
            {
                command.AppendArgument(predicateRegister);
                command.AppendArgument(left);
                command.AppendArgument(right);
            }
        }

        /// <summary cref="IValueVisitor.Visit(ConvertValue)"/>
        public void Visit(ConvertValue value)
        {
            var sourceValue = Load(value.Value);

            var convertOperation = Instructions.GetConvertOperation(
                value.SourceType,
                value.TargetType);

            var ptxType = PTXType.GetPTXType(value.Type, ABI);
            var targetRegister = Allocate(value, ptxType.RegisterKind);
            using (var command = BeginCommand(convertOperation))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(sourceValue);
            }
        }

        /// <summary cref="IValueVisitor.Visit(PointerCast)"/>
        public void Visit(PointerCast value)
        {
            Alias(value, value.Value);
        }

        /// <summary cref="IValueVisitor.Visit(AddressSpaceCast)"/>
        public void Visit(AddressSpaceCast addressCast)
        {
            var source = Load(addressCast.Value);

            var targetRegister = AllocatePlatformRegister(addressCast, out PTXType postFix);
            var toGeneric = addressCast.TargetAddressSpace == MemoryAddressSpace.Generic;
            var addressSpaceOperation = Instructions.GetAddressSpaceCast(toGeneric);
            using (var command = BeginCommand(addressSpaceOperation))
            {
                command.AppendAddressSpace(
                    toGeneric ?
                    (addressCast.Value.Type as AddressSpaceType).AddressSpace :
                    addressCast.TargetAddressSpace);

                command.AppendPostFix(postFix);
                command.AppendArgument(targetRegister);
                command.AppendArgument(source);
            }
        }

        /// <summary cref="IValueVisitor.Visit(ViewCast)"/>
        public void Visit(ViewCast value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(FloatAsIntCast)"/>
        public void Visit(FloatAsIntCast value)
        {
            var source = Load(value.Value);
            Debug.Assert(
                source.Kind == PTXRegisterKind.Float32 ||
                source.Kind == PTXRegisterKind.Float64);

            var registerType = PTXType.GetPTXType(value.BasicValueType);
            var targetRegister = Allocate(value, registerType.RegisterKind);
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Int32 ||
                targetRegister.Kind == PTXRegisterKind.Int64);

            Move(source, targetRegister);
        }

        /// <summary cref="IValueVisitor.Visit(IntAsFloatCast)"/>
        public void Visit(IntAsFloatCast value)
        {
            var source = Load(value.Value);
            Debug.Assert(
                source.Kind == PTXRegisterKind.Int32 ||
                source.Kind == PTXRegisterKind.Int64);

            var registerType = PTXType.GetPTXType(value.BasicValueType);
            var targetRegister = Allocate(value, registerType.RegisterKind);
            Debug.Assert(
                targetRegister.Kind == PTXRegisterKind.Float32 ||
                targetRegister.Kind == PTXRegisterKind.Float64);

            Move(source, targetRegister);
        }

        /// <summary cref="IValueVisitor.Visit(Predicate)"/>
        public void Visit(Predicate predicate)
        {
            if (predicate.IsHigherOrder)
                return;
            var condition = Load(predicate.Condition);
            var trueValue = Load(predicate.TrueValue);
            var falseValue = Load(predicate.FalseValue);

            var targetRegister = Allocate(predicate, trueValue.Kind);
            if (predicate.BasicValueType != BasicValueType.None)
            {
                using (var command = BeginCommand(
                    Instructions.GetSelectValueOperation(predicate.BasicValueType)))
                {
                    command.AppendArgument(targetRegister);
                    command.AppendArgument(trueValue);
                    command.AppendArgument(falseValue);
                    command.AppendArgument(condition);
                }
            }
            else
            {
                Move(trueValue, targetRegister, new PredicateConfiguration(condition, true));
                Move(falseValue, targetRegister, new PredicateConfiguration(condition, false));
            }
        }

        /// <summary cref="IValueVisitor.Visit(SelectPredicate)"/>
        public void Visit(SelectPredicate selectPredicate)
        {
            if (selectPredicate.IsHigherOrder)
                return;
            throw new NotSupportedException("Select predicates with value chains are currently not supported");
        }

        /// <summary cref="IValueVisitor.Visit(GenericAtomic)"/>
        public void Visit(GenericAtomic atomic)
        {
            var target = Load(atomic.Target);
            var value = Load(atomic.Value);

            var uses = Scope.GetUses(atomic);
            var requiresResult = !uses.HasExactlyOneMemoryRef || atomic.Kind == AtomicKind.Exchange;
            var atomicOperation = Instructions.GetAtomicOperation(
                atomic.Kind,
                requiresResult);
            var type = Instructions.GetAtomicOperationPostfix(
                atomic.Kind,
                atomic.ArithmeticBasicValueType);

            var ptxType = PTXType.GetPTXType(atomic.BasicValueType);
            var targetRegister = requiresResult ? Allocate(atomic, ptxType.RegisterKind) : default;
            using (var command = BeginCommand(atomicOperation))
            {
                command.AppendNonLocalAddressSpace(
                    (atomic.Target.Type as AddressSpaceType).AddressSpace);
                command.AppendPostFix(type);
                if (requiresResult)
                    command.AppendArgument(targetRegister);
                command.AppendArgumentValue(target);
                command.AppendArgument(value);
            }
        }

        /// <summary cref="IValueVisitor.Visit(AtomicCAS)"/>
        public void Visit(AtomicCAS atomicCAS)
        {
            var target = Load(atomicCAS.Target);
            var value = Load(atomicCAS.Value);
            var compare = Load(atomicCAS.CompareValue);

            var type = PTXType.GetPTXType(atomicCAS.BasicValueType);
            var targetRegister = Allocate(atomicCAS, type.RegisterKind);

            using (var command = BeginCommand(Instructions.AtomicCASOperation))
            {
                command.AppendNonLocalAddressSpace(
                    (atomicCAS.Target.Type as AddressSpaceType).AddressSpace);
                command.AppendPostFix(type);
                command.AppendArgument(targetRegister);
                command.AppendArgumentValue(target);
                command.AppendArgument(value);
                command.AppendArgument(compare);
            }
        }

        /// <summary cref="IValueVisitor.Visit(MemoryRef)"/>
        public void Visit(MemoryRef memoryRef)
        {
            Alias(memoryRef, memoryRef.Parent);
        }

        /// <summary cref="IValueVisitor.Visit(Alloca)"/>
        public void Visit(Alloca alloca)
        {
            // Ignore alloca
        }

        /// <summary cref="IValueVisitor.Visit(MemoryBarrier)"/>
        public void Visit(MemoryBarrier barrier)
        {
            var command = Instructions.GetMemoryBarrier(barrier.Kind);
            Command(command, null);
        }

        /// <summary cref="IValueVisitor.Visit(Load)"/>
        public void Visit(Load load)
        {
            var source = Load(load.Source);
            var sourceType = load.Source.Type as PointerType;

            var type = PTXType.GetPTXType(sourceType.ElementType, ABI);
            var targetRegister = Allocate(load, type.RegisterKind);

            using (var command = BeginCommand(Instructions.LoadOperation))
            {
                command.AppendAddressSpace(sourceType.AddressSpace);
                command.AppendPostFix(type);
                command.AppendArgument(targetRegister);
                command.AppendArgumentValue(source);
            }
        }

        /// <summary cref="IValueVisitor.Visit(Store)"/>
        public void Visit(Store store)
        {
            var target = Load(store.Target);
            var targetType = store.Target.Type as PointerType;
            var value = Load(store.Value);

            var type = PTXType.GetPTXType(targetType.ElementType, ABI);

            using (var command = BeginCommand(Instructions.StoreOperation))
            {
                command.AppendAddressSpace(targetType.AddressSpace);
                command.AppendPostFix(type);
                command.AppendArgumentValue(target);
                command.AppendArgument(value);
            }
        }

        /// <summary cref="IValueVisitor.Visit(SubViewValue)"/>
        public void Visit(SubViewValue value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(LoadElementAddress)"/>
        public void Visit(LoadElementAddress value)
        {
            if (!value.IsPointerAccess)
                throw new InvalidCodeGenerationException();

            var address = Load(value.Source);
            var sourceType = value.Source.Type as PointerType;
            var elementSize = ABI.GetSizeOf(sourceType.ElementType);
            var elementIndex = Load(value.ElementIndex);

            var offsetRegister = AllocatePlatformRegister(out PTXType _);
            using (var command = BeginCommand(
                Instructions.GetLEAMulOperation(ABI.PointerArithmeticType)))
            {
                command.AppendArgument(offsetRegister);
                command.AppendArgument(elementIndex);
                command.AppendConstant(elementSize);
            }

            Instructions.TryGetArithmeticOperation(
                BinaryArithmeticKind.Add,
                ABI.PointerArithmeticType,
                false,
                out string addCommand);
            var targetRegister = AllocatePlatformRegister(value, out PTXType _);
            using (var command = BeginCommand(addCommand))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(address);
                command.AppendArgument(offsetRegister);
            }

            FreeRegister(offsetRegister);
        }

        /// <summary cref="IValueVisitor.Visit(LoadFieldAddress)"/>
        public void Visit(LoadFieldAddress value)
        {
            var source = Load(value.Source);
            var fieldOffset = ABI.GetOffsetOf(value.StructureType, value.FieldIndex);

            if (fieldOffset != 0)
            {
                Instructions.TryGetArithmeticOperation(
                    BinaryArithmeticKind.Add,
                    ABI.PointerArithmeticType,
                    false,
                    out string addCommand);
                var targetRegister = AllocatePlatformRegister(value, out PTXType _);
                using (var command = BeginCommand(addCommand))
                {
                    command.AppendArgument(targetRegister);
                    command.AppendArgument(source);
                    command.AppendConstant(fieldOffset);
                }
            }
            else
                Alias(value, value.Source);
        }

        /// <summary cref="IValueVisitor.Visit(NewView)"/>
        public void Visit(NewView value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(GetViewLength)"/>
        public void Visit(GetViewLength value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(PrimitiveValue)"/>
        public void Visit(PrimitiveValue value)
        {
            var uses = Scope.GetUses(value);
            if (uses.TryGetSingleUse(out Use use) && use.Resolve() is Alloca)
                return;

            var basicValueType = value.BasicValueType;

            var type = PTXType.GetPTXType(basicValueType);
            var register = Allocate(value, type.RegisterKind);

            using (var command = BeginCommand(
                Instructions.MoveOperation,
                type))
            {
                command.AppendArgument(register);

                switch (basicValueType)
                {
                    case BasicValueType.Int1:
                    case BasicValueType.Int8:
                        command.AppendConstant(value.UInt8Value);
                        break;
                    case BasicValueType.Int16:
                        command.AppendConstant(value.UInt16Value);
                        break;
                    case BasicValueType.Int32:
                        command.AppendConstant(value.UInt32Value);
                        break;
                    case BasicValueType.Int64:
                        command.AppendConstant(value.UInt64Value);
                        break;
                    case BasicValueType.Float32:
                        command.AppendConstant(value.Float32Value);
                        break;
                    case BasicValueType.Float64:
                        command.AppendConstant(value.Float64Value);
                        break;
                    default:
                        throw new InvalidCodeGenerationException();
                }
            }
        }

        /// <summary cref="IValueVisitor.Visit(StringValue)"/>
        public void Visit(StringValue value)
        {
            if (!constants.TryGetValue(value, out (string, PTXRegister) entry) &&
                Scope.GetUses(value).HasAny)
            {
                var name = "__strconst" + constants.Count;
                var register = AllocatePlatformRegister(value, out PTXType postFix);
                constants.Add(value, (name, register));
                using (var command = BeginCommand(
                    Instructions.MoveOperation,
                    PTXType.GetPTXType(ABI.PointerArithmeticType)))
                {
                    command.AppendArgument(register);
                    command.AppendRawValueReference(name);
                }
            }
        }

        /// <summary cref="IValueVisitor.Visit(NullValue)"/>
        public void Visit(NullValue value)
        {
            if (!value.Type.IsPointerType)
                throw new InvalidCodeGenerationException();
            var register = AllocatePlatformRegister(value, out PTXType ptxType);
            // Move a constant zero pointer into the target register
            using (var command = BeginCommand(
                Instructions.MoveOperation,
                ptxType))
            {
                command.AppendArgument(register);
                command.AppendConstant(0);
            }
        }

        /// <summary cref="IValueVisitor.Visit(SizeOfValue)"/>
        public void Visit(SizeOfValue value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(UndefValue)"/>
        public void Visit(UndefValue value)
        {
            if (!value.Type.IsMemoryType)
                throw new InvalidCodeGenerationException();
        }

        /// <summary cref="IValueVisitor.Visit(GetField)"/>
        public void Visit(GetField value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(SetField)"/>
        public void Visit(SetField value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(GridDimensionValue)"/>
        public void Visit(GridDimensionValue value)
        {
            if (!emittedConstants.Add(value))
                return;
            var target = Allocate(value, PTXRegisterKind.Int32);
            Move(
                new PTXRegister(
                    PTXRegisterKind.NctaId,
                    (int)value.Dimension),
                target);
        }

        /// <summary cref="IValueVisitor.Visit(GroupDimensionValue)"/>
        public void Visit(GroupDimensionValue value)
        {
            if (!emittedConstants.Add(value))
                return;
            var target = Allocate(value, PTXRegisterKind.Int32);
            Move(
                new PTXRegister(
                    PTXRegisterKind.NtId,
                    (int)value.Dimension),
                target);
        }

        /// <summary cref="IValueVisitor.Visit(WarpSizeValue)"/>
        public void Visit(WarpSizeValue value) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(LaneIdxValue)"/>
        public void Visit(LaneIdxValue value)
        {
            if (!emittedConstants.Add(value))
                return;
            var target = Allocate(value, PTXRegisterKind.Int32);
            Move(
                new PTXRegister(PTXRegisterKind.LaneId, 0),
                target);
        }

        /// <summary cref="IValueVisitor.Visit(PredicateBarrier)"/>
        public void Visit(PredicateBarrier barrier)
        {
            var targetRegister = Allocate(barrier, PTXRegisterKind.Int32);
            var predicate = Load(barrier.Predicate);

            switch (barrier.Kind)
            {
                case PredicateBarrierKind.And:
                case PredicateBarrierKind.Or:
                    var targetPredicateRegister = AllocateRegister(PTXRegisterKind.Predicate);
                    using (var command = BeginCommand(
                        Instructions.GetPredicateBarrier(barrier.Kind)))
                    {
                        command.AppendArgument(targetPredicateRegister);
                        command.AppendConstant(0);
                        command.AppendArgument(predicate);
                    }
                    using (var command = BeginCommand(
                        Instructions.GetSelectValueOperation(BasicValueType.Int32)))
                    {
                        command.AppendArgument(targetRegister);
                        command.AppendConstant(1);
                        command.AppendConstant(0);
                        command.AppendArgument(targetPredicateRegister);
                    }
                    FreeRegister(targetPredicateRegister);
                    break;
                case PredicateBarrierKind.PopCount:
                    using (var command = BeginCommand(
                        Instructions.GetPredicateBarrier(barrier.Kind)))
                    {
                        command.AppendArgument(targetRegister);
                        command.AppendConstant(0);
                        command.AppendArgument(predicate);
                    }
                    break;
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        /// <summary cref="IValueVisitor.Visit(Barrier)"/>
        public void Visit(Barrier barrier)
        {
            using (var command = BeginCommand(
                Instructions.GetBarrier(barrier.Kind)))
            {
                switch (barrier.Kind)
                {
                    case BarrierKind.WarpLevel:
                        command.AppendConstant(Instructions.AllThreadsInAWarpMemberMask);
                        break;
                    case BarrierKind.GroupLevel:
                        command.AppendConstant(0);
                        break;
                    default:
                        throw new InvalidCodeGenerationException();
                }
            }
        }

        /// <summary cref="IValueVisitor.Visit(Shuffle)"/>
        public void Visit(Shuffle shuffle)
        {
            var ptxType = PTXType.GetPTXType(shuffle.Variable.BasicValueType);
            var targetRegister = Allocate(shuffle, ptxType.RegisterKind);

            var variable = Load(shuffle.Variable);
            var delta = Load(shuffle.Origin);

            var shuffleOperation = Instructions.GetShuffleOperation(shuffle.Kind);
            using (var command = BeginCommand(shuffleOperation))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(variable);
                command.AppendArgument(delta);

                if (shuffle.Kind == ShuffleKind.Up)
                    command.AppendConstant(0);
                else
                    command.AppendConstant(0x1f);

                command.AppendConstant(Instructions.AllThreadsInAWarpMemberMask);
            }
        }

        /// <summary cref="IValueVisitor.Visit(DebugAssertFailed)"/>
        public void Visit(DebugAssertFailed assert)
        {
            Debug.Assert(false, "Invalid assert node -> should have been removed");
        }

        /// <summary cref="IValueVisitor.Visit(DebugTrace)"/>
        public void Visit(DebugTrace trace)
        {
            Debug.Assert(false, "Invalid trace node -> should have been removed");
        }

        #endregion
    }
}
