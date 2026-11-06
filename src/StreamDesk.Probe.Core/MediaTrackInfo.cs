using System.Text.RegularExpressions;

namespace StreamDesk.Probe.Core;

public sealed record MediaTrackInfo(
    int Id,
    string RawName,
    string DisplayName,
    string LanguageCode,
    string? Region = null,
    bool IsOriginal = false,
    bool IsAudioDescription = false,
    bool IsSdh = false,
    bool IsDisabled = false,
    string? Channels = null)
{
    public bool IsSelected { get; init; }
}

public sealed record MediaTrackPreference(
    string ContentId,
    ContentKind Kind,
    string? AudioLanguage,
    string? AudioRegion,
    string? AudioTitle,
    string? SubtitleLanguage,
    string? SubtitleRegion,
    string? SubtitleTitle,
    bool SubtitlesDisabled,
    DateTimeOffset UpdatedAt);

public static class MediaTrackParser
{
    private static readonly Dictionary<string, (string Code, string Name)> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["es"] = ("es", "Español"),
        ["spa"] = ("es", "Español"),
        ["spanish"] = ("es", "Español"),
        ["español"] = ("es", "Español"),
        ["espanol"] = ("es", "Español"),
        ["castellano"] = ("es", "Español"),

        ["en"] = ("en", "Inglés"),
        ["eng"] = ("en", "Inglés"),
        ["english"] = ("en", "Inglés"),
        ["inglés"] = ("en", "Inglés"),
        ["ingles"] = ("en", "Inglés"),

        ["fr"] = ("fr", "Francés"),
        ["fre"] = ("fr", "Francés"),
        ["fra"] = ("fr", "Francés"),
        ["french"] = ("fr", "Francés"),
        ["francés"] = ("fr", "Francés"),
        ["frances"] = ("fr", "Francés"),

        ["de"] = ("de", "Alemán"),
        ["ger"] = ("de", "Alemán"),
        ["deu"] = ("de", "Alemán"),
        ["german"] = ("de", "Alemán"),
        ["alemán"] = ("de", "Alemán"),
        ["aleman"] = ("de", "Alemán"),

        ["it"] = ("it", "Italiano"),
        ["ita"] = ("it", "Italiano"),
        ["italian"] = ("it", "Italiano"),
        ["italiano"] = ("it", "Italiano"),

        ["pt"] = ("pt", "Portugués"),
        ["por"] = ("pt", "Portugués"),
        ["portuguese"] = ("pt", "Portugués"),
        ["portugués"] = ("pt", "Portugués"),
        ["portugues"] = ("pt", "Portugués"),

        ["ja"] = ("ja", "Japonés"),
        ["jpn"] = ("ja", "Japonés"),
        ["japanese"] = ("ja", "Japonés"),
        ["japonés"] = ("ja", "Japonés"),
        ["japones"] = ("ja", "Japonés"),

        ["ko"] = ("ko", "Coreano"),
        ["kor"] = ("ko", "Coreano"),
        ["korean"] = ("ko", "Coreano"),
        ["coreano"] = ("ko", "Coreano"),

        ["zh"] = ("zh", "Chino"),
        ["chi"] = ("zh", "Chino"),
        ["zho"] = ("zh", "Chino"),
        ["chinese"] = ("zh", "Chino"),
        ["chino"] = ("zh", "Chino"),

        ["ru"] = ("ru", "Ruso"),
        ["rus"] = ("ru", "Ruso"),
        ["russian"] = ("ru", "Ruso"),
        ["ruso"] = ("ru", "Ruso"),

        ["ar"] = ("ar", "Árabe"),
        ["ara"] = ("ar", "Árabe"),
        ["arabic"] = ("ar", "Árabe"),
        ["árabe"] = ("ar", "Árabe"),
        ["arabe"] = ("ar", "Árabe"),

        ["hi"] = ("hi", "Hindi"),
        ["hin"] = ("hi", "Hindi"),
        ["hindi"] = ("hi", "Hindi"),

        ["nl"] = ("nl", "Holandés"),
        ["dut"] = ("nl", "Holandés"),
        ["nld"] = ("nl", "Holandés"),
        ["dutch"] = ("nl", "Holandés"),
        ["holandés"] = ("nl", "Holandés"),
        ["holandes"] = ("nl", "Holandés"),

        ["pl"] = ("pl", "Polaco"),
        ["pol"] = ("pl", "Polaco"),
        ["polish"] = ("pl", "Polaco"),
        ["polaco"] = ("pl", "Polaco"),

        ["tr"] = ("tr", "Turco"),
        ["tur"] = ("tr", "Turco"),
        ["turkish"] = ("tr", "Turco"),
        ["turco"] = ("tr", "Turco"),

