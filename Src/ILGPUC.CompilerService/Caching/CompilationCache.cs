using System.Security.Cryptography;
using System.Text;
using ILGPUC.Compilers;
using Microsoft.Extensions.Caching.Memory;

namespace ILGPUC.CompilerService.Caching;

/// <summary>
/// An in-memory cache for compilation results, keyed by a hash of the
/// compile request parameters.
/// </summary>
public sealed class CompilationCache : ICache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _expiration;

    /// <summary>
    /// Constructs a new compilation cache.
    /// </summary>
    /// <param name="cache">The underlying memory cache.</param>
    /// <param name="configuration">
    /// The application configuration used to read the
    /// <c>Cache:ExpirationMinutes</c> setting (default: 60).
    /// </param>
    public CompilationCache(IMemoryCache cache, IConfiguration configuration)
    {
        _cache = cache;
        var minutes = configuration.GetValue("Cache:ExpirationMinutes", 60);
        _expiration = TimeSpan.FromMinutes(minutes);
    }

    /// <inheritdoc/>
    public CompilationResult? Get(string key)
    {
        if (_cache.TryGetValue(key, out CompilationResult? result))
            return result;
        return null;
    }

    /// <inheritdoc/>
    public void Set(string key, CompilationResult result)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _expiration,
        };
        _cache.Set(key, result, options);
    }

    /// <summary>
    /// Computes a deterministic cache key for the given compile request.
    /// </summary>
    /// <remarks>
    /// The key is a SHA-256 hash of the source code, CUDA flags, include
    /// paths, and compiler directives, prefixed with the target and output
    /// type.
    /// </remarks>
    /// <param name="request">The compile request.</param>
    /// <returns>A unique string key identifying the request.</returns>
    public static string ComputeKey(CompileRequest request)
    {
        var sb = new StringBuilder();
        sb.Append(request.SourceCode);
        sb.Append('\0');
        sb.Append(request.CudaFlags ?? string.Empty);

        if (request.IncludePaths is { Length: > 0 })
        {
            sb.Append('\0');
            sb.AppendJoin('\x1F', request.IncludePaths);
        }

        if (request.CompilerDirectives is { Length: > 0 })
        {
            sb.Append('\0');
            sb.AppendJoin('\x1F', request.CompilerDirectives);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        var hashString = Convert.ToHexStringLower(hash);
        return $"{request.Target}_{request.OutputType}_{hashString}";
    }
}
