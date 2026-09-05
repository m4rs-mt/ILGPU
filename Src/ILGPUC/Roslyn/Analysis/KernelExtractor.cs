// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelExtractor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Extracts kernel descriptors from launch sites by analyzing
/// lambdas, method groups, and captured parameters.
/// </summary>
sealed class KernelExtractor(CSharpCompilation compilation)
{
    /// <summary>
    /// Returns all diagnostics reported while extracting kernels.
    /// </summary>
    public List<KernelDiagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// Extracts a <see cref="KernelDescriptor"/> from the given launch site by
    /// dispatching on the kernel argument syntax (lambda, simple lambda, or method group).
    /// </summary>
    public KernelDescriptor Extract(KernelLaunchInfo launchInfo)
    {
        var model = compilation.GetSemanticModel(launchInfo.Invocation.SyntaxTree);

        // Launches are replaced statement by statement, a launch nested in an
        // expression (e.g. an expression-bodied member) would remain unrewritten
        if (launchInfo.Invocation.Parent is not ExpressionStatementSyntax)
        {
            Diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.LaunchNotStatementId,
                $"Kernel launch '{launchInfo.Invocation.Expression}' must be a " +
                $"statement of its own to be compiled. Use a block body " +
                $"instead of an expression body.",
                launchInfo.Invocation.GetLocation()));
        }

        return launchInfo.KernelArgument switch
        {
            ParenthesizedLambdaExpressionSyntax lambda =>
                ExtractFromLambda(model, lambda, launchInfo, Diagnostics),
            SimpleLambdaExpressionSyntax lambda =>
                ExtractFromSimpleLambda(model, lambda, launchInfo, Diagnostics),
            IdentifierNameSyntax methodGroup =>
                ExtractFromMethodGroup(model, methodGroup, launchInfo, Diagnostics),
            MemberAccessExpressionSyntax methodGroup =>
                ExtractFromMethodGroup(model, methodGroup, launchInfo, Diagnostics),
            _ => throw new InvalidOperationException(
                $"Unsupported kernel argument syntax: " +
                $"{launchInfo.KernelArgument.GetType().Name}"),
        };
    }

    /// <summary>
    /// Handles a simple lambda (e.g., <c>_ =&gt; MainKernel(...)</c>):
    /// delegates to <see cref="CreateFromMethodCall"/> if the body is a direct
    /// call of a method without delegate parameters, otherwise falls back to
    /// <see cref="CreateFromInlineBody"/>.
    /// </summary>
    private static KernelDescriptor ExtractFromSimpleLambda(
        SemanticModel model,
        SimpleLambdaExpressionSyntax lambda,
        KernelLaunchInfo launchInfo,
        List<KernelDiagnostic> diagnostics)
    {
        // Simple lambda: _ => MainKernel(a.View, b.View, c.View)
        // or: index => { output[index] = input[index] * scalar; }
        var body = lambda.Body;

        // Check if body is a single method invocation
        if (TryExtractMethodCall(model, body, out var methodSymbol,
                out var arguments)
            && !HasDelegateParameters(methodSymbol!))
        {
            return CreateFromMethodCall(
                model, methodSymbol!, arguments, launchInfo, diagnostics);
        }

        // Inline kernel — extract captured variables
        return CreateFromInlineBody(model, lambda, body, launchInfo, diagnostics);
    }

    /// <summary>
    /// Handles a parenthesized lambda (e.g., <c>(index, x) =&gt; ...</c>):
    /// delegates to <see cref="CreateFromMethodCall"/> if the body is a direct
    /// call of a method without delegate parameters, otherwise falls back to
    /// <see cref="CreateFromInlineBody"/>.
    /// </summary>
    private static KernelDescriptor ExtractFromLambda(
        SemanticModel model,
        ParenthesizedLambdaExpressionSyntax lambda,
        KernelLaunchInfo launchInfo,
        List<KernelDiagnostic> diagnostics)
    {
        var body = lambda.Body;

        if (TryExtractMethodCall(model, body, out var methodSymbol,
                out var arguments)
            && !HasDelegateParameters(methodSymbol!))
        {
            return CreateFromMethodCall(
                model, methodSymbol!, arguments, launchInfo, diagnostics);
        }

        return CreateFromInlineBody(model, lambda, body, launchInfo, diagnostics);
    }

    /// <summary>
    /// Handles a method-group argument (e.g., <c>MyKernel</c> or <c>Obj.MyKernel</c>):
    /// derives parameters from the method's parameter list, skipping the first
    /// (thread-index) parameter.
    /// </summary>
    private static KernelDescriptor ExtractFromMethodGroup(
        SemanticModel model,
        ExpressionSyntax methodRef,
        KernelLaunchInfo launchInfo,
        List<KernelDiagnostic> diagnostics)
    {
        var symbolInfo = model.GetSymbolInfo(methodRef);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            throw new InvalidOperationException(
                $"Could not resolve method group: {methodRef}");
        }
        ValidateLocalFunctionKernel(model, method, diagnostics);

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
    /// Reports a local function used as a kernel that cannot be compiled as
    /// one: it captures variables of its enclosing method (which the compiler
    /// passes through a hidden closure parameter) or it is declared in a
    /// generic method or type.
    /// </summary>
    private static void ValidateLocalFunctionKernel(
        SemanticModel model,
        IMethodSymbol method,
        List<KernelDiagnostic> diagnostics)
    {
        if (method.MethodKind != MethodKind.LocalFunction
            || method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
                is not LocalFunctionStatementSyntax syntax)
        {
            return;
        }

        for (var symbol = method.ContainingSymbol;
            symbol is not null and not INamespaceSymbol;
            symbol = symbol.ContainingSymbol)
        {
            if (symbol is IMethodSymbol { TypeParameters.Length: > 0 }
                or INamedTypeSymbol { TypeParameters.Length: > 0 })
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnostic.GenericContextId,
                    $"Local function '{method.Name}' cannot be used as a kernel " +
                    $"because it is declared inside the generic member " +
                    $"'{symbol.ToDisplayString()}'. Use a static method instead.",
                    syntax.Identifier.GetLocation()));
                return;
            }
        }

        var localModel = model.Compilation.GetSemanticModel(syntax.SyntaxTree);
        var operation = localModel.GetOperation(syntax);
        if (operation is null)
            return;

        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var (symbol, reference) in EnumerateOuterReferences(operation, syntax))
        {
            // Captures of called local functions are captured by the kernel
            var callee = symbol as IMethodSymbol;
            var captured = callee is null
                ? symbol
                : FindLocalFunctionCapture(model.Compilation, callee);
            if (captured is null || !seen.Add(captured))
                continue;

            var through = callee is null
                ? ""
                : $" through the local function '{callee.Name}'";
            diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.LocalFunctionCapturesId,
                $"Local function '{method.Name}' cannot be used as a kernel " +
                $"because it captures '{GetCaptureName(captured)}' from its " +
                $"enclosing scope{through}. Make it static and pass the value " +
                $"as a parameter instead.",
                reference.GetLocation()));
        }
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
    /// Returns <see langword="true"/> if <paramref name="method"/> takes a
    /// delegate. Delegates cannot be kernel arguments, so a launch lambda
    /// calling such a method is lifted as an inline kernel instead: the
    /// compiler then binds lambdas and method groups passed at the call, and
    /// captured delegate parameters of the launching method are bound to the
    /// targets passed by its callers.
    /// </summary>
    private static bool HasDelegateParameters(IMethodSymbol method) =>
        method.Parameters.Any(p => p.Type.TypeKind == TypeKind.Delegate);

    /// <summary>
    /// Returns the path of the first delegate field of the given value type,
    /// also in nested structs (e.g. <c>Inner.Op</c>), or null if it has none.
    /// Fields of views are not inspected.
    /// </summary>
    internal static string? FindDelegateField(
        ITypeSymbol type,
        HashSet<ITypeSymbol>? visited = null)
    {
        if (!type.IsValueType || IsViewType(type) || IsPrimitiveType(type))
            return null;
        visited ??= new(SymbolEqualityComparer.Default);
        if (!visited.Add(type))
            return null;

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsStatic || field.IsConst)
                continue;
            var name = field.AssociatedSymbol?.Name ?? field.Name;
            if (field.Type.TypeKind == TypeKind.Delegate)
                return name;
            if (FindDelegateField(field.Type, visited) is { } nested)
                return $"{name}.{nested}";
        }
        return null;
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
        KernelLaunchInfo launchInfo,
        List<KernelDiagnostic> diagnostics)
    {
        ValidateLocalFunctionKernel(model, method, diagnostics);
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

            if (FindDelegateField(type) is { } delegateField)
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnostic.UnsupportedCaptureId,
                    $"Kernel argument '{arg.Expression}' of type '{type}' has the " +
                    $"delegate field '{delegateField}'. Delegates cannot be kernel " +
                    $"arguments; pass the other fields separately and the " +
                    $"delegate as a delegate parameter of the kernel method.",
                    arg.Expression.GetLocation()));
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
    /// Every variable of the enclosing method that the lambda uses becomes a
    /// kernel parameter, and the lambda body is lifted into a static local
    /// function next to the launch site taking the index and those values.
    /// Captures that cannot be passed to a kernel are reported as diagnostics.
    /// </summary>
    private static KernelDescriptor CreateFromInlineBody(
        SemanticModel model,
        LambdaExpressionSyntax lambda,
        SyntaxNode body,
        KernelLaunchInfo launchInfo,
        List<KernelDiagnostic> diagnostics)
    {
        var lambdaSymbol = model.GetSymbolInfo(lambda).Symbol as IMethodSymbol;
        var operation = model.GetOperation(lambda);
        var kernelName = GetInlineKernelName(lambda);
        var parameters = ImmutableArray.CreateBuilder<CapturedParameterInfo>();
        var delegateCaptures = ImmutableArray.CreateBuilder<DelegateCapture>();
        var bufferViewAccesses = new Dictionary<SyntaxNode, string>();

        bool isValid = lambdaSymbol is { Parameters.Length: 1 } && operation is not null;
        bool callsLocalFunctions = false;
        if (isValid)
        {
            isValid &= ValidateEnclosingContext(lambda, lambdaSymbol!, diagnostics);
            isValid &= ValidateInstanceUses(
                lambda, lambdaSymbol!, operation!, diagnostics);
            isValid &= ValidateLocalFunctionUses(
                model.Compilation,
                lambda,
                operation!,
                diagnostics,
                out callsLocalFunctions);
        }

        var dataFlow = model.AnalyzeDataFlow(lambda);
        var written = dataFlow is { Succeeded: true }
            ? new HashSet<ISymbol>(dataFlow.WrittenInside, SymbolEqualityComparer.Default)
            : new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var (captured, reference) in CollectCaptures(lambda, operation))
        {
            var type = captured switch
            {
                ILocalSymbol local => local.Type,
                IParameterSymbol param => param.Type,
                _ => null,
            };
            if (type is null)
                continue;

            if (!ValidateCapture(
                model.Compilation,
                lambda,
                captured,
                type,
                reference,
                written,
                diagnostics))
            {
                isValid = false;
                continue;
            }

            // Delegate parameters of the launching method are bound to the
            // targets passed by its callers, one kernel for each target
            if (type.TypeKind == TypeKind.Delegate)
            {
                delegateCaptures.Add(new DelegateCapture(
                    parameters.Count,
                    (IParameterSymbol)captured,
                    reference));
            }

            // If the captured variable is a MemoryBuffer, lower it to its
            // .View (ArrayView) — buffers are reference types that cannot be
            // kernel parameters, but their views can.
            var name = captured.Name;
            ExpressionSyntax sourceExpr;
            ITypeSymbol effectiveType;
            if (TryGetBufferViewType(type, out var viewType))
            {
                sourceExpr = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(CreateIdentifier(name)),
                    SyntaxFactory.IdentifierName("View"));
                effectiveType = viewType!;
                foreach (var access in FindViewAccesses(operation!, captured))
                    bufferViewAccesses[access] = name;
            }
            else
            {
                sourceExpr = SyntaxFactory.IdentifierName(CreateIdentifier(name));
                effectiveType = type;
            }

            var kind = Classify(effectiveType);
            parameters.Add(new CapturedParameterInfo(
                Name: captured.Name,
                SourceExpression: sourceExpr,
                Type: effectiveType,
                Kind: kind));
        }

        var liftedKernel = isValid
            ? LiftInlineKernel(
                lambda,
                lambdaSymbol!,
                kernelName,
                parameters,
                bufferViewAccesses,
                isStatic: !callsLocalFunctions)
            : null;

        return new KernelDescriptor(
            KernelName: kernelName,
            KernelMethod: null,
            KernelBody: body,
            Parameters: parameters.ToImmutable(),
            Variant: launchInfo.Variant,
            LaunchInfo: launchInfo,
            LiftedKernel: liftedKernel,
            DelegateCaptures: delegateCaptures.Count > 0
                ? delegateCaptures.ToImmutable()
                : null);
    }

    /// <summary>
    /// Returns the name of an inline kernel, derived from the file and the
    /// position of its lambda (e.g. <c>InlineKernel_Program_L12_C31</c>).
    /// </summary>
    private static string GetInlineKernelName(LambdaExpressionSyntax lambda)
    {
        var span = lambda.GetLocation().GetLineSpan();
        var position = span.StartLinePosition;
        var file = new string(Path.GetFileNameWithoutExtension(span.Path ?? "")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());
        var prefix = file.Length > 0 ? $"InlineKernel_{file}" : "InlineKernel";
        return $"{prefix}_L{position.Line + 1}_C{position.Character + 1}";
    }

    /// <summary>
    /// Returns every variable of the enclosing scopes that the given lambda
    /// (or a function nested in it) refers to, in source order, together with
    /// its first reference. Constants are not captures and are skipped.
    /// </summary>
    private static List<(ISymbol Symbol, SyntaxNode Reference)> CollectCaptures(
        LambdaExpressionSyntax lambda,
        IOperation? operation)
    {
        var captures = new List<(ISymbol, SyntaxNode)>();
        if (operation is null)
            return captures;

        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var (symbol, reference) in EnumerateOuterReferences(operation, lambda))
        {
            if (symbol is ILocalSymbol or IParameterSymbol && seen.Add(symbol))
                captures.Add((symbol, reference));
        }
        return captures;
    }

    /// <summary>
    /// Returns the references of <paramref name="operation"/> to symbols declared
    /// outside of <paramref name="scope"/> in source order: non-constant locals
    /// and parameters, the enclosing instance (as its type), and non-static
    /// local functions, which may capture variables themselves.
    /// </summary>
    internal static IEnumerable<(ISymbol Symbol, SyntaxNode Reference)>
        EnumerateOuterReferences(IOperation operation, SyntaxNode scope)
    {
        foreach (var op in operation.Descendants().OrderBy(o => o.Syntax.SpanStart))
        {
            ISymbol? symbol = op switch
            {
                ILocalReferenceOperation { Local.IsConst: false } local => local.Local,
                IParameterReferenceOperation param => param.Parameter,
                IInstanceReferenceOperation
                {
                    ReferenceKind: InstanceReferenceKind.ContainingTypeInstance,
                } instance => instance.Type,
                IInvocationOperation invocation =>
                    AsNonStaticLocalFunction(invocation.TargetMethod),
                IMethodReferenceOperation reference =>
                    AsNonStaticLocalFunction(reference.Method),
                _ => null,
            };
            if (symbol is null
                || symbol is not ITypeSymbol && IsDeclaredInside(symbol, scope))
            {
                continue;
            }
            yield return (symbol, op.Syntax);
        }
    }

    /// <summary>
    /// Returns the definition of <paramref name="method"/> if it is a local
    /// function that is not declared <see langword="static"/>.
    /// </summary>
    private static IMethodSymbol? AsNonStaticLocalFunction(IMethodSymbol method) =>
        method.OriginalDefinition is
        {
            MethodKind: MethodKind.LocalFunction,
            IsStatic: false,
        } definition
            ? definition
            : null;

    /// <summary>
    /// Returns the first variable of an enclosing scope, or the type of the
    /// enclosing instance, that the given non-static local function uses,
    /// directly or through the local functions it calls. Returns
    /// <see langword="null"/> if the local function captures nothing.
    /// </summary>
    internal static ISymbol? FindLocalFunctionCapture(
        Compilation compilation,
        IMethodSymbol function,
        HashSet<ISymbol>? visited = null)
    {
        visited ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (!visited.Add(function)
            || function.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
                is not LocalFunctionStatementSyntax syntax
            || compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax)
                is not { } operation)
        {
            return null;
        }

        foreach (var (symbol, _) in EnumerateOuterReferences(operation, syntax))
        {
            var captured = symbol is IMethodSymbol callee
                ? FindLocalFunctionCapture(compilation, callee, visited)
                : symbol;
            if (captured is not null)
                return captured;
        }
        return null;
    }

    /// <summary>
    /// Returns the name of a captured symbol for diagnostics, which is
    /// <c>this</c> for the enclosing instance.
    /// </summary>
    internal static string GetCaptureName(ISymbol captured) =>
        captured is ITypeSymbol ? "this" : captured.Name;

    /// <summary>
    /// Reports the non-static local functions of the enclosing scopes called by
    /// an inline kernel that capture variables: the lifted kernel cannot pass
    /// them on. Non-capturing ones can be called, but only from a lifted
    /// kernel that is not declared static itself.
    /// </summary>
    private static bool ValidateLocalFunctionUses(
        Compilation compilation,
        LambdaExpressionSyntax lambda,
        IOperation operation,
        List<KernelDiagnostic> diagnostics,
        out bool callsLocalFunctions)
    {
        callsLocalFunctions = false;
        bool isValid = true;
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var (symbol, reference) in EnumerateOuterReferences(operation, lambda))
        {
            if (symbol is not IMethodSymbol function || !seen.Add(function))
                continue;

            callsLocalFunctions = true;
            if (FindLocalFunctionCapture(compilation, function) is not { } captured)
                continue;

            diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.LocalFunctionCapturesId,
                $"Inline kernel calls the local function '{function.Name}', which " +
                $"captures '{GetCaptureName(captured)}' from its enclosing scope. " +
                $"Make it static and pass the value as a parameter instead.",
                reference.GetLocation()));
            isValid = false;
        }
        return isValid;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="symbol"/> is declared
    /// within <paramref name="node"/> (a parameter or local of the function
    /// itself or of a function nested in it).
    /// </summary>
    private static bool IsDeclaredInside(ISymbol symbol, SyntaxNode node) =>
        symbol.DeclaringSyntaxReferences.Any(r =>
            r.SyntaxTree == node.SyntaxTree && node.Span.Contains(r.Span));

    /// <summary>
    /// Reports an inline kernel declared inside a generic method or type: the
    /// lifted kernel would be generic over type parameters that are only bound
    /// by the callers of the enclosing method.
    /// </summary>
    private static bool ValidateEnclosingContext(
        LambdaExpressionSyntax lambda,
        IMethodSymbol lambdaSymbol,
        List<KernelDiagnostic> diagnostics)
    {
        for (var symbol = lambdaSymbol.ContainingSymbol;
            symbol is not null and not INamespaceSymbol;
            symbol = symbol.ContainingSymbol)
        {
            bool isGeneric = symbol switch
            {
                IMethodSymbol method => method.TypeParameters.Length > 0,
                INamedTypeSymbol type => type.TypeParameters.Length > 0,
                _ => false,
            };
            if (!isGeneric)
                continue;

            diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.GenericContextId,
                $"Inline kernels cannot be declared inside the generic " +
                $"{(symbol is IMethodSymbol ? "method" : "type")} " +
                $"'{symbol.ToDisplayString()}'. Move the kernel body into a " +
                $"static method and launch it with a lambda that calls it.",
                lambda.GetLocation()));
            return false;
        }
        return true;
    }

    /// <summary>
    /// Reports every use of the enclosing instance (<c>this</c>, <c>base</c>,
    /// or an instance member accessed through them) inside an inline kernel.
    /// </summary>
    private static bool ValidateInstanceUses(
        LambdaExpressionSyntax lambda,
        IMethodSymbol lambdaSymbol,
        IOperation operation,
        List<KernelDiagnostic> diagnostics)
    {
        bool isValid = true;
        foreach (var op in operation.Descendants())
        {
            if (op is not IInstanceReferenceOperation
                {
                    ReferenceKind: InstanceReferenceKind.ContainingTypeInstance,
                } instance)
            {
                continue;
            }

            // Report the member access instead of its implicit receiver
            var syntax = instance.IsImplicit && instance.Parent is not null
                ? instance.Parent.Syntax
                : instance.Syntax;
            diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.CapturesThisId,
                $"Inline kernel uses the enclosing instance in '{syntax}'. " +
                $"Kernels cannot capture 'this'; copy the value into a local " +
                $"variable before the launch.",
                syntax.GetLocation()));
            isValid = false;
        }

        // Primary-constructor parameters used outside of the constructor are
        // stored on the instance as well
        foreach (var (captured, reference) in CollectCaptures(lambda, operation))
        {
            if (captured is IParameterSymbol param
                && param.ContainingSymbol is IMethodSymbol
                {
                    MethodKind: MethodKind.Constructor,
                } constructor
                && !IsContainedIn(lambdaSymbol, constructor))
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnostic.CapturesThisId,
                    $"Inline kernel uses the primary constructor parameter " +
                    $"'{param.Name}', which is stored in the enclosing " +
                    $"instance. Kernels cannot capture 'this'; copy the value " +
                    $"into a local variable before the launch.",
                    reference.GetLocation()));
                isValid = false;
            }
        }
        return isValid;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="container"/> is one of
    /// the symbols containing <paramref name="symbol"/>.
    /// </summary>
    private static bool IsContainedIn(ISymbol symbol, ISymbol container)
    {
        for (var current = symbol.ContainingSymbol; current is not null;
            current = current.ContainingSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(current, container))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Validates a single captured variable of an inline kernel: it must not
    /// be assigned inside the kernel and its type must be passable as a kernel
    /// argument (a value type or a buffer that is lowered to its view), or it
    /// must be a delegate parameter of the launching method that is bound to
    /// the targets its callers pass.
    /// </summary>
    private static bool ValidateCapture(
        Compilation compilation,
        LambdaExpressionSyntax lambda,
        ISymbol captured,
        ITypeSymbol type,
        SyntaxNode reference,
        HashSet<ISymbol> written,
        List<KernelDiagnostic> diagnostics)
    {
        var location = reference.GetLocation();
        bool isByRef = captured switch
        {
            ILocalSymbol local => local.IsRef,
            IParameterSymbol param => param.RefKind != RefKind.None,
            _ => false,
        };

        string? message = null;
        string id = KernelDiagnostic.UnsupportedCaptureId;
        if (isByRef || type.IsRefLikeType)
        {
            message =
                $"Inline kernel captures '{captured.Name}' " +
                (isByRef ? "by reference" : $"of ref struct type '{type}'") +
                ", which cannot be passed to a kernel.";
        }
        else if (type.TypeKind == TypeKind.Delegate
            && !DelegateTargetDiscoverer.IsBindableParameter(compilation, captured))
        {
            message =
                $"Inline kernel captures the delegate '{captured.Name}', which " +
                $"cannot be passed to a kernel. Only delegate parameters of the " +
                $"launching method that it does not assign can be captured; pass " +
                $"a lambda expression or a static method directly at the call " +
                $"inside the kernel, or use a generic struct implementing an " +
                $"interface instead.";
        }
        else if (type.TypeKind != TypeKind.Delegate
            && type.IsReferenceType
            && !TryGetBufferViewType(type, out _))
        {
            message =
                $"Inline kernel captures '{captured.Name}' of reference type " +
                $"'{type}', which cannot be passed to a kernel. Capture a view " +
                $"or a value type instead.";
        }
        else if (FindDelegateField(type) is { } delegateField)
        {
            message =
                $"Inline kernel captures '{captured.Name}' of type '{type}', whose " +
                $"field '{delegateField}' is a delegate. Delegates cannot be " +
                $"kernel arguments; capture the other fields separately and pass " +
                $"the delegate as a parameter of the launching method.";
        }
        else if (written.Contains(captured))
        {
            id = KernelDiagnostic.AssignsCaptureId;
            message =
                $"Inline kernel assigns the captured variable " +
                $"'{captured.Name}'. Kernels receive captured values by copy, " +
                $"so the assignment would not be visible outside the kernel; " +
                $"write results to a view instead.";
            location = FindAssignment(lambda, captured, reference)
                .GetLocation();
        }

        if (message is null)
            return true;
        diagnostics.Add(new KernelDiagnostic(id, message, location));
        return false;
    }

    /// <summary>
    /// Returns the first identifier inside <paramref name="lambda"/> that names
    /// <paramref name="symbol"/> and is the target of a write, falling back to
    /// <paramref name="fallback"/>.
    /// </summary>
    private static SyntaxNode FindAssignment(
        LambdaExpressionSyntax lambda,
        ISymbol symbol,
        SyntaxNode fallback)
    {
        foreach (var id in lambda.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (id.Identifier.ValueText != symbol.Name)
                continue;
            var target = id.Parent is MemberAccessExpressionSyntax access
                && access.Expression == id ? (ExpressionSyntax)access : id;
            bool isWrite = target.Parent switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left == target,
                PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
                ArgumentSyntax argument =>
                    !argument.RefKindKeyword.IsKind(SyntaxKind.None),
                _ => false,
            };
            if (isWrite)
                return id;
        }
        return fallback;
    }

    /// <summary>
    /// Returns the <c>buffer.View</c> accesses of a captured buffer inside an
    /// inline kernel. The lifted kernel receives the view itself under the
    /// buffer's name, so these accesses are replaced by the bare name.
    /// </summary>
    private static IEnumerable<SyntaxNode> FindViewAccesses(
        IOperation operation,
        ISymbol buffer) =>
        operation.Descendants()
            .OfType<IPropertyReferenceOperation>()
            .Where(p => p.Property.Name == "View"
                && p.Instance switch
                {
                    ILocalReferenceOperation local =>
                        SymbolEqualityComparer.Default.Equals(local.Local, buffer),
                    IParameterReferenceOperation param =>
                        SymbolEqualityComparer.Default.Equals(param.Parameter, buffer),
                    _ => false,
                })
            .Select(p => p.Syntax);

    /// <summary>
    /// Lifts the body of an inline kernel into a local function that takes the
    /// kernel index followed by all captured values:
    /// <code>
    /// static void InlineKernel_Program_L12_C31(
    ///     global::ILGPU.Index1D index, int offset) { ... }
    /// </code>
    /// Declared next to the launch site, the body keeps its lexical scope
    /// (usings, helpers and nested types of the enclosing type), while the
    /// parameters shadow the captured variables they replace. The function is
    /// declared <see langword="static"/> unless it calls (non-capturing) local
    /// functions of the enclosing scopes that are not static themselves.
    /// </summary>
    private static LocalFunctionStatementSyntax LiftInlineKernel(
        LambdaExpressionSyntax lambda,
        IMethodSymbol lambdaSymbol,
        string kernelName,
        IEnumerable<CapturedParameterInfo> parameters,
        Dictionary<SyntaxNode, string> bufferViewAccesses,
        bool isStatic)
    {
        var indexParameter = lambdaSymbol.Parameters[0];
        var parameterList = new List<ParameterSyntax>
        {
            CreateParameter(indexParameter.Name, indexParameter.Type),
        };
        foreach (var param in parameters)
            parameterList.Add(CreateParameter(param.Name, param.Type));

        var function = SyntaxFactory.LocalFunctionStatement(
                SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                SyntaxFactory.Identifier(kernelName))
            .WithParameterList(SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(parameterList)));
        if (isStatic)
        {
            function = function.WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.StaticKeyword)));
        }

        var body = lambda.Body.ReplaceNodes(
            bufferViewAccesses.Keys,
            (original, _) => SyntaxFactory.IdentifierName(
                    CreateIdentifier(bufferViewAccesses[original]))
                .WithTriviaFrom(original));
        function = body switch
        {
            BlockSyntax block => function.WithBody(block),
            ExpressionSyntax expression => function
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(expression))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            _ => throw new InvalidOperationException(
                $"Unsupported inline kernel body: {body.Kind()}"),
        };
        return function.NormalizeWhitespace();
    }

    /// <summary>
    /// Creates a parameter declaration with a fully qualified type, so it
    /// binds independently of the using directives of the launch site.
    /// </summary>
    private static ParameterSyntax CreateParameter(string name, ITypeSymbol type) =>
        SyntaxFactory.Parameter(CreateIdentifier(name))
            .WithType(SyntaxFactory.ParseTypeName(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

    /// <summary>
    /// Creates an identifier token for <paramref name="name"/>, written as a
    /// verbatim <c>@</c> identifier if the name is a C# keyword.
    /// </summary>
    private static SyntaxToken CreateIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            ? SyntaxFactory.ParseToken($"@{name}")
            : SyntaxFactory.Identifier(name);

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
