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
using System.Collections.Immutable;

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

            // Declare view
            Declare(target);

            // Assign pointer
            using (var statement = BeginStatement(target, target.PointerFieldIndex))
                statement.Append(pointer);

            // Assign length
            using (var statement = BeginStatement(target, target.LengthFieldIndex))
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
        /// <param name="viewType">The view type.</param>
        /// <param name="accessChain">The access chain.</param>
        private void MakeNullView(Variable value, ViewType viewType, ImmutableArray<int> accessChain)
        {
            using (var statement = BeginStatement(
                value,
                accessChain.Add(CLTypeGenerator.ViewPointerFieldIndex)))
            {
                statement.AppendPointerCast(TypeGenerator[viewType.ElementType]);
                statement.AppendConstant(0);
            }

            using (var statement = BeginStatement(
                value,
                accessChain.Add(CLTypeGenerator.ViewLengthFieldIndex)))
                statement.AppendConstant(0);
        }

        /// <summary cref="IValueVisitor.Visit(ViewCast)"/>
        public void Visit(ViewCast value)
        {
            var target = AllocateView(value);
            var source = LoadView(value.Value);

            // Declare view
            Declare(target);

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

            // Declare view
            Declare(target);

            // Assign pointer
            using (var statement = BeginStatement(target, target.PointerFieldIndex))
            {
                statement.AppendCommand(CLInstructions.AddressOfOperation);
                statement.Append(source);
                statement.AppendField(target.PointerFieldIndex);
                statement.AppendIndexer(offset);
            }

            // Assign length
            using (var statement = BeginStatement(target, target.LengthFieldIndex))
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

        /// <summary cref="IValueVisitor.Visit(AddressSpaceCast)"/>
        public void Visit(AddressSpaceCast value)
        {
            var targetType = value.TargetType as AddressSpaceType;
            var source = Load(value.Value);
            var target = Allocate(value);

            bool isOperation = CLInstructions.TryGetAddressSpaceCast(
                value.TargetAddressSpace,
                out string operation);

            void GeneratePointerCast(StatementEmitter statement)
            {
                if (isOperation)
                {
                    // There is a specific cast operation
                    statement.AppendCommand(operation);
                    statement.BeginArguments();
                    statement.Append(source);
                }
                else
                    statement.AppendPointerCast(TypeGenerator[targetType.ElementType]);
                statement.Append(source);
            }

            if (value.IsPointerCast)
            {
                using (var statement = BeginStatement(target))
                {
                    GeneratePointerCast(statement);
                    if (isOperation)
                        statement.EndArguments();
                }
            }
            else
            {
                var targetView = target as ViewImplementationVariable;
                Declare(target);

                // Assign pointer
                using (var statement = BeginStatement(target, targetView.PointerFieldIndex))
                {
                    GeneratePointerCast(statement);
                    statement.AppendField(targetView.PointerFieldIndex);
                    if (isOperation)
                        statement.EndArguments();
                }

                // Assign length
                using (var statement = BeginStatement(target, targetView.LengthFieldIndex))
                {
                    statement.Append(source);
                    statement.AppendField(targetView.LengthFieldIndex);
                }
            }
        }
    }
}
