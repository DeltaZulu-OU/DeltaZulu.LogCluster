namespace DeltaZulu.LogCluster;

internal sealed class NullGapSuggestionEngine : IGapSuggestionEngine
{
    public static NullGapSuggestionEngine Instance { get; } = new();

    public string WordParser => "word";

    public string RestParser => "rest";

    public IEnumerable<string> Recognize(string sample) => [];

    public int Priority(string parser) => 0;
}
