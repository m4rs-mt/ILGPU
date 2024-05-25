// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: InterleaveFieldsGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ILGPU.Analyzers
{
    [Generator]
    public class ReaderDelegatesGenerator : ISourceGenerator
    {
        private class ValueKindReciever : ISyntaxReceiver
        {
            private readonly ConcurrentDictionary<string, string> valueKinds;

            public IReadOnlyDictionary<string, string> ValueKinds => valueKinds;

            public ValueKindReciever()
            {
                valueKinds = new ConcurrentDictionary<string, string>();
            }

            public void OnVisitSyntaxNode(SyntaxNode node)
            {
                AttributeSyntax? attribute;
                if (node is ClassDeclarationSyntax cl &&
                    (attribute = cl.AttributeLists.SelectMany(x => x.Attributes)
                    .SingleOrDefault(attr => attr.Name.ToString() == "ValueKind"))
                    is not null)
                {
                    var valueKind = attribute.ArgumentList?
                        .Arguments.SingleOrDefault()
                        .Expression.ToString() ?? throw new InvalidOperationException("Malformed input syntax.");
                    var valueClass = cl.Identifier.ToString();
                    valueKinds.TryAdd(valueKind, valueClass);
                }
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ValueKindReciever());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var valueKindReciever = context.SyntaxReceiver as ValueKindReciever;
            if (valueKindReciever is not null)
            {
                var builder = new StringBuilder();

                builder.AppendLine("using ILGPU.IR.Values;");
                builder.AppendLine();
                builder.AppendLine("namespace ILGPU.IR");
                builder.AppendLine("{");
                builder.AppendLine("    public static partial class ValueKinds");
                builder.AppendLine("    {");
                builder.AppendLine("        static ValueKinds()");
                builder.AppendLine("        {");
                foreach (var kv in valueKindReciever.ValueKinds)
                {
                    builder.AppendLine($"            _readerDelegates.Add({kv.Key}, {kv.Value}.Read);");
                }
                builder.AppendLine("        }");
                builder.AppendLine("    }");
                builder.AppendLine("}");

                context.AddSource("ValueKinds.g.cs", builder.ToString());
            }
        }
    }
}