        ["sv"] = ("sv", "Sueco"),
        ["swe"] = ("sv", "Sueco"),
        ["swedish"] = ("sv", "Sueco"),
        ["sueco"] = ("sv", "Sueco"),

        ["no"] = ("no", "Noruego"),
        ["nor"] = ("no", "Noruego"),
        ["norwegian"] = ("no", "Noruego"),
        ["noruego"] = ("no", "Noruego"),

        ["da"] = ("da", "Danés"),
        ["dan"] = ("da", "Danés"),
        ["danish"] = ("da", "Danés"),
        ["danés"] = ("da", "Danés"),
        ["danes"] = ("da", "Danés"),

        ["fi"] = ("fi", "Finlandés"),
        ["fin"] = ("fi", "Finlandés"),
        ["finnish"] = ("fi", "Finlandés"),
        ["finlandés"] = ("fi", "Finlandés"),
        ["finlandes"] = ("fi", "Finlandés")
    };

    public static MediaTrackInfo ParseAudioTrack(MediaTrack track) => ParseTrackCore(track, isSubtitle: false);

    public static MediaTrackInfo ParseSubtitleTrack(MediaTrack track) => ParseTrackCore(track, isSubtitle: true);

    private static MediaTrackInfo ParseTrackCore(MediaTrack track, bool isSubtitle)
    {
        var raw = (track.Name ?? "").Trim();
        if (track.Id == -1 || (isSubtitle && (raw.Equals("Disable", StringComparison.OrdinalIgnoreCase) ||
                                              raw.Equals("Desactivar", StringComparison.OrdinalIgnoreCase) ||
                                              raw.Equals("Desactivados", StringComparison.OrdinalIgnoreCase) ||
                                              string.IsNullOrWhiteSpace(raw))))
        {
            return new MediaTrackInfo(-1, raw, "Desactivados", "", IsDisabled: true);
        }

        var lower = raw.ToLowerInvariant();
        var isOriginal = lower.Contains("original");
        var isAudioDescription = lower.Contains("audio description") || lower.Contains("audiodescripción") ||
                                lower.Contains("audiodescripcion") || Regex.IsMatch(lower, @"\b(ad)\b");
        var isSdh = lower.Contains("cc") || lower.Contains("sdh") || lower.Contains("closed caption") ||
                    lower.Contains("discapacidad auditiva");

        // Channels
        string? channels = null;
        if (Regex.IsMatch(lower, @"\b(5\.1|7\.1|2\.0|stereo|mono|surround)\b"))
        {
            var match = Regex.Match(lower, @"\b(5\.1|7\.1|2\.0|stereo|mono|surround)\b");
            channels = match.Value switch
            {
                "5.1" => "5.1",
                "7.1" => "7.1",
                "2.0" => "2.0",
                "stereo" => "Estéreo",
                "mono" => "Mono",
                "surround" => "Surround",
                _ => match.Value
            };
        }

        // Region / dialect
        string? region = null;
        if (Regex.IsMatch(lower, @"\b(latinoamérica|latinoamerica|latam|latin america|latino|lat)\b"))
            region = "Latinoamérica";
        else if (Regex.IsMatch(lower, @"\b(españa|espana|spain|castellano|castilian|peninsular)\b"))
            region = "España";
        else if (Regex.IsMatch(lower, @"\b(brasil|brazil)\b"))
            region = "Brasil";
        else if (Regex.IsMatch(lower, @"\b(méxico|mexico)\b"))
            region = "México";
        else if (Regex.IsMatch(lower, @"\b(usa|ee\.uu\.|eeuu|united states)\b"))
            region = "EE. UU.";
        else if (Regex.IsMatch(lower, @"\b(uk|reino unido|british)\b"))
            region = "Reino Unido";

        // Language identification
        var code = "";
        var baseName = "";

        // Look for bracketed or parenthesized tags first: [es], [spa], (English), etc.
        var bracketMatches = Regex.Matches(raw, @"[\[\(\{]([a-zA-Z]{2,10})[\]\)\}]");
        foreach (Match match in bracketMatches)
        {
            var candidate = match.Groups[1].Value;
            if (LanguageMap.TryGetValue(candidate, out var found))
            {
                code = found.Code;
                baseName = found.Name;
                break;
            }
        }

        // If not found in brackets, search by word boundaries in raw string
        if (string.IsNullOrEmpty(code))
        {
            foreach (var kvp in LanguageMap)
            {
                if (Regex.IsMatch(lower, $@"\b{Regex.Escape(kvp.Key)}\b"))
                {
                    code = kvp.Value.Code;
                    baseName = kvp.Value.Name;
                    break;
                }
            }
        }

        // Clean display name formulation
        string displayName;
        if (!string.IsNullOrEmpty(baseName))
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(region)) parts.Add(region);
            if (isOriginal) parts.Add("Original");
            if (isAudioDescription) parts.Add("Audiodescripción");
            if (isSdh) parts.Add("CC");
            if (!string.IsNullOrEmpty(channels)) parts.Add(channels);

            displayName = parts.Count > 0 ? $"{baseName} ({string.Join(" · ", parts)})" : baseName;
        }
        else
        {
            // If language wasn't identified, clean up generic VLC names like "Track 1 - [Unknown]"
            var cleaned = Regex.Replace(raw, @"^Track\s+(\d+)", "Pista $1", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(cleaned)) cleaned = $"Pista {track.Id}";
            displayName = cleaned;
        }

        return new MediaTrackInfo(
            track.Id,
            raw,
            displayName,
            code,
            region,
            isOriginal,
            isAudioDescription,
            isSdh,
            IsDisabled: false,
            channels);
    }

    public static MediaTrackInfo? FindBestAudioMatch(
        IEnumerable<MediaTrackInfo> availableTracks,
        MediaTrackPreference? preference,
        string? fallbackLanguage = null)
    {
        var tracks = availableTracks.Where(t => !t.IsDisabled).ToList();
        if (tracks.Count == 0) return null;

        if (preference is not null && !string.IsNullOrWhiteSpace(preference.AudioLanguage))
        {
            // 1. Exact language + region
            if (!string.IsNullOrWhiteSpace(preference.AudioRegion))
            {
                var exactRegion = tracks.FirstOrDefault(t =>
                    string.Equals(t.LanguageCode, preference.AudioLanguage, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.Region, preference.AudioRegion, StringComparison.OrdinalIgnoreCase));
                if (exactRegion is not null) return exactRegion;
            }

            // 2. Exact language match
            var exactLang = tracks.FirstOrDefault(t =>
                string.Equals(t.LanguageCode, preference.AudioLanguage, StringComparison.OrdinalIgnoreCase));
            if (exactLang is not null) return exactLang;

            // 3. Title match
            if (!string.IsNullOrWhiteSpace(preference.AudioTitle))
            {
                var titleMatch = tracks.FirstOrDefault(t =>
                    string.Equals(t.DisplayName, preference.AudioTitle, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.RawName, preference.AudioTitle, StringComparison.OrdinalIgnoreCase));
                if (titleMatch is not null) return titleMatch;
            }
        }

        // 4. Fallback to global user language if provided
        if (!string.IsNullOrWhiteSpace(fallbackLanguage))
        {
            var fallback = tracks.FirstOrDefault(t =>
                string.Equals(t.LanguageCode, fallbackLanguage, StringComparison.OrdinalIgnoreCase));
            if (fallback is not null) return fallback;
        }

        return null;
    }

    public static MediaTrackInfo? FindBestSubtitleMatch(
        IEnumerable<MediaTrackInfo> availableTracks,
        MediaTrackPreference? preference)
    {
        var tracks = availableTracks.ToList();
        if (tracks.Count == 0) return null;

        var disabledTrack = tracks.FirstOrDefault(t => t.IsDisabled || t.Id == -1);

        if (preference is not null)
        {
            if (preference.SubtitlesDisabled)
            {
                return disabledTrack ?? new MediaTrackInfo(-1, "Disable", "Desactivados", "", IsDisabled: true);
            }

            if (!string.IsNullOrWhiteSpace(preference.SubtitleLanguage))
            {
                var nonDisabled = tracks.Where(t => !t.IsDisabled).ToList();

                // 1. Exact language + region
                if (!string.IsNullOrWhiteSpace(preference.SubtitleRegion))
                {
                    var exactRegion = nonDisabled.FirstOrDefault(t =>
                        string.Equals(t.LanguageCode, preference.SubtitleLanguage, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(t.Region, preference.SubtitleRegion, StringComparison.OrdinalIgnoreCase));
                    if (exactRegion is not null) return exactRegion;
                }

                // 2. Exact language
                var exactLang = nonDisabled.FirstOrDefault(t =>
                    string.Equals(t.LanguageCode, preference.SubtitleLanguage, StringComparison.OrdinalIgnoreCase));
                if (exactLang is not null) return exactLang;

                // 3. Title match
                if (!string.IsNullOrWhiteSpace(preference.SubtitleTitle))
                {
                    var titleMatch = nonDisabled.FirstOrDefault(t =>
                        string.Equals(t.DisplayName, preference.SubtitleTitle, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(t.RawName, preference.SubtitleTitle, StringComparison.OrdinalIgnoreCase));
                    if (titleMatch is not null) return titleMatch;
                }
            }
        }

        return null;
    }
}
