using System.Text;

namespace DeltaZulu.LogCluster;

internal readonly record struct CandidateKey : IEquatable<CandidateKey>
{
    private readonly string _value;

    public CandidateKey(ReadOnlySpan<int> anchors)
    {
        if (anchors.Length == 0)
        {
            _value = string.Empty;
            return;
        }

        var builder = new StringBuilder(anchors.Length * 4);
        for (var i = 0; i < anchors.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\u001f');
            }

            builder.Append(anchors[i]);
        }
        _value = builder.ToString();
    }

    public override string ToString() => _value ?? string.Empty;
}
