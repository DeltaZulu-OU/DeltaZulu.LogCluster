using DeltaZulu.LogCluster;
using DeltaZulu.Normalize;

namespace DeltaZulu.Suggester;

/// <summary>
/// Gap suggestion engine that recognizes liblognorm parser motifs from mined samples.
/// Parser names, priorities, and whole-sample validation are sourced from
/// <see cref="DeltaZulu.Normalize.ILiblognormParserCatalog" /> so the suggester does not
/// duplicate liblognorm parser syntax.
/// </summary>
public sealed class LiblognormSuggestionEngine : IGapSuggestionEngine
{
    private readonly ILiblognormParserCatalog _catalog;

    /// <summary>Initializes a new engine over the supplied parser catalog.</summary>
    /// <param name="catalog">The Normalize catalog that supplies parser metadata and validators.</param>
    public LiblognormSuggestionEngine(ILiblognormParserCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Gets a shared stateless instance backed by the default Normalize catalog.</summary>
    public static LiblognormSuggestionEngine Instance { get; } = new(LiblognormParserCatalog.Instance);

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
