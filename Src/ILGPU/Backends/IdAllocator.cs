using ILGPU.IR;
using System.Collections.Generic;

namespace ILGPU.Backends
{
    /// <summary>
    /// An allocator which really deals with more than just variables but
    /// instructions in general in terms of ids.
    /// </summary>
    /// <typeparam name="TKind">
    /// An enum describing the kinds (or different types) of ids.
    /// </typeparam>
    public abstract class IdAllocator<TKind> where TKind: struct
    {
        #region Nested Types

        /// <summary>
        /// A variable with a unique id that can be assigned only once.
        /// </summary>
        public class IdVariable
        {
            internal IdVariable(uint id, TKind kind)
            {
                Id = id;
                Kind = kind;
            }

            /// <summary>
            /// The unique id of this variable.
            /// </summary>
            public uint Id { get; }

            /// <summary>
            /// The kind of this variable.
            /// </summary>
            public TKind Kind { get; }

            /// <summary>
            /// Converts this <see cref="IdVariable"/> IdVariable to
            /// just its <see cref="IdVariable.Id"/>
            /// </summary>
            /// <param name="variable">The variable to convert.</param>
            /// <returns>The variable's id.</returns>
            public static implicit operator uint(IdVariable variable) => variable.Id;
        }

        /// <summary>
        ///
        /// </summary>
        public struct TypeContext
        {
            /// <summary>
            /// Creates a new type context.
            /// </summary>
            /// <param name="mapping">
            /// The mapping from <see cref="BasicValueType"/> to <see cref="TKind"/>
            /// </param>
            /// <param name="labelType">The type used for labels</param>
            public TypeContext(Dictionary<BasicValueType, TKind> mapping, TKind labelType)
            {
                BasicValueTypeMapping = mapping;
                LabelType = labelType;
            }

            public Dictionary<BasicValueType, TKind> BasicValueTypeMapping { get; }

            public TKind LabelType { get; }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new IdAllocator with the given <see cref="TypeContext"/>
        /// </summary>
        /// <param name="context">The type context to use.</param>
        public IdAllocator(TypeContext context)
        {
            typeContext = context;
        }

        private readonly Dictionary<NodeId, IdVariable> lookup =
            new Dictionary<NodeId, IdVariable>();

        private uint idCounter = 0;

        private readonly TypeContext typeContext;

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a new variable.
        /// </summary>
        /// <param name="value">The value to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public IdVariable Allocate(Value value) => Allocate(value,
            typeContext.BasicValueTypeMapping[value.BasicValueType]);

        /// <summary>
        /// Allocates a new variable with the specified kind.
        /// </summary>
        /// <param name="value">The value to allocate.</param>
        /// <param name="kind">The kind this variable should be.</param>
        /// <returns>The allocated variable.</returns>
        public IdVariable Allocate(Value value, TKind kind)
        {
            if (lookup.TryGetValue(value.Id, out IdVariable variable))
                return variable;
            variable = new IdVariable(idCounter, kind);
            idCounter++;
            lookup.Add(value.Id, variable);
            return variable;
        }

        /// <summary>
        /// Creates a label variable to refer to the given block.
        /// </summary>
        /// <param name="block">The block to declare (add a label to) </param>
        /// <returns>The label variable.</returns>
        public IdVariable DeclareBlock(BasicBlock block)
        {
            if (lookup.TryGetValue(block.Id, out IdVariable variable))
                return variable;
            variable = new IdVariable(idCounter, typeContext.LabelType);
            idCounter++;
            lookup.Add(block.Id, variable);
            return variable;
        }

        /// <summary>
        /// Loads the given value.
        /// </summary>
        /// <param name="value">The value to load.</param>
        /// <returns>The loaded variable.</returns>
        public IdVariable Load(Value value) =>
            lookup[value.Id];

        /// <summary>
        /// Loads the label for the given block.
        /// </summary>
        /// <param name="block">The block to load.</param>
        /// <returns>The loaded label variable.</returns>
        public IdVariable Load(BasicBlock block) =>
            lookup[block.Id];

        #endregion
    }
}
