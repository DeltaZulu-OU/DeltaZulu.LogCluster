namespace DeltaZulu.LogCluster;

internal sealed class NullGapSuggestionEngine : IGapSuggestionEngine
{
    public static NullGapSuggestionEngine Instance { get; } = new();

    public string RestParser => "rest";
    public string WordParser => "word";

    public int Priority(string parser) => 0;

    public IEnumerable<string> Recognize(string sample) => [];
}
