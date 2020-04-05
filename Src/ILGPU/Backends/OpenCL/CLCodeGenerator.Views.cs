// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
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
        /// <summary cref="IBackendCodeGenerator.GenerateCode(LoadElementAddress)"/>
        public void GenerateCode(LoadElementAddress value)
        {
            var elementIndex = LoadAs<PrimitiveVariable>(value.ElementIndex);
            var source = Load(value.Source);
            var target = AllocatePointerType(value.Type as PointerType);

            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(CLInstructions.AddressOfOperation);
                statement.Append(source);
                statement.AppendIndexer(elementIndex);
            }

            Bind(value, target);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(AddressSpaceCast)"/>
        public void GenerateCode(AddressSpaceCast value)
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

            using (var statement = BeginStatement(target))
            {
                GeneratePointerCast(statement);
                if (isOperation)
                    statement.EndArguments();
            }
        }
    }
}
