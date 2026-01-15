// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelExtractor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Extracts kernel descriptors from launch sites by analyzing
/// lambdas, method groups, and captured parameters.
/// </summary>
sealed class KernelExtractor(CSharpCompilation compilation)
{
    /// <summary>
    /// Extracts a <see cref="KernelDescriptor"/> from the given launch site by
    /// dispatching on the kernel argument syntax (lambda, simple lambda, or method group).
    /// </summary>
    public KernelDescriptor Extract(KernelLaunchInfo launchInfo)
    {
        var model = compilation.GetSemanticModel(launchInfo.Invocation.SyntaxTree);

        return launchInfo.KernelArgument switch
        {
            ParenthesizedLambdaExpressionSyntax lambda =>
                ExtractFromLambda(model, lambda, launchInfo),
            SimpleLambdaExpressionSyntax lambda =>
                ExtractFromSimpleLambda(model, lambda, launchInfo),
            IdentifierNameSyntax methodGroup =>
                ExtractFromMethodGroup(model, methodGroup, launchInfo),
            MemberAccessExpressionSyntax methodGroup =>
                ExtractFromMethodGroup(model, methodGroup, launchInfo),
            _ => throw new InvalidOperationException(
                $"Unsupported kernel argument syntax: " +
                $"{launchInfo.KernelArgument.GetType().Name}"),
        };
    }

    /// <summary>
    /// Handles a simple lambda (e.g., <c>_ =&gt; MainKernel(...)</c>):
    /// delegates to <see cref="CreateFromMethodCall"/> if the body is a direct
    /// method call, otherwise falls back to <see cref="CreateFromInlineBody"/>.
    /// </summary>
    private static KernelDescriptor ExtractFromSimpleLambda(
        SemanticModel model,
        SimpleLambdaExpressionSyntax lambda,
        KernelLaunchInfo launchInfo)
    {
        // Simple lambda: _ => MainKernel(a.View, b.View, c.View)
        // or: index => { output[index] = input[index] * scalar; }
        var body = lambda.Body;

        // Check if body is a single method invocation
        if (TryExtractMethodCall(model, body, out var methodSymbol,
                out var arguments))
        {
            return CreateFromMethodCall(
                model, methodSymbol!, arguments, launchInfo);
        }

        // Inline kernel — extract captured variables
        return CreateFromInlineBody(model, lambda, body, launchInfo);
    }

    /// <summary>
    /// Handles a parenthesized lambda (e.g., <c>(index, x) =&gt; ...</c>):
    /// delegates to <see cref="CreateFromMethodCall"/> if the body is a direct
    /// method call, otherwise falls back to <see cref="CreateFromInlineBody"/>.
    /// </summary>
    private static KernelDescriptor ExtractFromLambda(
        SemanticModel model,
        ParenthesizedLambdaExpressionSyntax lambda,
        KernelLaunchInfo launchInfo)
    {
        var body = lambda.Body;

        if (TryExtractMethodCall(model, body, out var methodSymbol,
                out var arguments))
        {
            return CreateFromMethodCall(
                model, methodSymbol!, arguments, launchInfo);
        }

        return CreateFromInlineBody(model, lambda, body, launchInfo);
    }

    /// <summary>
    /// Handles a method-group argument (e.g., <c>MyKernel</c> or <c>Obj.MyKernel</c>):
    /// derives parameters from the method's parameter list, skipping the first
    /// (thread-index) parameter.
    /// </summary>
    private static KernelDescriptor ExtractFromMethodGroup(
        SemanticModel model,
        ExpressionSyntax methodRef,
        KernelLaunchInfo launchInfo)
    {
        var symbolInfo = model.GetSymbolInfo(methodRef);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            throw new InvalidOperationException(
                $"Could not resolve method group: {methodRef}");
        }

        // For method groups, parameters come from the method signature.
        // Skip the first parameter if it's an index type (Index1D,
        // LongIndex1D, Index2D, Index3D, KernelIndex) — these are
        // provided by the runtime, not captured from the call site.
        int skipCount = method.Parameters.Length > 0
            && IsIndexType(method.Parameters[0].Type)
            ? 1 : 0;
        var parameters = ImmutableArray.CreateBuilder<CapturedParameterInfo>();
        foreach (var param in method.Parameters.Skip(skipCount))
        {
            var kind = Classify(param.Type);
            parameters.Add(new CapturedParameterInfo(
                Name: param.Name,
                SourceExpression: SyntaxFactory.IdentifierName(param.Name),
                Type: param.Type,
                Kind: kind));
        }

