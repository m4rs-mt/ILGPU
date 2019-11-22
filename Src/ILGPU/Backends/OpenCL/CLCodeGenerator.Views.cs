// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLCodeGenerator.Views.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        /// <summary cref="IValueVisitor.Visit(NewView)"/>
        public void Visit(NewView value)
        {
            var target = AllocateView(value);
            var pointer = LoadAs<PointerVariable>(value.Pointer);
            var length = LoadAs<PrimitiveVariable>(value.Length);

            // Assign pointer
            using (var statement = BeginStatement(target, target.PointerFieldIndex))
                statement.Append(pointer);

            // Assign length
            using (var statement = BeginStatement(target, target.VariableName))
                statement.Append(length);
        }

        /// <summary cref="IValueVisitor.Visit(GetViewLength)"/>
        public void Visit(GetViewLength value)
        {
            var target = Allocate(value);
            var viewSource = LoadView(value.View);
            using (var statement = BeginStatement(target))
            {
                statement.Append(viewSource);
                statement.AppendField(viewSource.LengthFieldIndex);
            }
        }

        /// <summary>
        /// Creates a new empty view.
        /// </summary>
        /// <param name="value">The source value.</param>
        private void MakeNullView(NullValue value)
        {
            var target = AllocateView(value);

            using (var statement = BeginStatement(target, target.PointerFieldIndex))
                statement.AppendConstant(0);

            using (var statement = BeginStatement(target, target.LengthFieldIndex))
                statement.AppendConstant(0);
        }

        /// <summary cref="IValueVisitor.Visit(ViewCast)"/>
        public void Visit(ViewCast value)
        {
            var target = AllocateView(value);
            var source = LoadView(value.Value);

            using (var statement = BeginStatement(target, target.PointerFieldIndex))
            {
                statement.AppendPointerCast(TypeGenerator[target.ElementType]);
                statement.Append(source);
                statement.AppendField(source.PointerFieldIndex);
            }

            var sourceElementSize = ABI.GetSizeOf(value.SourceElementType);
            var targetElementSize = ABI.GetSizeOf(value.TargetElementType);

            // var newLength = length * sourceElementSize / targetElementSize;
            using (var statement = BeginStatement(target, target.LengthFieldIndex))
            {
                statement.OpenParen();
                statement.Append(source);
                statement.AppendField(source.LengthFieldIndex);
                statement.AppendOperation(
                    CLInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Mul,
                        false,
                        out bool _));
                statement.AppendConstant(sourceElementSize);
                statement.CloseParen();
                statement.AppendOperation(
                    CLInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Div,
                        false,
                        out bool _));
                statement.AppendConstant(targetElementSize);
            }
        }

        /// <summary cref="IValueVisitor.Visit(SubViewValue)"/>
        public void Visit(SubViewValue value)
        {
            var source = LoadView(value.Source);
            var offset = LoadAs<PrimitiveVariable>(value.Offset);
            var length = LoadAs<PrimitiveVariable>(value.Length);
            var target = AllocateView(value);

            // Assign pointer
            using (var statement = BeginStatement(target, target.PointerFieldIndex))
            {
                statement.AppendCommand(CLInstructions.AddressOfOperation);
                statement.Append(source);
                statement.TryAppendViewPointerField(source);
                statement.AppendIndexer(offset);
            }

            // Assign length
            using (var statement = BeginStatement(target, target.VariableName))
                statement.Append(length);
        }

        /// <summary cref="IValueVisitor.Visit(LoadElementAddress)"/>
        public void Visit(LoadElementAddress value)
        {
            var pointerType = value.Type as PointerType;
            var elementIndex = LoadAs<PrimitiveVariable>(value.ElementIndex);
            var source = Load(value.Source);
            var target = AllocatePointerType(pointerType.ElementType, pointerType.AddressSpace);

            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(CLInstructions.AddressOfOperation);
                statement.Append(source);
                statement.TryAppendViewPointerField(source);
                statement.AppendIndexer(elementIndex);
            }

            Bind(value, target);
        }

        /// <summary>
        /// Creates a set of operations to realize a generic lea operation.
        /// </summary>
        /// <param name="statement">The statement emitter.</param>
        /// <param name="elementIndex">The current element index (the offset).</param>
        /// <param name="address">The source address.</param>
        private static void MakeLoadElementAddress(
            ref StatementEmitter statement,
            PrimitiveVariable elementIndex,
            PointerVariable address)
        {
            statement.AppendCommand(CLInstructions.AddressOfOperation);
            statement.Append(address);
            statement.AppendIndexer(elementIndex);
        }

        /// <summary cref="IValueVisitor.Visit(AddressSpaceCast)"/>
        public void Visit(AddressSpaceCast value)
        {
            var targetType = value.TargetType as AddressSpaceType;
            var source = Load(value.Value);
            var target = Allocate(value);

            using (var statement = BeginStatement(target))
            {
                bool isOperation;
                if (isOperation = CLInstructions.TryGetAddressSpaceCast(
                    value.TargetAddressSpace,
                    out string operation))
                {
                    // There is a specific cast operation
                    statement.AppendCommand(operation);
                    statement.BeginArguments();
                    statement.Append(source);
                }
                else
                {
                    statement.AppendPointerCast(TypeGenerator[targetType.ElementType]);
                }
                statement.TryAppendViewPointerField(source);

                if (isOperation)
                    statement.EndArguments();
            }
        }
    }
}
