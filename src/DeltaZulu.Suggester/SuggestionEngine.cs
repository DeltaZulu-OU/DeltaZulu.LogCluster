using DeltaZulu.LogCluster;
using DeltaZulu.Parse;

namespace DeltaZulu.Suggester;

/// <summary>
/// Gap suggestion engine that recognizes parser motifs from mined samples.
/// </summary>
public sealed class SuggestionEngine : IGapSuggestionEngine
{
    private readonly IParserCatalog _catalog;

    /// <summary>Initializes a new engine over the supplied parser catalog.</summary>
    /// <param name="catalog">The Normalize catalog that supplies parser metadata and validators.</param>
    public SuggestionEngine(IParserCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Gets a shared stateless instance backed by the default Normalize catalog.</summary>
    public static SuggestionEngine Instance { get; } = new(ParserCatalog.Instance);

    /// <inheritdoc />
    public string RestParser => _catalog.RestParserName;

    /// <inheritdoc />
    public string WordParser => _catalog.WordParserName;

    /// <inheritdoc />
    public int Priority(string parser) =>
        _catalog.TryGetParser(parser, out var descriptor) ? descriptor.Priority : int.MaxValue;

    /// <inheritdoc />
    public IEnumerable<string> Recognize(string sample)
    {
        foreach (var parser in _catalog.Parsers)
        {
            if (parser.CanInferFromSample && _catalog.IsFullMatch(parser.Name, sample))
            {
                yield return parser.Name;
            }
        }
    }
}
