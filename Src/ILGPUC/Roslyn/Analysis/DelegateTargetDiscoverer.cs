// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateTargetDiscoverer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// A delegate passed at a call site that is registered under the keys of its
/// target at runtime.
/// </summary>
/// <param name="DelegateType">The type of the delegate parameter at the call.</param>
/// <param name="Keys">The keys the delegate is registered under.</param>
record DelegateRegistration(ITypeSymbol DelegateType, List<string> Keys);

/// <summary>
/// Discovers the targets of delegate parameters of launching methods that are
/// captured by inline kernels. Like <see cref="SpecializationDiscoverer"/> for
/// type arguments, it traces the callers of the launching method for the
/// lambda expressions, anonymous methods and static methods they pass, also
/// through delegate parameters of calling methods that forward them. Each
/// distinct target gets a key, the call sites register their delegates under
/// the keys of their targets at runtime, and a kernel bound to each target is
/// compiled.
/// </summary>
/// <param name="compilation">The Roslyn compilation.</param>
sealed class DelegateTargetDiscoverer(Compilation compilation)
{
    /// <summary>
    /// The keys of the targets of each traced parameter (null if the targets
    /// could not be traced).
    /// </summary>
    private readonly Dictionary<IParameterSymbol, ImmutableArray<string>?> _keys =
        new(SymbolEqualityComparer.Default);

    /// <summary>
    /// The number of keys created so far, which makes all keys unique.
    /// </summary>
    private int _numKeys;

    /// <summary>
    /// Returns all diagnostics reported while tracing targets.
    /// </summary>
    public List<KernelDiagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// Returns the delegates to register at runtime by their argument syntax.
    /// </summary>
    public Dictionary<ArgumentSyntax, DelegateRegistration> Registrations { get; } =
        [];

    /// <summary>
    /// Returns the keys of all targets passed to the given captured delegate
    /// parameter, or null if they could not be traced (which is reported).
    /// </summary>
    /// <param name="capture">The captured delegate parameter.</param>
    public ImmutableArray<string>? DiscoverTargets(DelegateCapture capture)
    {
        var parameter = capture.Parameter;
        if (_keys.TryGetValue(parameter, out var cached))
            return cached;

        var targets = new List<(ISymbol, ArgumentSyntax, ITypeSymbol)>();
        bool isValid = TraceParameter(
            parameter,
            new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default),
            targets);

