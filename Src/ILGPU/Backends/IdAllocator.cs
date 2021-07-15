using ILGPU.IR;
using ILGPU.IR.Types;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ILGPU.Backends
{
    /// <summary>
    /// An allocator which really deals with more than just variables but
    /// instructions in general in terms of ids.
    /// </summary>
    /// <typeparam name="TKind">
    /// An enum describing the kinds (or different types) of ids.
    /// </typeparam>
    public abstract class IdAllocator<TKind> where TKind : struct
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
            /// <param name="labelKind">The type used for labels</param>
            public TypeContext(Dictionary<BasicValueType, TKind> mapping,
                TKind labelKind,
                TKind typeKind,
                TKind functionKind)
            {
                BasicValueTypeMapping = mapping;
                LabelKind = labelKind;
                TypeKind = typeKind;
                FunctionKind = functionKind;
            }

            public Dictionary<BasicValueType, TKind> BasicValueTypeMapping { get; }

            public TKind LabelKind { get; }

            public TKind TypeKind { get; }

            public TKind FunctionKind { get; }
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

        private readonly Dictionary<Node, IdVariable> lookup =
            new Dictionary<Node, IdVariable>();

        private int idCounter = 0;

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
        /// <param name="node">The value to allocate.</param>
        /// <param name="kind">The kind this variable should be.</param>
        /// <returns>The allocated variable.</returns>
        public IdVariable Allocate(Node node, TKind kind)
        {
            if (lookup.TryGetValue(node, out IdVariable variable))
                return variable;
            variable = new IdVariable((uint) Interlocked.Increment(ref idCounter), kind);
            lookup.Add(node, variable);
            return variable;
        }

        /// <summary>
        /// "Allocates" a function (stores the id for it)
        /// </summary>
        /// <param name="method">The method to "allocate"</param>
        /// <returns>The id variable referring to they type</returns>
        public IdVariable Allocate(Method method) =>
            Allocate(method, typeContext.FunctionKind);

        /// <summary>
        /// Creates a label variable to refer to the given block.
        /// </summary>
        /// <param name="block">The block to declare (add a label to) </param>
        /// <returns>The label variable.</returns>
        public IdVariable Allocate(BasicBlock block) =>
            Allocate(block, typeContext.LabelKind);

        /// <summary>
        /// "Allocates" a type (stores the id for it)
        /// </summary>
        /// <param name="type">The type to "allocate"</param>
        /// <returns>The id variable referring to they type</returns>
        public IdVariable Allocate(TypeNode type) => Allocate(type, typeContext.TypeKind);

        /// <summary>
        /// Loads the given node.
        /// </summary>
        /// <param name="node">The node to load.</param>
        /// <returns>The loaded variable.</returns>
        public IdVariable Load(Node node) =>
            lookup[node];

        // TODO: A better solution than this?
        public IEnumerable<TypeNode> GetTypeNodes() =>
            (IEnumerable<TypeNode>)lookup.Where(pair => pair.Key is TypeNode)
                .Select(pair => pair.Key);

        #endregion
    }
}
