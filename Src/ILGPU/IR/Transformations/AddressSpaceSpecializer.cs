// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AddressSpaceSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using static ILGPU.IR.Types.AddressSpaceType;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Specializes address spaces of all methods. All methods marked with
    /// <see cref="MethodFlags.EntryPoint"/> will be considered to receive pointers
    /// to the specified <see cref="MemoryAddressSpace"/>.
    /// </summary>
    public sealed class AddressSpaceSpecializer :
        UnorderedTransformation<PointerAddressSpaces>
    {
        #region Static

        /// <summary>
        /// Specializes an address-space dependent parameter.
        /// </summary>
        /// <param name="methodBuilder">The target method builder.</param>
        /// <param name="builder">The entry block builder.</param>
        /// <param name="typeConverter">The type converter to use.</param>
        /// <param name="parameter">The source parameter.</param>
        /// <returns>True, if the given parameter was specialized.</returns>
        private static bool SpecializeParameter(
            Method.Builder methodBuilder,
            BasicBlock.Builder builder,
            AddressSpaceConverter typeConverter,
            Parameter parameter)
        {
            var converted = typeConverter.ConvertType(
                builder,
                parameter.Type);

            var location = parameter.Location;
            var targetParam = methodBuilder.AddParameter(converted, parameter.Name);

            if (converted == parameter.Type)
            {
                parameter.Replace(targetParam);
                return false;
            }

            Value convertedValue;
            if (converted is AddressSpaceType)
            {
                convertedValue = builder.CreateAddressSpaceCast(
                    location,
                    targetParam,
                    (parameter.Type as IAddressSpaceType).AddressSpace);
            }
            else 
            {
                var structureType = parameter.Type.As<StructureType>(location);
                var structureBuilder = builder.CreateStructure(
                    location,
                    structureType);

                var targetType = converted.As<StructureType>(location);
                foreach (var (fieldType, access) in structureType)
                {
                    var field = builder.CreateGetField(
                        location,
                        targetParam,
                        access);
                    if (fieldType == targetType[access])
                    {
                        structureBuilder.Add(field);
                    }
                    else
                    {
                        structureBuilder.Add(
                            builder.CreateAddressSpaceCast(
                                location,
                                field,
                                (fieldType as IAddressSpaceType).AddressSpace));
                    }
                }
                convertedValue = structureBuilder.Seal();
            }

            parameter.Replace(convertedValue);
            return true;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address space specialization transformation.
        /// </summary>
        /// <param name="kernelAddressSpace">
        /// The root address space of all kernel functions.
        /// </param>
        public AddressSpaceSpecializer(MemoryAddressSpace kernelAddressSpace)
        {
            KernelAddressSpace = kernelAddressSpace;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the kernel address space.
        /// </summary>
        public MemoryAddressSpace KernelAddressSpace { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="PointerAddressSpaces"/> analysis based on the main
        /// entry0point method.
        /// </summary>
        protected override PointerAddressSpaces CreateIntermediate<TPredicate>(
            in MethodCollection<TPredicate> methods)
        {
            // Get the main entry point method
            foreach (var method in methods)
            {
                if (method.HasFlags(MethodFlags.EntryPoint))
                {
                    return PointerAddressSpaces.Create(
                        method,
                        KernelAddressSpace);
                }
            }

            // We could not find any entry point
            return null;
        }

        /// <summary>
        /// Applies the address space specialization transformation.
        /// </summary>
        protected override bool PerformTransformation(
            Method.Builder builder,
            PointerAddressSpaces addressSpaces)
        {
            if (addressSpaces is null)
                return false;

            // Initialize the main converted and the entry block builder
            var entryBuilder = builder.EntryBlockBuilder;
            entryBuilder.SetupInsertPositionToStart();

            // Specialize all parameters
            bool applied = false;
            for (int i = 0, e = builder.NumParams; i < e; ++i)
            {
                // Query address-space information from the analysis
                var parameter = builder[i];
                var addressSpace = addressSpaces[parameter].UnifiedAddressSpace;
                var typeConverter = new AddressSpaceConverter(addressSpace);
                applied |= SpecializeParameter(
                    builder,
                    entryBuilder,
                    typeConverter,
                    parameter);
            }

            return applied;
        }

        /// <summary>
        /// Performs no operation.
        /// </summary>
        protected override void FinishProcessing(PointerAddressSpaces intermediate) { }

        #endregion
    }
}
