using System.Text;

namespace DeltaZulu.LogCluster;

public sealed class TokenDictionary
{
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private readonly List<string> _tokens = [];

    public TokenDictionary()
    {
        _lookup = _ids.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public int Count => _tokens.Count;

    public string this[int id] => _tokens[id];

    public int GetOrAdd(string message, int start, int length)
    {
        var span = message.AsSpan(start, length);
        if (_lookup.TryGetValue(span, out var id))
        {
            return id;
        }

        var token = span.ToString();
        id = _tokens.Count;
        _ids.Add(token, id);
        _tokens.Add(token);
        return id;
    }

    public string Join(IReadOnlyList<int> tokenIds) => tokenIds.Count switch {
        0 => string.Empty,
        1 => _tokens[tokenIds[0]],
        _ => JoinMany(tokenIds),
    };

    private string JoinMany(IReadOnlyList<int> tokenIds)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < tokenIds.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(_tokens[tokenIds[i]]);
        }
        return builder.ToString();
    }
}
