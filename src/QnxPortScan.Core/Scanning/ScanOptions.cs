namespace QnxPortScan.Core.Scanning;

public sealed class ScanOptions
{
    public static readonly string[] DefaultExtensions =
        [".c", ".h", ".cc", ".cpp", ".cxx", ".c++", ".hh", ".hpp", ".hxx", ".inl", ".ipp"];

    public static readonly string[] DefaultExcludedDirectories =
        [".git", ".svn", ".hg", "node_modules", "build", "out", "cmake-build-debug", "cmake-build-release"];

    public IReadOnlyCollection<string> Extensions { get; init; } = DefaultExtensions;
    public IReadOnlyCollection<string> ExcludedDirectories { get; init; } = DefaultExcludedDirectories;

    /// <summary>Extra path fragments (relative, '/' separated) to skip, e.g. "third_party/".</summary>
    public IReadOnlyCollection<string> ExcludePaths { get; init; } = [];

    /// <summary>Report matches inside Linux-only #ifdef branches (as notes). Off by default.</summary>
    public bool IncludeGuarded { get; init; }

    public long MaxFileBytes { get; init; } = 4 * 1024 * 1024;
}
