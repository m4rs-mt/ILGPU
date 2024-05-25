using ILGPU.IR.Types;
using System.Collections.Generic;

namespace ILGPU.IR.Serialization
{
    /// <summary>
    /// Wraps several tables containing the information
    /// necessary to deserialize an <see cref="IRContext"/> instance.
    /// </summary>
    public interface IIRMappingContext
    {
        /// <summary>
        /// Returns a static mapping of all discovered IR methods.
        /// </summary>
        IReadOnlyDictionary<long, Method> Methods { get; }

        /// <summary>
        /// Returns a static mapping of all discovered IR blocks.
        /// </summary>
        IReadOnlyDictionary<long, BasicBlock> Blocks { get; }

        /// <summary>
        /// Returns a static mapping of all discovered IR types.
        /// </summary>
        IReadOnlyDictionary<long, TypeNode> Types { get; }

        /// <summary>
        /// Returns a dynamic mapping of all currently deserialized IR values.
        /// </summary>
        IDictionary<long, Value> Values { get; }
    }
}
