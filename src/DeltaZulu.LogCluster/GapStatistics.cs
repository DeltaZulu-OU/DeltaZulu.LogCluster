namespace DeltaZulu.LogCluster;

/// <summary>
/// Accumulates word-count, sample, and parser evidence for one variable gap.
/// </summary>
public sealed class GapStatistics
{
    private readonly Dictionary<string, int> _parserVotes = new(StringComparer.Ordinal);
    private readonly List<string> _samples;
    private readonly int maxSamples;

    /// <summary>
    /// Initializes gap statistics with a bounded sample count.
    /// </summary>
    /// <param name="maxSamples">The maximum number of distinct gap samples to retain.</param>
    public GapStatistics(int maxSamples)
    {
        this.maxSamples = maxSamples;
        _samples = new(maxSamples);
    }

    /// <summary>Gets the maximum number of words observed in the gap.</summary>
    public int MaxWords { get; private set; }
    /// <summary>Gets the minimum number of words observed in the gap.</summary>
    public int MinWords { get; private set; } = int.MaxValue;
    /// <summary>Gets the number of observations recorded for the gap.</summary>
    public int Observations { get; private set; }

    /// <summary>
    /// Records one observed gap value and updates parser suggestions from its sample text.
    /// </summary>
    /// <param name="words">The token identifiers that appeared in the gap.</param>
    /// <param name="dictionary">The dictionary used to resolve token identifiers into sample text.</param>
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

    /// <summary>
    /// Converts the accumulated statistics into immutable output data.
    /// </summary>
    /// <returns>A gap output record containing word ranges, samples, and parser confidence.</returns>
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
