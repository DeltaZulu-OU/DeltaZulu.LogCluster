using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace DeltaZulu.Suggester;

public static partial class LiblognormMotifs
{
    public const string DateIso = "date-iso";
    public const string Float = "float";
    public const string Ipv4 = "ipv4";
    public const string Ipv6 = "ipv6";
    public const string Mac48 = "mac48";
    public const string Number = "number";
    public const string Rest = "rest";
    public const string Word = "word";
    private static readonly Regex FloatRegex = FloatPattern();
    private static readonly Regex IntegerRegex = IntegerPattern();
    private static readonly Regex Ipv4Regex = Ipv4Pattern();
    private static readonly Regex Mac48Regex = Mac48Pattern();
    private static readonly Regex WordRegex = WordPattern();

    public static int Priority(string parser) => parser switch {
        Ipv4 or Ipv6 or Mac48 => 0,
        DateIso => 1,
        Number or Float => 2,
        Word => 10,
        Rest => 20,
        _ => 30,
    };

    public static IEnumerable<string> Recognize(string sample)
    {
        if (Ipv4Regex.IsMatch(sample) && IPAddress.TryParse(sample, out var ipv4) && ipv4.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            yield return Ipv4;
        }

        if (sample.Contains(':', StringComparison.Ordinal) && IPAddress.TryParse(sample, out var ipv6) && ipv6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            yield return Ipv6;
        }

        if (Mac48Regex.IsMatch(sample))
        {
            yield return Mac48;
        }

        if (IntegerRegex.IsMatch(sample))
        {
            yield return Number;
        }

        if (FloatRegex.IsMatch(sample))
        {
            yield return Float;
        }

        if (DateOnly.TryParseExact(sample, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            yield return DateIso;
        }

        if (WordRegex.IsMatch(sample))
        {
            yield return Word;
        }
    }

    [GeneratedRegex("^[+-]?(?:[0-9]+\\.[0-9]*|[0-9]*\\.[0-9]+)(?:[eE][+-]?[0-9]+)?$")]
    private static partial Regex FloatPattern();

    [GeneratedRegex("^[+-]?[0-9]+$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex("^(?:[0-9]{1,3}\\.){3}[0-9]{1,3}$")]
    private static partial Regex Ipv4Pattern();

    [GeneratedRegex("^[0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5}$")]
    private static partial Regex Mac48Pattern();

    [GeneratedRegex("^[^\\s]+$")]
    private static partial Regex WordPattern();
}
