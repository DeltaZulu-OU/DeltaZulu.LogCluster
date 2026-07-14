namespace DeltaZulu.LogCluster;

public sealed class GapStatistics
{
    private readonly Dictionary<string, int> _parserVotes = new(StringComparer.Ordinal);
    private readonly List<string> _samples;
    private readonly int maxSamples;

    public GapStatistics(int maxSamples)
    {
        this.maxSamples = maxSamples;
        _samples = new(maxSamples);
    }

    public int MaxWords { get; private set; }
    public int MinWords { get; private set; } = int.MaxValue;
    public int Observations { get; private set; }

    public void Observe(IReadOnlyList<int> words, TokenDictionary dictionary)
    {
        Observations++;
        MinWords = Math.Min(MinWords, words.Count);
        MaxWords = Math.Max(MaxWords, words.Count);
        if (words.Count == 0)
        {
            return;
        }

        var sample = dictionary.Join(words);
        if (_samples.Count < maxSamples && !_samples.Contains(sample, StringComparer.Ordinal))
        {
            _samples.Add(sample);
        }
        foreach (var parser in LiblognormMotifs.Recognize(sample))
        {
            _parserVotes[parser] = _parserVotes.GetValueOrDefault(parser) + 1;
        }
    }

    public GapOutput ToOutput()
    {
        var min = MinWords == int.MaxValue ? 0 : MinWords;
        var suggestion = _parserVotes.OrderByDescending(v => v.Value).ThenBy(v => LiblognormMotifs.Priority(v.Key)).FirstOrDefault();
        var parser = suggestion.Key;
        var confidence = Observations == 0 || string.IsNullOrEmpty(parser) ? 0 : suggestion.Value / (double)Observations;
        if (MaxWords > 1 && confidence < 1.0)
        {
            parser = LiblognormMotifs.Rest;
        }

        return new GapOutput(min, MaxWords, Observations, _samples.ToArray(), string.IsNullOrEmpty(parser) ? null : parser, confidence);
    }
}
