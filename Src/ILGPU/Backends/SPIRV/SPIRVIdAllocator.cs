using ILGPU.IR.Types;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// Represents the type of an id.
    /// </summary>
    /// <remarks>
    /// See https://www.khronos.org/registry/spir-v/specs/unified1/SPIRV.html#_types
    /// for documentation
    /// </remarks>
    public enum SPIRVIdKind
    {
        BoolType,
        IntType,
        FloatType,
        NumericalType,
        Scalar,
        Vector,
        Matrix,
        Array,
        Structure,
        Aggregate,
        Composite,
        Image,
        Sampler,
        SampledImage,
        PhysicalPointerType,
        LogicalPointerType,
        ConcreteType,
        AbstractType,
        OpaqueType,
        VariablePointer
    }

    public class SPIRVIdAllocator : RegisterAllocator<SPIRVIdKind>
    {
        #region Instance

        /// <summary>
        ///
        /// </summary>
        /// <param name="backend"></param>
        public SPIRVIdAllocator(Backend backend) : base(backend)
        {
            Backend = backend;
        }

        // I'm leaving this as a dictionary because I feel it's more readable but
        // if there are performance concerns it can be switched out for an ImmutableArray
        // like the PTX allocator.
        private Dictionary<BasicValueType, SPIRVIdKind> BasicValueTypeToIdKindMapping =
            new Dictionary<BasicValueType, SPIRVIdKind> { };

        #endregion

        #region Properties

        public Backend Backend { get; }

        #endregion

        #region Methods



        protected override RegisterDescription ResolveRegisterDescription(TypeNode type)
            => throw new NotImplementedException();

        public override HardwareRegister AllocateRegister(RegisterDescription description)
            => throw new NotImplementedException();

        public override void FreeRegister(HardwareRegister hardwareRegister) =>
            throw new NotImplementedException();

        #endregion
    }
}
