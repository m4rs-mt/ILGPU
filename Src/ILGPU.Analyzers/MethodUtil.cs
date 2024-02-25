using Microsoft.CodeAnalysis;

namespace ILGPU.Analyzers
{
    public static class MethodUtil
    {
        public static IOperation? GetMethodBody(SemanticModel model, IMethodSymbol symbol)
        {
            return symbol switch
            {
                { IsPartialDefinition: false } => model.GetOperation(
                    symbol.DeclaringSyntaxReferences[0].GetSyntax()),
                { PartialImplementationPart: not null } => model.GetOperation(
                    symbol.PartialImplementationPart.DeclaringSyntaxReferences[0]
                        .GetSyntax()),
                _ => null
            };
        }
    }
}