using ILGPU.IR;
using System.Collections.Generic;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// Represents the type of a SPIR-V id.
    /// </summary>
    /// <remarks>
    /// This is based on ILGPU's needs only and will not
    /// necessarily line up with the official SPIR-V type specification.
    /// </remarks>
    public enum SPIRVIdKind
    {
        Bool,
        Int,
        Float,
        Label
    }

    public class SPIRVIdAllocator : IdAllocator<SPIRVIdKind>
    {
        #region Instance

        // I'm leaving this as a dictionary because I feel it's more readable but
        // if there are performance concerns it can be switched out for an ImmutableArray
        // like the PTX allocator.
        private static Dictionary<BasicValueType, SPIRVIdKind> basicValueTypeToIdKindMapping =
            new Dictionary<BasicValueType, SPIRVIdKind>
            {
                [BasicValueType.Int1] = SPIRVIdKind.Int,
                [BasicValueType.Int8] = SPIRVIdKind.Int,
                [BasicValueType.Int16] = SPIRVIdKind.Int,
                [BasicValueType.Int32] = SPIRVIdKind.Int,
                [BasicValueType.Int64] = SPIRVIdKind.Int,
                [BasicValueType.Float16] = SPIRVIdKind.Float,
                [BasicValueType.Float32] = SPIRVIdKind.Float,
                [BasicValueType.Float64] = SPIRVIdKind.Float,
            };

        #endregion

        /// <summary>
        /// Creates a new SPIRVIdAllocator.
        /// </summary>
        public SPIRVIdAllocator()
            : base(new TypeContext(basicValueTypeToIdKindMapping, SPIRVIdKind.Label))
        {

        }
    }
}