        ImmutableArray<string>? keys = null;
        var method = GetDisplayName((IMethodSymbol)parameter.ContainingSymbol);
        if (isValid && targets.Count < 1)
        {
            Diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.UnboundDelegateParameterId,
                $"Inline kernel captures the delegate parameter " +
                $"'{parameter.Name}' of '{method}', but no call of '{method}' " +
                $"passes a lambda expression or a static method for it, so no " +
                $"kernel can be compiled for it.",
                capture.Reference.GetLocation()));
        }
        else if (isValid)
        {
            keys = CreateKeys(parameter, method, targets);
        }
        _keys.Add(parameter, keys);
        return keys;
    }

    /// <summary>
    /// Creates a key for each distinct target and registers the delegates
    /// passed at all call sites under the keys of their targets.
    /// </summary>
    private ImmutableArray<string> CreateKeys(
        IParameterSymbol parameter,
        string method,
        List<(ISymbol Target, ArgumentSyntax Argument, ITypeSymbol Type)> targets)
    {
        var keys = ImmutableArray.CreateBuilder<string>();
        var keysByTarget = new Dictionary<ISymbol, string>(
            SymbolEqualityComparer.Default);
        foreach (var (target, argument, type) in targets)
        {
            if (!keysByTarget.TryGetValue(target, out var key))
            {
                key = $"{method}.{parameter.Name}#{_numKeys++}";
                keysByTarget.Add(target, key);
                keys.Add(key);
            }

            if (!Registrations.TryGetValue(argument, out var registration))
            {
                registration = new DelegateRegistration(type, []);
                Registrations.Add(argument, registration);
            }
            if (!registration.Keys.Contains(key))
                registration.Keys.Add(key);
        }
        return keys.ToImmutable();
    }

    /// <summary>
    /// Traces the arguments of all calls of the method declaring the given
    /// delegate parameter.
    /// </summary>
    /// <param name="parameter">The delegate parameter.</param>
    /// <param name="visited">The parameters traced so far.</param>
    /// <param name="targets">
    /// The collected targets, together with the arguments they are passed by
    /// and the delegate types at these arguments.
    /// </param>
    /// <returns>True if all arguments could be traced.</returns>
    private bool TraceParameter(
        IParameterSymbol parameter,
        HashSet<IParameterSymbol> visited,
        List<(ISymbol, ArgumentSyntax, ITypeSymbol)> targets)
    {
        // Forwarding cycles add no further targets
        if (!visited.Add(parameter))
            return true;

        var method = (IMethodSymbol)parameter.ContainingSymbol;
        bool isValid = true;
        foreach (var name in FindReferences(method))
        {
            var model = compilation.GetSemanticModel(name.SyntaxTree);
            if (GetInvocation(name) is not { } invocation
                || model.GetOperation(invocation) is not IInvocationOperation call)
            {
                var display = GetDisplayName(method);
                Diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnostic.UntraceableDelegateArgumentId,
                    $"'{display}' is used as a delegate here, so the delegates " +
                    $"passed to it for '{parameter.Name}' cannot be traced. Call " +
                    $"'{display}' directly instead.",
                    name.GetLocation()));
                isValid = false;
                continue;
            }

            var argument = call.Arguments.FirstOrDefault(
                a => a.Parameter?.Ordinal == parameter.Ordinal);
            isValid &= TraceArgument(
                parameter,
                invocation,
                argument,
                visited,
                targets);
        }
        return isValid;
    }

    /// <summary>
    /// Traces a single argument passed to the given delegate parameter: a
    /// non-capturing lambda expression or anonymous method, a static method
    /// group, or a delegate parameter of the calling method that is traced to
    /// the callers of that method in turn.
    /// </summary>
    private bool TraceArgument(
        IParameterSymbol parameter,
        InvocationExpressionSyntax invocation,
        IArgumentOperation? argument,
        HashSet<IParameterSymbol> visited,
        List<(ISymbol, ArgumentSyntax, ITypeSymbol)> targets)
    {
        var method = GetDisplayName((IMethodSymbol)parameter.ContainingSymbol);
        if (argument is not
            {
                ArgumentKind: not ArgumentKind.DefaultValue,
                Syntax: ArgumentSyntax syntax,
            })
        {
            return ReportUntraceable(method, parameter, invocation);
        }

        var value = argument.Value;
        while (value is IConversionOperation { Operand.Type.TypeKind: TypeKind.Delegate }
            conversion)
        {
            value = conversion.Operand;
        }

        switch (value)
        {
            case IDelegateCreationOperation
            {
                Target: IAnonymousFunctionOperation lambda,
            }:
                if (!ValidateLambda(method, parameter, lambda))
                    return false;
                targets.Add((lambda.Symbol, syntax, argument.Parameter!.Type));
                return true;
            case IDelegateCreationOperation
            {
                Target: IMethodReferenceOperation reference,
            }:
                if (!ValidateMethodGroup(method, parameter, reference, syntax))
                    return false;
                targets.Add((reference.Method, syntax, argument.Parameter!.Type));
                return true;
            case IParameterReferenceOperation { Parameter: var forwarded }
                when IsBindableParameter(compilation, forwarded):
                return TraceParameter(forwarded, visited, targets);
            default:
                return ReportUntraceable(method, parameter, syntax);
        }
    }

    /// <summary>
    /// Reports an argument that cannot be traced to a lambda expression, an
    /// anonymous method or a static method.
    /// </summary>
    private bool ReportUntraceable(
        string method,
        IParameterSymbol parameter,
        SyntaxNode location)
    {
        Diagnostics.Add(new KernelDiagnostic(
            KernelDiagnostic.UntraceableDelegateArgumentId,
            $"The delegate passed to '{method}' for '{parameter.Name}' cannot be " +
            $"traced to a lambda expression, an anonymous method or a static " +
            $"method. Pass one of them directly, or a delegate parameter of the " +
            $"calling method that its callers pass one of them to.",
            location.GetLocation()));
        return false;
    }

    /// <summary>
    /// Reports a lambda expression or anonymous method passed to a delegate
    /// parameter that captures variables, which cannot be bound to a kernel,
    /// or that is declared in a generic method or type.
    /// </summary>
    private bool ValidateLambda(
        string method,
        IParameterSymbol parameter,
        IAnonymousFunctionOperation lambda)
    {
        if (FindGenericContainer(lambda.Symbol.ContainingSymbol) is { } generic)
        {
            Diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.GenericContextId,
                $"The lambda passed to '{method}' for '{parameter.Name}' cannot be " +
                $"bound to a kernel because it is declared inside the generic " +
                $"{(generic is IMethodSymbol ? "method" : "type")} " +
                $"'{generic.ToDisplayString()}'. Declare it in a non-generic " +
                $"method instead.",
                lambda.Syntax.GetLocation()));
            return false;
        }

        bool isValid = true;
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var (symbol, reference) in KernelExtractor.EnumerateOuterReferences(
            lambda,
            lambda.Syntax))
        {
            // Captures of called local functions are captured by the lambda
            var captured = symbol is IMethodSymbol callee
                ? KernelExtractor.FindLocalFunctionCapture(compilation, callee)
                : symbol;
            if (captured is null || !seen.Add(captured))
                continue;

            Diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.CapturingDelegateArgumentId,
                $"The lambda passed to '{method}' for '{parameter.Name}' must not " +
                $"capture variables, but it captures " +
                $"'{KernelExtractor.GetCaptureName(captured)}'. Pass the values " +
                $"as separate arguments instead.",
                reference.GetLocation()));
            isValid = false;
        }
        return isValid;
    }

    /// <summary>
    /// Reports a method group passed to a delegate parameter that refers to an
    /// instance method, whose delegate captures the instance, or that depends on
    /// type parameters.
    /// </summary>
    private bool ValidateMethodGroup(
        string method,
        IParameterSymbol parameter,
        IMethodReferenceOperation reference,
        ArgumentSyntax syntax)
    {
        var target = reference.Method;
        string? message = null;
        string id = KernelDiagnostic.UntraceableDelegateArgumentId;
        if (reference.Instance is not null || !target.IsStatic)
        {
            message =
                $"The method '{target.Name}' passed to '{method}' for " +
                $"'{parameter.Name}' is not static, so its delegate captures an " +
                $"instance. Pass a static method or a lambda expression instead.";
        }
        else if (IsOpen(target))
        {
            id = KernelDiagnostic.GenericContextId;
            message =
                $"The method '{target.Name}' passed to '{method}' for " +
                $"'{parameter.Name}' depends on type parameters of the calling " +
                $"context, so it cannot be bound to a kernel. Pass a method " +
                $"without open type parameters instead.";
        }

        if (message is null)
            return true;
        Diagnostics.Add(new KernelDiagnostic(id, message, syntax.GetLocation()));
        return false;
    }

    /// <summary>
    /// Returns all references to the given method in the compilation.
    /// </summary>
    private IEnumerable<SimpleNameSyntax> FindReferences(IMethodSymbol method)
    {
        var definition = method.OriginalDefinition;
        foreach (var tree in compilation.SyntaxTrees)
        {
            SemanticModel? model = null;
            foreach (var name in tree.GetRoot().DescendantNodes()
                .OfType<SimpleNameSyntax>())
            {
                if (name.Identifier.ValueText != method.Name)
                    continue;

                model ??= compilation.GetSemanticModel(tree);
                if (model.GetSymbolInfo(name).Symbol is IMethodSymbol symbol
                    && SymbolEqualityComparer.Default.Equals(
                        (symbol.ReducedFrom ?? symbol).OriginalDefinition,
                        definition))
                {
                    yield return name;
                }
            }
        }
    }

    /// <summary>
    /// Returns the invocation whose invoked expression is the given name, or
    /// null if the name refers to the method without invoking it.
    /// </summary>
    private static InvocationExpressionSyntax? GetInvocation(SimpleNameSyntax name)
    {
        ExpressionSyntax expression = name.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Name == name => access,
            MemberBindingExpressionSyntax binding when binding.Name == name =>
                binding,
            _ => name,
        };
        return expression.Parent is InvocationExpressionSyntax invocation
            && invocation.Expression == expression
            ? invocation
            : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given captured symbol is a
    /// delegate parameter of a method or local function that can be bound to
    /// the targets passed by the callers of that method: it is passed by value
    /// and never assigned in the method.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation.</param>
    /// <param name="symbol">The captured symbol.</param>
    public static bool IsBindableParameter(Compilation compilation, ISymbol symbol)
    {
        if (symbol is not IParameterSymbol
            {
                RefKind: RefKind.None,
                Type.TypeKind: TypeKind.Delegate,
                ContainingSymbol: IMethodSymbol
                {
                    MethodKind: MethodKind.Ordinary or MethodKind.LocalFunction,
                } method,
            } parameter)
        {
            return false;
        }

        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            SyntaxNode? body = declaration switch
            {
                BaseMethodDeclarationSyntax m =>
                    (SyntaxNode?)m.Body ?? m.ExpressionBody?.Expression,
                LocalFunctionStatementSyntax f =>
                    (SyntaxNode?)f.Body ?? f.ExpressionBody?.Expression,
                _ => null,
            };
            if (body is null)
                continue;

            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            var dataFlow = body is ExpressionSyntax expression
                ? model.AnalyzeDataFlow(expression)
                : model.AnalyzeDataFlow((StatementSyntax)body);
            if (dataFlow is not { Succeeded: true }
                || dataFlow.WrittenInside.Contains(
                    parameter,
                    SymbolEqualityComparer.Default))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns the name of a method for diagnostics and keys, qualified by its
    /// containing type (e.g. <c>Host.UnaryOp</c>).
    /// </summary>
    /// <param name="method">The method.</param>
    public static string GetDisplayName(IMethodSymbol method) =>
        $"{method.ContainingType.ToDisplayString()}.{method.Name}";

    /// <summary>
    /// Returns the innermost generic method or type containing the given symbol
    /// (including the symbol itself), or null if there is none.
    /// </summary>
    private static ISymbol? FindGenericContainer(ISymbol? symbol)
    {
        for (; symbol is not null and not INamespaceSymbol;
            symbol = symbol.ContainingSymbol)
        {
            if (symbol is IMethodSymbol { TypeParameters.Length: > 0 }
                or INamedTypeSymbol { TypeParameters.Length: > 0 })
            {
                return symbol;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given method or one of its
    /// containing types has type arguments that are type parameters.
    /// </summary>
    private static bool IsOpen(IMethodSymbol method) =>
        method.TypeArguments.Any(IsOpen)
        || method.ContainingType is { } type && IsOpen(type);

    /// <summary>
    /// Returns <see langword="true"/> if the given type contains type
    /// parameters.
    /// </summary>
    private static bool IsOpen(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => IsOpen(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.Any(IsOpen)
                || named.ContainingType is { } outer && IsOpen(outer),
            _ => false,
        };
}