        return new KernelDescriptor(
            KernelName: method.Name,
            KernelMethod: method,
            KernelBody: null,
            Parameters: parameters.ToImmutable(),
            Variant: launchInfo.Variant,
            LaunchInfo: launchInfo);
    }

    /// <summary>
    /// Attempts to unwrap a lambda body to a single method-call invocation.
    /// Handles expression bodies, expression-statement bodies, and single-statement
    /// block bodies.
    /// </summary>
    /// <param name="model">
    /// The semantic model for the syntax tree containing the body.
    /// </param>
    /// <param name="body">The lambda body node to inspect.</param>
    /// <param name="method">The resolved method symbol if successful.</param>
    /// <param name="arguments">The argument list of the invocation if successful.</param>
    /// <returns>
    /// <see langword="true"/> if the body is a direct method call and the symbol
    /// could be resolved; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryExtractMethodCall(
        SemanticModel model,
        SyntaxNode body,
        out IMethodSymbol? method,
        out SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        method = null;
        arguments = default;

        // Unwrap block body with single expression statement
        InvocationExpressionSyntax? invocation = body switch
        {
            InvocationExpressionSyntax inv => inv,
            ExpressionStatementSyntax { Expression: InvocationExpressionSyntax inv }
                => inv,
            BlockSyntax
            {
                Statements: [ExpressionStatementSyntax
                { Expression: InvocationExpressionSyntax inv }]
            } => inv,
            _ => null,
        };

        if (invocation == null)
            return false;

        var symbolInfo = model.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol resolved)
            return false;

        method = resolved;
        arguments = invocation.ArgumentList.Arguments;
        return true;
    }

    /// <summary>
    /// Builds a <see cref="KernelDescriptor"/> for a kernel identified as a
    /// direct method call inside a lambda body. Parameters are derived from the
    /// arguments passed to that call.
    /// </summary>
    private static KernelDescriptor CreateFromMethodCall(
        SemanticModel model,
        IMethodSymbol method,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        KernelLaunchInfo launchInfo)
    {
        var parameters = ImmutableArray.CreateBuilder<CapturedParameterInfo>();

        // Determine whether to skip argument 0: skip it only if it is
        // the lambda's own index parameter (i.e., a simple identifier
        // matching a lambda parameter name). For auto-sized launches
        // this is always the case (index => Kernel(index, ...)); for
        // grouped launches it depends — the index may or may not be
        // forwarded to the kernel method.
        int startIndex = 0;
        if (arguments.Count > 0
            && arguments[0].Expression is IdentifierNameSyntax id
            && IsLambdaParameterName(launchInfo, id.Identifier.Text))
        {
            startIndex = 1;
        }
        for (int i = startIndex; i < arguments.Count; i++)
        {
            var arg = arguments[i];
            var typeInfo = model.GetTypeInfo(arg.Expression);
            var type = typeInfo.Type ?? typeInfo.ConvertedType;
            if (type == null)
            {
                throw new InvalidOperationException(
                    $"Could not resolve type of argument: {arg.Expression}");
            }

            var kind = Classify(type);

            // Generate a clean parameter name from the argument expression
            var name = method.Parameters.Length > i
                ? method.Parameters[i].Name
                : GenerateParameterName(arg.Expression, i);

            parameters.Add(new CapturedParameterInfo(
                Name: name,
                SourceExpression: arg.Expression,
                Type: type,
                Kind: kind));
        }

        return new KernelDescriptor(
            KernelName: method.Name,
            KernelMethod: method,
            KernelBody: null,
            Parameters: parameters.ToImmutable(),
            Variant: launchInfo.Variant,
            LaunchInfo: launchInfo);
    }

    /// <summary>
    /// Builds a <see cref="KernelDescriptor"/> for an inline lambda kernel.
    /// Uses data-flow analysis to discover captured variables.
    /// </summary>
    private static KernelDescriptor CreateFromInlineBody(
        SemanticModel model,
        LambdaExpressionSyntax lambda,
        SyntaxNode body,
        KernelLaunchInfo launchInfo)
    {
        // Use data flow analysis to find captured variables
        var bodyNode = body switch
        {
            BlockSyntax block => (SyntaxNode)block,
            ExpressionSyntax expr => expr,
            _ => body,
        };

        DataFlowAnalysis? dataFlow = bodyNode switch
        {
            StatementSyntax stmt => model.AnalyzeDataFlow(stmt),
            ExpressionSyntax expr => model.AnalyzeDataFlow(expr),
            _ => null,
        };
        var parameters = ImmutableArray.CreateBuilder<CapturedParameterInfo>();

        if (dataFlow != null && dataFlow.Succeeded)
        {
            foreach (var captured in dataFlow.DataFlowsIn)
            {
                // Skip the lambda parameter itself (the index)
                if (IsLambdaParameter(lambda, captured))
                    continue;

                var type = captured switch
                {
                    ILocalSymbol local => local.Type,
                    IParameterSymbol param => param.Type,
                    IFieldSymbol field => field.Type,
                    _ => null,
                };

                if (type == null)
                    continue;

                // If the captured variable is a MemoryBuffer, lower it to its
                // .View (ArrayView) — buffers are reference types that cannot be
                // kernel parameters, but their views can.
                ExpressionSyntax sourceExpr;
                ITypeSymbol effectiveType;
                if (TryGetBufferViewType(type, out var viewType))
                {
                    sourceExpr = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(captured.Name),
                        SyntaxFactory.IdentifierName("View"));
                    effectiveType = viewType!;
                }
                else
                {
                    sourceExpr = SyntaxFactory.IdentifierName(captured.Name);
                    effectiveType = type;
                }

                var kind = Classify(effectiveType);
                parameters.Add(new CapturedParameterInfo(
                    Name: captured.Name,
                    SourceExpression: sourceExpr,
                    Type: effectiveType,
                    Kind: kind));
            }
        }

        // Generate a unique name for inline kernels
        var location = lambda.GetLocation().GetLineSpan();
        var kernelName = $"InlineKernel_L{location.StartLinePosition.Line}";

        return new KernelDescriptor(
            KernelName: kernelName,
            KernelMethod: null,
            KernelBody: body,
            Parameters: parameters.ToImmutable(),
            Variant: launchInfo.Variant,
            LaunchInfo: launchInfo);
    }

    /// <summary>
    /// Returns <see langword="true"/> if the type is a known ILGPU index type.
    /// </summary>
    private static bool IsIndexType(ITypeSymbol type) =>
        type.Name is "Index1D" or "LongIndex1D"
            or "Index2D" or "LongIndex2D"
            or "Index3D" or "LongIndex3D"
            or "KernelIndex";

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="name"/> matches a lambda
    /// parameter declared in the kernel argument of the launch site.
    /// </summary>
    private static bool IsLambdaParameterName(
        KernelLaunchInfo launchInfo, string name)
    {
        return launchInfo.KernelArgument switch
        {
            SimpleLambdaExpressionSyntax simple =>
                simple.Parameter.Identifier.Text == name,
            ParenthesizedLambdaExpressionSyntax paren =>
                paren.ParameterList.Parameters
                    .Any(p => p.Identifier.Text == name),
            _ => false,
        };
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="symbol"/> is one of the
    /// declared parameters of <paramref name="lambda"/> (i.e., the thread-index
    /// variable, not a captured outer variable).
    /// </summary>
    private static bool IsLambdaParameter(
        LambdaExpressionSyntax lambda, ISymbol symbol)
    {
        if (symbol is not IParameterSymbol)
            return false;

        var paramNames = lambda switch
        {
            SimpleLambdaExpressionSyntax simple =>
                [simple.Parameter.Identifier.Text],
            ParenthesizedLambdaExpressionSyntax paren =>
                paren.ParameterList.Parameters
                    .Select(p => p.Identifier.Text).ToArray(),
            _ => Array.Empty<string>(),
        };

        return paramNames.Contains(symbol.Name);
    }

    /// <summary>
    /// Derives a valid C# identifier from an argument expression string,
    /// replacing punctuation with underscores or removing it.
    /// Falls back to <c>param{index}</c> if the result is not a valid identifier.
    /// </summary>
    private static string GenerateParameterName(
        ExpressionSyntax expr, int index)
    {
        // Try to derive a name from the expression
        var text = expr.ToString()
            .Replace(".", "_", StringComparison.Ordinal)
            .Replace("[", "", StringComparison.Ordinal)
            .Replace("]", "", StringComparison.Ordinal);

        if (SyntaxFacts.IsValidIdentifier(text))
            return text;

        return $"param{index}";
    }

    #region Parameter Classification

    /// <summary>
    /// Classifies a type symbol into a <see cref="ParameterKind"/> for
    /// use in kernel parameter generation.
    /// </summary>
    internal static ParameterKind Classify(ITypeSymbol type)
    {
        // Check for ArrayView<T> and derived types (ArrayView1D, ArrayView2D, etc.)
        if (IsViewType(type))
            return ParameterKind.View;

        // Check for pointer types
        if (type.TypeKind == TypeKind.Pointer)
            return ParameterKind.Pointer;
        if (type.Name == "IntPtr" || type.Name == "UIntPtr")
            return ParameterKind.Pointer;

        // Check if it's a value type / struct
        if (type.IsValueType && type.TypeKind == TypeKind.Struct)
        {
            // Primitive types are special-named structs in .NET
            if (IsPrimitiveType(type))
                return ParameterKind.Primitive;

            if (ContainsViewField(type))
                return ParameterKind.StructWithViews;
            return ParameterKind.StructPlain;
        }

        // Enums, etc.
        if (type.TypeKind == TypeKind.Enum)
            return ParameterKind.Primitive;

        return ParameterKind.Primitive;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="type"/> is a known
    /// .NET primitive numeric or boolean type, including <c>Half</c>,
    /// <c>Int128</c>, and <c>UInt128</c>.
    /// </summary>
    private static bool IsPrimitiveType(ITypeSymbol type) =>
        type.SpecialType switch
        {
            SpecialType.System_Boolean => true,
            SpecialType.System_Byte => true,
            SpecialType.System_SByte => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Single => true,
            SpecialType.System_Double => true,
            SpecialType.System_Char => true,
            _ => type.Name is "Half" or "Int128" or "UInt128",
        };

    /// <summary>
    /// Returns true if the type is ArrayView&lt;T&gt; or a derived view type
    /// (ArrayView1D, ArrayView2D, ArrayView3D).
    /// </summary>
    private static bool IsViewType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            if (named.Name is "ArrayView" or "ArrayView1D" or "ArrayView2D"
                or "ArrayView3D")
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="type"/> is a struct
    /// that directly or transitively contains an ILGPU view field.
    /// Cycle-safe via <paramref name="visited"/>.
    /// </summary>
    private static bool ContainsViewField(
        ITypeSymbol type,
        HashSet<ITypeSymbol>? visited = null)
    {
        visited ??= new(SymbolEqualityComparer.Default);
        if (!visited.Add(type))
            return false;

        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field && !field.IsStatic)
            {
                if (IsViewType(field.Type))
                    return true;
                if (field.Type.IsValueType && field.Type.TypeKind == TypeKind.Struct
                    && !IsPrimitiveType(field.Type))
                {
                    if (ContainsViewField(field.Type, visited))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// If <paramref name="type"/> is a MemoryBuffer type
    /// (<c>MemoryBuffer1D</c>, <c>MemoryBuffer2D</c>, <c>MemoryBuffer3D</c>),
    /// extracts the corresponding <c>ArrayView</c> type from the
    /// <c>MemoryBuffer&lt;TView&gt;</c> base class and returns it via
    /// <paramref name="viewType"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the type is a known MemoryBuffer and
    /// the view type was successfully extracted.
    /// </returns>
    private static bool TryGetBufferViewType(
        ITypeSymbol type,
        out ITypeSymbol? viewType)
    {
        viewType = null;

        if (type is not INamedTypeSymbol named || !named.IsGenericType)
            return false;

        if (named.Name is not ("MemoryBuffer1D" or "MemoryBuffer2D"
            or "MemoryBuffer3D"))
            return false;

        // Walk the base type chain to find MemoryBuffer<TView>
        // and extract TView (the ArrayView type).
        var current = named.BaseType;
        while (current != null)
        {
            if (current is { IsGenericType: true, Name: "MemoryBuffer" }
                && current.TypeArguments.Length == 1)
            {
                viewType = current.TypeArguments[0];
                return true;
            }
            current = current.BaseType;
        }

        return false;
    }

    #endregion
}
