using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;

namespace ILGPU.IR
{
    public record struct IRValue(NodeId Method, NodeId BasicBlock, NodeId Id, ValueKind ValueKind, NodeId Type, NodeId[] Nodes, long Data, string? Tag)
    {
    }
}
