// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLCodeGenerator.Values.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Diagnostics;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        /// <summary cref="IValueVisitor.Visit(MethodCall)"/>
        public void Visit(MethodCall methodCall)
        {
            var target = methodCall.Target;
            var returnType = target.ReturnType;

            StatementEmitter statementEmitter;
            if (!returnType.IsVoidType)
            {
                var returnValue = AllocateIntrinsic(methodCall);
                statementEmitter = BeginStatement(returnValue);
                statementEmitter.AppendCommand(GetMethodName(target));
            }
            else
                statementEmitter = BeginStatement(GetMethodName(target));

            // Append arguments
            statementEmitter.BeginArguments();
            foreach (var argument in methodCall)
            {
                var variable = Load(argument);
                if (variable is ViewVariable viewVariable)
                {
                    statementEmitter.AppendArgument(viewVariable.Pointer);
                    statementEmitter.AppendArgument(viewVariable.Length);
                }
                else
                    statementEmitter.AppendArgument(variable as IntrinsicVariable);
            }
            statementEmitter.EndArguments();

            // End call
            statementEmitter.Dispose();
        }

        /// <summary cref="IValueVisitor.Visit(Parameter)"/>
        public void Visit(Parameter parameter)
        {
            // Parameters are already assigned to variables
        }

        /// <summary cref="IValueVisitor.Visit(PhiValue)"/>
        public void Visit(PhiValue phiValue)
        {
            // Phi values are already assigned to variables
        }

        /// <summary cref="IValueVisitor.Visit(UnaryArithmeticValue)"/>
        public void Visit(UnaryArithmeticValue value)
        {
            var argument = LoadIntrinsic(value.Value);
            var target = AllocateIntrinsic(value);

            using (var statement = BeginStatement(target))
            {
                statement.AppendCast(value.BasicValueType);
                var operation = CLInstructions.GetArithmeticOperation(
                    value.Kind,
                    value.BasicValueType.IsFloat(),
                    out bool isFunction);

                if (isFunction)
                    statement.AppendCommand(operation);
                statement.BeginArguments();
                if (!isFunction)
                    statement.AppendCommand(operation);

                statement.AppendCast(value.ArithmeticBasicValueType);
                statement.AppendArgument(argument);
                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(BinaryArithmeticValue)"/>
        public void Visit(BinaryArithmeticValue value)
        {
            var left = LoadIntrinsic(value.Left);
            var right = LoadIntrinsic(value.Right);

            var target = AllocateIntrinsic(value);
            using (var statement = BeginStatement(target))
            {
                statement.AppendCast(value.BasicValueType);
                var operation = CLInstructions.GetArithmeticOperation(
                    value.Kind,
                    value.BasicValueType.IsFloat(),
                    out bool isFunction);

                if (isFunction)
                {
                    statement.AppendCommand(operation);
                    statement.BeginArguments();
                }
                else
                    statement.OpenParen();

                statement.AppendCast(value.ArithmeticBasicValueType);
                statement.AppendArgument(left);

                if (!isFunction)
                    statement.AppendCommand(operation);

                statement.AppendArgument();
                statement.AppendCast(value.ArithmeticBasicValueType);
                statement.Append(right);

                if (isFunction)
                    statement.EndArguments();
                else
                    statement.CloseParen();
            }
        }

        /// <summary cref="IValueVisitor.Visit(TernaryArithmeticValue)"/>
        public void Visit(TernaryArithmeticValue value)
        {
            if (!CLInstructions.TryGetArithmeticOperation(
                value.Kind,
                value.BasicValueType.IsFloat(),
                out string operation))
                throw new InvalidCodeGenerationException();

            var first = LoadIntrinsic(value.First);
            var second = LoadIntrinsic(value.Second);
            var third = LoadIntrinsic(value.Third);

            var targetRegister = AllocateIntrinsic(value);
            using (var statement = BeginStatement(targetRegister))
            {
                statement.AppendCast(value.BasicValueType);
                statement.AppendCommand(operation);
                statement.BeginArguments();

                statement.AppendArgument();
                statement.AppendCast(value.ArithmeticBasicValueType);
                statement.Append(first);

                statement.AppendArgument();
                statement.AppendCast(value.ArithmeticBasicValueType);
                statement.Append(second);

                statement.AppendArgument();
                statement.AppendCast(value.ArithmeticBasicValueType);
                statement.Append(third);

                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(CompareValue)"/>
        public void Visit(CompareValue value)
        {
            var left = LoadIntrinsic(value.Left);
            var right = LoadIntrinsic(value.Right);

            var targetRegister = AllocateIntrinsic(value);
            using (var statement = BeginStatement(targetRegister))
            {
                statement.AppendArgument(left);
                statement.AppendCommand(
                    CLInstructions.GetCompareOperation(
                        value.Kind));
                statement.AppendArgument(right);
            }
        }

        /// <summary cref="IValueVisitor.Visit(ConvertValue)"/>
        public void Visit(ConvertValue value)
        {
            var sourceValue = LoadIntrinsic(value.Value);

            var targetRegister = AllocateIntrinsic(value);
            using (var statement = BeginStatement(targetRegister))
            {
                statement.AppendCast(value.TargetType);
                statement.AppendArgument(sourceValue);
            }
        }

        /// <summary cref="IValueVisitor.Visit(PointerCast)"/>
        public void Visit(PointerCast value)
        {
            var sourceValue = LoadIntrinsic(value.Value);

            var targetRegister = AllocateIntrinsic(value);
            using (var statement = BeginStatement(targetRegister))
            {
                statement.AppendCast(value.TargetType);
                statement.AppendArgument(sourceValue);
            }
        }

        /// <summary cref="IValueVisitor.Visit(FloatAsIntCast)"/>
        public void Visit(FloatAsIntCast value)
        {
            var source = LoadIntrinsic(value.Value);
            var targetRegister = AllocateIntrinsic(value);

            using (var statement = BeginStatement(targetRegister))
            {
                statement.AppendCommand(CLInstructions.FloatAsInt);
                statement.BeginArguments();
                statement.AppendArgument(source);
                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(IntAsFloatCast)"/>
        public void Visit(IntAsFloatCast value)
        {
            var source = LoadIntrinsic(value.Value);
            var targetRegister = AllocateIntrinsic(value);

            using (var statement = BeginStatement(targetRegister))
            {
                statement.AppendCommand(CLInstructions.IntAsFloat);
                statement.BeginArguments();
                statement.AppendArgument(source);
                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(Predicate)"/>
        public void Visit(Predicate predicate)
        {
            var condition = LoadIntrinsic(predicate.Condition);
            var trueValue = LoadIntrinsic(predicate.TrueValue);
            var falseValue = LoadIntrinsic(predicate.FalseValue);

            var targetRegister = AllocateIntrinsic(predicate);
            using (var statement = BeginStatement(targetRegister))
            {
                statement.AppendArgument(condition);
                statement.AppendCommand(CLInstructions.SelectOperation1);
                statement.AppendArgument(trueValue);
                statement.AppendCommand(CLInstructions.SelectOperation2);
                statement.AppendArgument(falseValue);
            }
        }

        /// <summary cref="IValueVisitor.Visit(GenericAtomic)"/>
        public void Visit(GenericAtomic atomic)
        {
            var target = LoadIntrinsic(atomic.Target);
            var value = LoadIntrinsic(atomic.Value);

            var atomicOperation = CLInstructions.GetAtomicOperation(atomic.Kind);

            StatementEmitter statement;
            if (atomic.Kind == AtomicKind.Exchange)
            {
                var result = AllocateIntrinsic(atomic);
                statement = BeginStatement(result, atomicOperation);
            }
            else
                statement = BeginStatement(atomicOperation);

            statement.BeginArguments();
            statement.AppendAtomicCast(atomic.ArithmeticBasicValueType);
            statement.AppendArgument(target);
            statement.AppendArgument(value);
            statement.EndArguments();

            statement.Dispose();
        }

        /// <summary cref="IValueVisitor.Visit(AtomicCAS)"/>
        public void Visit(AtomicCAS atomicCAS)
        {
            var target = LoadIntrinsic(atomicCAS.Target);
            var value = LoadIntrinsic(atomicCAS.Value);
            var compare = LoadIntrinsic(atomicCAS.CompareValue);

            var tempVariable = AllocateType(BasicValueType.Int1) as PrimitiveVariable;
            var targetVariable = AllocateIntrinsic(atomicCAS);
            using (var statement = BeginStatement(tempVariable))
            {
                statement.AppendCommand(CLInstructions.AtomicCASOperation);
                statement.BeginArguments();
                statement.AppendAtomicCast(atomicCAS.ArithmeticBasicValueType);
                statement.AppendArgument(target);
                statement.AppendArgument(value);
                statement.AppendArgument(compare);
                statement.EndArguments();
            }

            // The OpenCL way is not compatible with the internal CAS semantic
            // We should adapt to the more general way of returning a bool in the future
            // For now, check the result of the operation and emit an atomic load
            // in the case of a failure.
            using (var statement = BeginStatement(targetVariable))
            {
                statement.Append(tempVariable);
                statement.AppendCommand(CLInstructions.SelectOperation1);
                statement.Append(value);
                statement.AppendCommand(CLInstructions.SelectOperation2);

                statement.AppendCommand(CLInstructions.AtomicLoadOperation);
                statement.BeginArguments();
                statement.AppendArgument(target);
                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(Alloca)"/>
        public void Visit(Alloca alloca)
        {
            // Ignore alloca
        }

        /// <summary cref="IValueVisitor.Visit(MemoryBarrier)"/>
        public void Visit(MemoryBarrier barrier)
        {
            var fenceFlags = CLInstructions.GetMemoryFenceFlags(true);
            var command = CLInstructions.GetMemoryBarrier(
                barrier.Kind,
                out string memoryScope);
            using (var statement = BeginStatement(command))
            {
                statement.BeginArguments();

                statement.AppendArgument();
                statement.AppendCommand(fenceFlags);

                statement.AppendArgument();
                statement.AppendCommand(memoryScope);

                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(Load)"/>
        public void Visit(Load load)
        {
            var address = LoadIntrinsic(load.Source);
            var target = AllocateIntrinsic(load);

            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(CLInstructions.DereferenceOperation);
                statement.AppendArgument(address);
            }
        }

        /// <summary cref="IValueVisitor.Visit(Store)"/>
        public void Visit(Store store)
        {
            var address = LoadIntrinsic(store.Target);
            var value = LoadIntrinsic(store.Value);

            using (var statement = BeginStatement(
                CLInstructions.DereferenceOperation))
            {
                statement.AppendArgument(address);
                statement.AppendCommand(CLInstructions.AssignmentOperation);
                statement.AppendArgument(value);
            }
        }

        /// <summary cref="IValueVisitor.Visit(LoadFieldAddress)"/>
        public void Visit(LoadFieldAddress value)
        {
            var source = LoadIntrinsic(value.Source);
            var target = AllocateIntrinsic(value);

            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(CLInstructions.AddressOfOperation);
                statement.AppendArgument(source);
                statement.AppendField(value.FieldIndex);
            }
        }

        /// <summary cref="IValueVisitor.Visit(PrimitiveValue)"/>
        public void Visit(PrimitiveValue value)
        {
            if (value.Uses.TryGetSingleUse(out Use use) && use.Resolve() is Alloca)
                return;

            var variable = AllocateIntrinsic(value);
            using (var statement = BeginStatement(variable))
            {
                switch (value.BasicValueType)
                {
                    case BasicValueType.Int1:
                        statement.AppendConstant(value.Int1Value ? 1 : 0);
                        break;
                    case BasicValueType.Int8:
                        statement.AppendConstant(value.UInt8Value);
                        break;
                    case BasicValueType.Int16:
                        statement.AppendConstant(value.UInt16Value);
                        break;
                    case BasicValueType.Int32:
                        statement.AppendConstant(value.UInt32Value);
                        break;
                    case BasicValueType.Int64:
                        statement.AppendConstant(value.UInt64Value);
                        break;
                    case BasicValueType.Float32:
                        statement.AppendConstant(value.Float32Value);
                        break;
                    case BasicValueType.Float64:
                        statement.AppendConstant(value.Float64Value);
                        break;
                    default:
                        throw new InvalidCodeGenerationException();
                }
            }
        }

        /// <summary cref="IValueVisitor.Visit(StringValue)"/>
        public void Visit(StringValue value)
        {
            // Ignore string values for now
        }

        /// <summary cref="IValueVisitor.Visit(NullValue)"/>
        public void Visit(NullValue value)
        {
            switch (value.Type)
            {
                case VoidType _:
                    // Ignore void type nulls
                    break;
                case ViewType viewType:
                    MakeNullView(value, viewType);
                    break;
                default:
                    var target = AllocateIntrinsic(value);
                    using (var statement = BeginStatement(target))
                    {
                        statement.AppendCast(value.Type);
                        statement.AppendConstant(0);
                    }
                    break;
            }
        }

        /// <summary cref="IValueVisitor.Visit(SizeOfValue)"/>
        public void Visit(SizeOfValue value)
        {
            var target = AllocateIntrinsic(value);
            var size = ABI.GetSizeOf(value.TargetType);
            using (var statement = BeginStatement(target))
            {
                statement.AppendConstant(size);
            }
        }

        /// <summary cref="IValueVisitor.Visit(GetField)"/>
        public void Visit(GetField value)
        {
            var source = LoadIntrinsic(value.ObjectValue);
            var target = AllocateIntrinsic(value);
            using (var statement = BeginStatement(target))
            {
                statement.AppendArgument(source);
                statement.AppendField(value.FieldIndex);
            }
        }

        /// <summary cref="IValueVisitor.Visit(SetField)"/>
        public void Visit(SetField value)
        {
            var source = LoadIntrinsic(value.ObjectValue);
            var set = LoadIntrinsic(value.Value);
            var target = AllocateIntrinsic(value);

            // Copy value
            using (var statement = BeginStatement(target))
                statement.AppendArgument(source);

            // Update field value
            using (var statement = BeginStatement(target, value.FieldIndex))
            {
                statement.AppendCommand(" = ");
                statement.AppendArgument(set);
            }
        }

        /// <summary cref="IValueVisitor.Visit(GetElement)"/>
        public void Visit(GetElement value)
        {
            var source = LoadIntrinsic(value.ObjectValue);
            var index = LoadIntrinsic(value.Index);
            var target = AllocateIntrinsic(value);

            using (var statement = BeginStatement(target))
            {
                statement.AppendArgument(source);
                statement.AppendIndexer(index);
            }
        }

        /// <summary cref="IValueVisitor.Visit(SetElement)"/>
        public void Visit(SetElement value)
        {
            var source = LoadIntrinsic(value.ObjectValue);
            var index = LoadIntrinsic(value.Index);
            var set = LoadIntrinsic(value.Value);
            var target = AllocateIntrinsic(value);

            // Copy value
            using (var statement = BeginStatement(target))
                statement.AppendArgument(source);

            // Update array value
            using (var statement = BeginStatement(target, index))
            {
                statement.AppendCommand(CLInstructions.AssignmentOperation);
                statement.AppendArgument(set);
            }
        }

        private void MakeIntrinsicValue(
            Value value,
            string operation,
            string args = null)
        {
            var target = AllocateIntrinsic(value);
            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(operation);
                if (args != null)
                {
                    statement.BeginArguments();
                    statement.AppendCommand(args);
                    statement.EndArguments();
                }
            }
        }

        private void MakeIntrinsicValue(
            Value value,
            string operation,
            DeviceConstantDimension3D dimension) =>
            MakeIntrinsicValue(
                value,
                operation,
                ((int)dimension).ToString());

        /// <summary cref="IValueVisitor.Visit(GridIndexValue)"/>
        public void Visit(GridIndexValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGridIndex,
                value.Dimension);

        /// <summary cref="IValueVisitor.Visit(GroupIndexValue)"/>
        public void Visit(GroupIndexValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGroupIndex,
                value.Dimension);

        /// <summary cref="IValueVisitor.Visit(GridDimensionValue)"/>
        public void Visit(GridDimensionValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGridSize,
                value.Dimension);

        /// <summary cref="IValueVisitor.Visit(GroupDimensionValue)"/>
        public void Visit(GroupDimensionValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGridSize,
                value.Dimension);

        /// <summary cref="IValueVisitor.Visit(WarpSizeValue)"/>
        public void Visit(WarpSizeValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetWarpSize);

        /// <summary cref="IValueVisitor.Visit(LaneIdxValue)"/>
        public void Visit(LaneIdxValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetLaneIndexOperation);

        /// <summary cref="IValueVisitor.Visit(PredicateBarrier)"/>
        public void Visit(PredicateBarrier barrier)
        {
            var sourcePredicate = LoadIntrinsic(barrier.Predicate);
            var target = AllocateIntrinsic(barrier);

            if (!CLInstructions.TryGetPredicateBarrier(
                barrier.Kind,
                out string operation))
                throw new InvalidCodeGenerationException();

            using (var statement = BeginStatement(target))
            {
                statement.AppendCast(BasicValueType.Int1);
                statement.AppendCommand(operation);
                statement.BeginArguments();
                statement.AppendCast(BasicValueType.Int32);
                statement.AppendArgument(sourcePredicate);
                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(Barrier)"/>
        public void Visit(Barrier barrier)
        {
            using (var statement = BeginStatement(
                CLInstructions.GetBarrier(barrier.Kind))) { }
        }

        /// <summary cref="IValueVisitor.Visit(Broadcast)"/>
        public void Visit(Broadcast broadcast)
        {
            var source = LoadIntrinsic(broadcast.Variable);
            var origin = LoadIntrinsic(broadcast.Origin);
            var target = AllocateIntrinsic(broadcast);

            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(
                    CLInstructions.GetBroadcastOperation(
                        broadcast.Kind));
                statement.BeginArguments();
                statement.AppendArgument(source);
                statement.AppendArgument(origin);
                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(WarpShuffle)"/>
        public void Visit(WarpShuffle shuffle)
        {
            if (!CLInstructions.TryGetShuffleOperation(
                Backend.Vendor,
                shuffle.Kind,
                out string operation))
                throw new InvalidCodeGenerationException();

            var source = LoadIntrinsic(shuffle.Variable);
            var origin = LoadIntrinsic(shuffle.Origin);
            var target = AllocateIntrinsic(shuffle);
            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(operation);
                statement.BeginArguments();

                statement.AppendArgument(source);
                // TODO: create a generic version that does not need this switch
                switch (shuffle.Kind)
                {
                    case ShuffleKind.Down:
                    case ShuffleKind.Up:
                        statement.AppendArgument(source);
                        break;
                }
                statement.AppendArgument(origin);

                statement.EndArguments();
            }
        }

        /// <summary cref="IValueVisitor.Visit(SubWarpShuffle)"/>
        public void Visit(SubWarpShuffle shuffle) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(UndefinedValue)"/>
        public void Visit(UndefinedValue undefined) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(HandleValue)"/>
        public void Visit(HandleValue handle) => throw new InvalidCodeGenerationException();

        /// <summary cref="IValueVisitor.Visit(DebugOperation)"/>
        public void Visit(DebugOperation debug)
        {
            Debug.Assert(false, "Invalid debug node -> should have been removed");
        }
    }
}
