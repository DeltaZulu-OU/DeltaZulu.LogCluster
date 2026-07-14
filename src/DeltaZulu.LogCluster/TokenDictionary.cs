using System.Text;

namespace DeltaZulu.LogCluster;

/// <summary>
/// Assigns stable integer identifiers to distinct token strings and resolves identifiers back to text.
/// </summary>
public sealed class TokenDictionary
{
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private readonly List<string> _tokens = [];

    /// <summary>
    /// Initializes a new empty token dictionary.
    /// </summary>
    public TokenDictionary()
    {
        _lookup = _ids.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Gets the number of distinct tokens stored in the dictionary.
    /// </summary>
    public int Count => _tokens.Count;

    /// <summary>
    /// Gets the token text associated with a token identifier.
    /// </summary>
    /// <param name="id">The token identifier to resolve.</param>
    /// <returns>The token text stored for <paramref name="id" />.</returns>
    public string this[int id] => _tokens[id];

    /// <summary>
    /// Gets the identifier for a token slice, adding the token when it has not been seen before.
    /// </summary>
    /// <param name="message">The message containing the token slice.</param>
    /// <param name="start">The zero-based start offset of the token within <paramref name="message" />.</param>
    /// <param name="length">The length of the token slice.</param>
    /// <returns>The stable identifier assigned to the token text.</returns>
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

    /// <summary>
    /// Joins token identifiers back into a space-delimited text fragment.
    /// </summary>
    /// <param name="tokenIds">The token identifiers to resolve and join.</param>
    /// <returns>The resolved tokens separated by single spaces, or an empty string for no tokens.</returns>
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
