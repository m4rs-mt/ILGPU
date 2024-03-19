using Microsoft.CodeAnalysis;

namespace ILGPU.Analyzers
{
    public static class MethodUtil
    {
        public static IOperation? GetMethodBody(SemanticModel model, IMethodSymbol symbol)
        {
            return symbol switch
            {
                {
                    IsPartialDefinition: false,
                    DeclaringSyntaxReferences: var refs,
                } => refs.Length > 0 ? model.GetOperation(refs[0].GetSyntax()) : null,
                {
                    PartialImplementationPart: { DeclaringSyntaxReferences: var refs },
                } => refs.Length > 0 ? model.GetOperation(refs[0].GetSyntax()) : null,
                _ => null
            };
        }
    }
}
