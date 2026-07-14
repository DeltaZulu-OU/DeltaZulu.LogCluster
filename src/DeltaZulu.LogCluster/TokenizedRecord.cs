using System.Text;

namespace DeltaZulu.LogCluster;

/// <summary>
/// Separators is aligned with Tokens: Separators[i] is the whitespace run immediately before
/// Tokens[i], and Separators[^1] (length Tokens.Length + 1) is the trailing whitespace after the
/// last token. This lets rendering reproduce the delimiter actually observed at each anchor
/// boundary instead of always rejoining with a single ASCII space.
/// </summary>
/// <param name="SequenceNumber">The input sequence number for the tokenized record.</param>
/// <param name="Tokens">The token identifiers in message order.</param>
/// <param name="Separators">The whitespace separators surrounding the tokens.</param>
internal sealed record TokenizedRecord(long SequenceNumber, int[] Tokens, string[] Separators)
{
    /// <summary>
    /// Losslessly rebuilds the original message text for --outliers reporting: Tokens and
    /// Separators together capture exactly what Tokenize() consumed.
    /// </summary>
    /// <param name="dictionary">The dictionary used to resolve token identifiers.</param>
    /// <returns>The reconstructed original message text.</returns>
    public string Reconstruct(TokenDictionary dictionary)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < Tokens.Length; i++)
        {
            builder.Append(Separators[i]);
            builder.Append(dictionary[Tokens[i]]);
        }
        builder.Append(Separators[^1]);
        return builder.ToString();
    }

    /// <summary>
    /// Shared by both mining strategies: "materialize" calls ToArray() on this once and caches
    /// the result; "stream" leaves it lazy and re-enumerates recordSource() through it once per
    /// pass, so records are tokenized on the fly and never all held in memory at once.
    /// </summary>
    /// <param name="records">The records to tokenize.</param>
    /// <param name="dictionary">The dictionary used to resolve token identifiers.</param>
    /// <param name="maxRecords">The maximum number of records allowed before an exception is thrown.</param>
    /// <param name="maxInputBytes">The maximum cumulative message length allowed before an exception is thrown.</param>
    /// <returns>A lazy sequence of tokenized records.</returns>
    public static IEnumerable<TokenizedRecord> Stream(IEnumerable<LogRecord> records, TokenDictionary dictionary, long maxRecords, long maxInputBytes)
    {
        long recordCount = 0;
        long totalBytes = 0;
        foreach (var record in records)
        {
            recordCount++;
            totalBytes += record.Message.Length;
            if (recordCount > maxRecords)
            {
                throw new LogClusterInputTooLargeException($"input exceeds --max-records ({maxRecords}); use a smaller input or increase the limit");
            }
            if (totalBytes > maxInputBytes)
            {
                throw new LogClusterInputTooLargeException($"input exceeds --max-input-bytes ({maxInputBytes}); use a smaller input or increase the limit");
            }
            var (tokens, separators) = Tokenize(record.Message, dictionary);
            yield return new TokenizedRecord(record.SequenceNumber, tokens, separators);
        }
    }

    private static (int[] Tokens, string[] Separators) Tokenize(string message, TokenDictionary dictionary)
    {
        var tokens = new List<int>();
        var separators = new List<string>();
        var separatorStart = 0;
        var start = -1;
        for (var i = 0; i <= message.Length; i++)
        {
            if (i < message.Length && !char.IsWhiteSpace(message[i]))
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                separators.Add(message[separatorStart..start]);
                tokens.Add(dictionary.GetOrAdd(message, start, i - start));
                separatorStart = i;
                start = -1;
            }
        }
        separators.Add(message[separatorStart..]);
        return (tokens.ToArray(), separators.ToArray());
    }
}
