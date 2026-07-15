namespace DeltaZulu.LogCluster;

/// <summary>
/// Supplies parser suggestions for variable gaps discovered by the mining algorithm.
/// </summary>
public interface IGapSuggestionEngine
{
    /// <summary>Gets the parser name to use for a single-token fallback gap.</summary>
    string WordParser { get; }

    /// <summary>Gets the parser name to use for a variable-width fallback gap.</summary>
    string RestParser { get; }

    /// <summary>Returns parser names recognized for a gap sample.</summary>
    /// <param name="sample">The gap sample text to inspect.</param>
    /// <returns>The parser names that match <paramref name="sample" />.</returns>
    IEnumerable<string> Recognize(string sample);

    /// <summary>Returns a stable ordering priority for parser names; lower values win ties.</summary>
    /// <param name="parser">The parser name to rank.</param>
    /// <returns>The priority assigned to <paramref name="parser" />.</returns>
    int Priority(string parser);
}
