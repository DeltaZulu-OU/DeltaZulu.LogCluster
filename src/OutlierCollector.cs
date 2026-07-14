namespace DeltaZulu.LogCluster;

/// <summary>
/// Tracks lines that matched no surviving candidate (--outliers), analogous to LogClusterC's
/// outliers.c. Count is exact; Samples is bounded so a pathological input can't blow up memory
/// just for a diagnostic report.
/// </summary>
internal sealed class OutlierCollector
{
    private readonly List<string> _samples;
    private readonly int maxSamples;

    public OutlierCollector(int maxSamples)
    {
        this.maxSamples = maxSamples;
        _samples = new(maxSamples);
    }

    public int Count { get; private set; }
    public IReadOnlyList<string> Samples => _samples;

    public void Observe(string line)
    {
        Count++;
        if (_samples.Count < maxSamples)
        {
            _samples.Add(line);
        }
    }
}
