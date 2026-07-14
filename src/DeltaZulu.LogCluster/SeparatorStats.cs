namespace DeltaZulu.LogCluster;

/// <summary>
/// Tracks the whitespace actually observed at one anchor boundary across matching records, so
/// rendering can reproduce a delimiter other than a single ASCII space (e.g. CSV/pipe-separated
/// logs) instead of always rejoining with ' '.
/// </summary>
internal sealed class SeparatorStats
{
    private readonly Dictionary<string, int> _votes = new(StringComparer.Ordinal);

    public string Modal() => _votes.Count == 0
        ? " "
        : _votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).First().Key;

    public void Observe(string separator) => _votes[separator] = _votes.GetValueOrDefault(separator) + 1;
}
