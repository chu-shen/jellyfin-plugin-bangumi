using System.Net;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Bangumi.Configuration;

namespace Jellyfin.Plugin.Bangumi.Model;

public class Episode
{
    public int Id { get; set; }

    [JsonPropertyName("subject_id")]
    public int ParentId { get; set; }

    public EpisodeType Type { get; set; }

    [JsonIgnore]
    public string OriginalName => WebUtility.HtmlDecode(OriginalNameRaw);

    [JsonPropertyName("name")]
    public string OriginalNameRaw { get; set; } = "";

    [JsonIgnore]
    public string? ChineseName => WebUtility.HtmlDecode(ChineseNameRaw);

    [JsonPropertyName("name_cn")]
    public string? ChineseNameRaw { get; set; }

    [JsonPropertyName("sort")]
    public double Order { get; set; }

    public int Disc { get; set; }

    /// <summary>
    ///     条目内的集数, 从1开始。非本篇剧集的此字段无意义
    /// </summary>
    [JsonPropertyName("ep")]
    public double Index { get; set; }

    [JsonPropertyName("airdate")]
    public string AirDate { get; set; } = "";

    public string? Duration { get; set; }

    [JsonPropertyName("desc")]
    public string? Description { get; set; }

    public string? GetName(PluginConfiguration? configuration = default)
    {
        return configuration?.TranslationPreference switch
        {
            TranslationPreferenceType.Chinese => string.IsNullOrEmpty(ChineseName) ? OriginalName : ChineseName,
            TranslationPreferenceType.Original => OriginalName,
            _ => OriginalName
        };
    }

    public override string ToString()
    {
        return $"<Bangumi Episode #{Id}: {OriginalName}>";
    }
}