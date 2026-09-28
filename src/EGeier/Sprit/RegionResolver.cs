using System.Globalization;
using System.Text;

namespace EGeier.Sprit;

/// <summary>A region matched by <see cref="RegionResolver"/>.</summary>
/// <param name="Region">The matched state or district.</param>
/// <param name="MatchedBy">What matched: the region name, a municipality name or a postal code.</param>
public sealed record RegionMatch(Region Region, RegionMatchKind MatchedBy);

/// <summary>How a <see cref="RegionMatch"/> was found.</summary>
public enum RegionMatchKind
{
    /// <summary>The name of the state or district matched exactly.</summary>
    ExactName,

    /// <summary>The name of the state or district matched partially.</summary>
    PartialName,

    /// <summary>A municipality in the district matched (requires regions loaded with cities).</summary>
    Municipality,

    /// <summary>A postal code in the district matched.</summary>
    PostalCode,
}

/// <summary>
/// Resolves free-text region names such as "Steiermark", "Bezirk Mödling", "Graz" or "Favoriten"
/// to the codes the region search needs.
/// </summary>
public static class RegionResolver
{
    /// <summary>
    /// Returns the best matching regions for <paramref name="query"/>, best first. Returns an empty
    /// list if nothing matches and several entries if the query is ambiguous.
    /// </summary>
    /// <param name="regions">Regions from <see cref="ISpritClient.GetRegionsAsync"/>, ideally with cities.</param>
    /// <param name="query">Name of a Bundesland, Bezirk, municipality, or a postal code.</param>
    public static IReadOnlyList<RegionMatch> Resolve(IEnumerable<Region> regions, string query)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var needle = Normalize(query);
        if (needle.Length == 0)
        {
            return [];
        }

        var states = regions.ToList();
        var all = states.Concat(states.SelectMany(s => s.SubRegions)).ToList();

        // Compare verbatim first so "Salzburg Stadt" finds "Salzburg(Stadt)" rather than the state;
        // then without filler words. States come first in 'all', so "Wien" prefers the Bundesland.
        var literal = Normalize(query, dropFillerWords: false);
        var exact = all.FirstOrDefault(r => Normalize(r.Name, dropFillerWords: false) == literal)
            ?? all.FirstOrDefault(r => Normalize(r.Name) == needle);
        if (exact is not null)
        {
            return [new RegionMatch(exact, RegionMatchKind.ExactName)];
        }

        var districts = states.SelectMany(s => s.SubRegions).ToList();

        if (needle.All(char.IsAsciiDigit))
        {
            return [.. districts
                .Where(d => d.PostalCodes.Contains(needle))
                .Select(d => new RegionMatch(d, RegionMatchKind.PostalCode))];
        }

        var byCity = districts
            .Where(d => d.Cities.Any(c => Normalize(c) == needle))
            .Select(d => new RegionMatch(d, RegionMatchKind.Municipality))
            .ToList();
        if (byCity.Count > 0)
        {
            return byCity;
        }

        return [.. all
            .Where(r => ContainsWord(Normalize(r.Name), needle))
            .Select(r => new RegionMatch(r, RegionMatchKind.PartialName))];
    }

    /// <summary>
    /// Lower-cases, folds umlauts and diacritics, and drops filler words such as "Bezirk" and "(Stadt)",
    /// so that "Bezirk Mödling", "moedling" and "Mödling" compare equal.
    /// </summary>
    internal static string Normalize(string? value, bool dropFillerWords = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("st.", "sankt ", StringComparison.Ordinal);

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        var words = builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !dropFillerWords || w is not ("bezirk" or "politischer" or "bundesland" or "stadt"));
        return string.Join(' ', words);
    }

    /// <summary>Single words match as word prefix ("favor" → "Favoriten"), phrases on word boundaries.</summary>
    private static bool ContainsWord(string haystack, string needle) =>
        needle.Contains(' ', StringComparison.Ordinal)
            ? $" {haystack} ".Contains($" {needle} ", StringComparison.Ordinal)
            : haystack.Split(' ').Any(w => w.StartsWith(needle, StringComparison.Ordinal));
}
