using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FuzzySharp;
using Jellyfin.Plugin.Bangumi.Configuration;

namespace Jellyfin.Plugin.Bangumi.Model;

public class Subject
{
    public int Id { get; set; }

    [JsonIgnore]
    public string OriginalName => WebUtility.HtmlDecode(OriginalNameRaw);

    [JsonPropertyName("name")]
    public string OriginalNameRaw { get; set; } = "";

    [JsonIgnore]
    public string? ChineseName => WebUtility.HtmlDecode(ChineseNameRaw);

    [JsonPropertyName("name_cn")]
    public string? ChineseNameRaw { get; set; }

    public string? Summary { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("air_date")]
    public string? Date2 { get; set; }

    [JsonIgnore]
    public string? AirDate => Date ?? Date2;

    [JsonIgnore]
    public string? ProductionYear => AirDate?.Length >= 4 ? AirDate?[..4] : null;

    public Dictionary<string, string>? Images { get; set; }

    [JsonIgnore]
    public string? DefaultImage => Images?["large"];

    [JsonPropertyName("eps")]
    public int? EpisodeCount { get; set; }

    [JsonPropertyName("rating")]
    public Rating? Rating { get; set; }

    [JsonPropertyName("tags")]
    public List<Tag> Tags { get; set; } = new();

    [JsonPropertyName("nsfw")]
    public bool IsNSFW { get; set; }

    [JsonIgnore]
    public string[] PopularTags
    {
        get
        {
            var baseline = Tags.Sum(tag => tag.Count) / 25;
            return Tags.Where(tag => tag.Count >= baseline).Select(tag => tag.Name).ToArray();
        }
    }
    [JsonPropertyName("infobox")]
    public List<InfoboxItem> Infobox { get; set; } = new();

    public string[] Alias()
    {
        var aliases = new List<string>();

        foreach (var item in Infobox)
        {
            if (item.Key == "别名" && item.Value.ValueKind == JsonValueKind.Array)
            {
                var values = item.Value.EnumerateArray()
                    .Select(x => x.GetProperty("v").GetString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray();
                if (values != null)
                {
                    aliases.AddRange(values!);
                }
                return aliases.ToArray();
            }
        }

        return aliases.ToArray();
    }
    public class InfoboxItem
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }= string.Empty;

        [JsonPropertyName("value")]
        public JsonElement Value { get; set; }
    }

    [JsonPropertyName("relation")]
    public string? Relation { get; set; }

    public string GetName(PluginConfiguration? configuration = default)
    {
        return configuration?.TranslationPreference switch
        {
            TranslationPreferenceType.Chinese => string.IsNullOrEmpty(ChineseName) ? OriginalName : ChineseName,
            TranslationPreferenceType.Original => OriginalName,
            _ => OriginalName
        };
    }

    public static List<Subject> SortBySimilarity(IEnumerable<Subject> list, string keyword)
    {
        var score = new Dictionary<Subject, int>();

        keyword = keyword.ToLower();

        foreach (var subject in list)
        {
            var chineseNameScore = Fuzz.Ratio(subject.ChineseName==null?subject.ChineseName:subject.ChineseName.ToLower(), keyword);
            var originalNameScore = Fuzz.Ratio(subject.OriginalName.ToLower(), keyword);
            var aliasScore = subject.Alias().Select(alias => Fuzz.Ratio(alias.ToLower(), keyword));

            var maxScore = Math.Max(Math.Max(chineseNameScore, originalNameScore), aliasScore.Any() ? aliasScore.Max() : int.MinValue);
            score.Add(subject, maxScore);
        }

        return score.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToList();
    }

}