using DeltaZulu.LogCluster;

namespace DeltaZulu.Suggester;

/// <summary>
/// Gap suggestion engine that recognizes liblognorm parser motifs from mined samples.
/// </summary>
public sealed class LiblognormSuggestionEngine : IGapSuggestionEngine
{
    /// <summary>Gets a shared stateless instance of the liblognorm suggestion engine.</summary>
    public static LiblognormSuggestionEngine Instance { get; } = new();

    private LiblognormSuggestionEngine()
    {
    }

    /// <inheritdoc />
    public string WordParser => LiblognormMotifs.Word;

    /// <inheritdoc />
    public string RestParser => LiblognormMotifs.Rest;

    /// <inheritdoc />
    public IEnumerable<string> Recognize(string sample) => LiblognormMotifs.Recognize(sample);

    /// <inheritdoc />
    public int Priority(string parser) => LiblognormMotifs.Priority(parser);
}
