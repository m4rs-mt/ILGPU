namespace ILGPU.IR
{
    public record struct IRType(NodeId Id, IRType.Classifier Class, NodeId[] Nodes, BasicValueType BasicValueType, long Data)
    {
        public enum Classifier
        {
            Void,
            String,
            Primitive,
            Pointer,
            View,
            Array,
            Structure,
            Unknown,
        }
    }
}
