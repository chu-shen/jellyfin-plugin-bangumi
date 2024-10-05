using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Bangumi.Configuration;
using Jellyfin.Plugin.Bangumi.Model;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Bangumi.Parser.Anitomy
{
    public class AnitomyEpisodeParser : IEpisodeParser
    {
        private readonly BangumiApi _api;
        private readonly ILogger<AnitomyEpisodeParser> _log;
        private readonly ILibraryManager _libraryManager;
        private readonly PluginConfiguration _configuration;
        private readonly EpisodeInfo _info;
        private readonly LocalConfiguration _localConfiguration;
        private readonly CancellationToken _token;
        private readonly IFileSystem _fileSystem;

        public AnitomyEpisodeParser(BangumiApi api, ILoggerFactory loggerFactory, ILibraryManager libraryManager, PluginConfiguration Configuration, EpisodeInfo info, LocalConfiguration localConfiguration, CancellationToken token, IFileSystem fileSystem)
        {
            _api = api;
            _log = loggerFactory.CreateLogger<AnitomyEpisodeParser>();
            _libraryManager = libraryManager;
            _configuration = Configuration;
            _info = info;
            _localConfiguration = localConfiguration;
            _token = token;
            _fileSystem = fileSystem;
        }


        public async Task<Model.Episode?> GetEpisode(int seriesId, double episodeIndex)
        {
            var fileName = Path.GetFileName(_info.Path);
            var anitomy = new Jellyfin.Plugin.Bangumi.Anitomy(fileName);
            var (anitomyEpisodeType, bangumiEpisodeType) = GetEpisodeType(fileName, anitomy);

            if (_configuration.AlwaysProcessMultiSeasonFolderByAnitomySharp)
                seriesId = await ProcessMultiSeasonFolder(seriesId);

            try
            {
                // 基本规则
                Episode? episode = await rule01(seriesId, episodeIndex, fileName, anitomy, anitomyEpisodeType, bangumiEpisodeType);
                // 多季度规则，仅适用于普通剧集 #TODO 可能存在未识别的 sp 被此规则识别成剧集
                if (episode is null && (bangumiEpisodeType is EpisodeType.Normal || bangumiEpisodeType is null))
                {
                    episode = await rule02(seriesId, episodeIndex, anitomy);
                }

                if (episode != null)
                {
                    // 对于无标题的剧集，手动添加标题，而不是使用 Jellyfin 生成的标题
                    if (episode.ChineseNameRaw == "" && episode.OriginalNameRaw == "")
                    {
                        episode.OriginalNameRaw = TitleOfSpecialEpisode(anitomy, anitomyEpisodeType);
                    }
                    return episode;
                }

                // 特典
                var sp = new Jellyfin.Plugin.Bangumi.Model.Episode();
                sp.Type = bangumiEpisodeType ?? EpisodeType.Special;
                sp.Order = episodeIndex;
                sp.OriginalNameRaw = TitleOfSpecialEpisode(anitomy, anitomyEpisodeType);
                _log.LogInformation("Set OriginalName: {OriginalNameRaw} for {fileName}", sp.OriginalNameRaw, fileName);
                return sp;
            }
            catch (InvalidOperationException)
            {
                _log.LogWarning("Error while match episode!");
                return null;
            }
        }

        /// <summary>
        /// 基础规则
        /// </summary>
        /// <param name="seriesId"></param>
        /// <param name="episodeIndex"></param>
        /// <param name="fileName"></param>
        /// <param name="anitomy"></param>
        /// <param name="anitomyEpisodeType"></param>
        /// <param name="bangumiEpisodeType"></param>
        /// <returns></returns>
        private async Task<Episode?> rule01(int seriesId, double episodeIndex, string fileName, Bangumi.Anitomy anitomy, string? anitomyEpisodeType, EpisodeType? bangumiEpisodeType)
        {
            // 获取剧集元数据
            var episodeListData = await _api.GetSubjectEpisodeList(seriesId, bangumiEpisodeType, episodeIndex, _token) ?? new List<Episode>();
            if (episodeListData.Count == 0)
            {
                // Bangumi 中本应为`Special`类型的剧集被划分到`Normal`类型的问题
                if (bangumiEpisodeType is EpisodeType.Special)
                {
                    episodeListData = await _api.GetSubjectEpisodeList(seriesId, null, episodeIndex, _token) ?? new List<Episode>();
                    _log.LogInformation("Process Special: {anitomyEpisodeType} for {fileName}", anitomyEpisodeType, fileName);
                }
                // 仅包含 SP，无其他剧集类型时，尝试匹配 Bangumi 的特典元数据 e.g. [Girls und Panzer Saishuushou][Vol.03][SP02][Akiyama Yukari Panzer Lecture][BDRIP][1080P][H264_FLAC].mkv
                // TODO 由于 AnitomySharp 识别的问题，导致错误匹配的概率较高
                // if (anitomyEpisodeType is not null && bangumiEpisodeType is EpisodeType.Other && anitomyEpisodeType.Contains("SP", StringComparison.OrdinalIgnoreCase)){
                //     var specialEpisodeListData = await _api.GetSubjectEpisodeList(seriesId, EpisodeType.Special, episodeIndex, _token) ?? new List<Episode>();
                //     if (specialEpisodeListData.Count != 0)
                //     {
                //         episodeListData = specialEpisodeListData;
                //         _log.LogDebug("Try match special episode for: {fileName}",fileName);
                //     }
                // }
            }

            // 匹配剧集元数据
            var episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Order.Equals(episodeIndex));
            if (episode is null)
            {
                // 该剧集类型下由于集数问题导致无法正确匹配
                if (bangumiEpisodeType is not null && episodeIndex == 0 && episodeListData.Count != 0)
                {
                    episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Order.Equals(1));
                }

                // 季度分割导致的编号问题
                // 是否应该优先使用 ALT 剧集？各有优劣？
                // example: Legend of the Galactic Heroes - Die Neue These 12 (48)
                var episodeIndexAlt = anitomy.ExtractEpisodeNumberAlt();
                if (episodeIndexAlt is not null)
                {
                    episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Order.Equals(double.Parse(episodeIndexAlt)));
                }

                // 尝试使用 Bangumi `Index`序号进行匹配
                // if (episodeIndex != 0 && episodeListData.Count != 0)
                // {
                //     episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Index.Equals(episodeIndex.Value));
                // }
            }

            return episode;
        }

        /// <summary>
        /// 处理多季度且文件序号连续
        /// 如：「機動戦士ガンダム00」分为两季，每季序号均从1开始，但本地文件命名为 1-50
        /// 如：「らんま1/2」分为两季，第二季序号接第一季顺序，但本地文件命名为 1-161
        /// </summary>
        /// <param name="seriesId"></param>
        /// <param name="episodeIndex"></param>
        /// <param name="anitomy"></param>
        /// <returns></returns>
        private async Task<Episode?> rule02(int seriesId, double episodeIndex, Bangumi.Anitomy anitomy)
        {
            var episodeIndexAlt = double.Parse(anitomy.ExtractEpisodeNumberAlt() ?? "-1");
            // 获取剧集元数据
            var episodeListData = await _api.GetSubjectEpisodeList(seriesId, EpisodeType.Normal, episodeIndex, _token) ?? new List<Episode>();
            var seasonEpisodeCount = episodeListData.Last().Order;
            if (episodeIndex <= seasonEpisodeCount && episodeIndexAlt <= seasonEpisodeCount)
                return null;
            var subjectId = 0;
        nextSeason:
            // 获取下一季元数据
            var results = await _api.GetRelatedSubject(seriesId, _token);
            if (results is null)
                return null;
            foreach (var result in results)
            {
                if (result.Relation == "续集")
                {
                    subjectId = result.Id;
                    _log.LogInformation("use sequel: {sequel} for episode", subjectId);
                    break;
                }
            }
            // 无续集
            if (seriesId == subjectId)
                return null;

            seriesId = subjectId;
            episodeListData = await _api.GetSubjectEpisodeList(seriesId, EpisodeType.Normal, episodeIndex, _token) ?? new List<Episode>();
            if (episodeListData.Count == 0)
                return null;
            // 下一季的下一季……
            // 本季的第一集序号
            var getFirstEpisodeOrder = (await _api.GetSubjectEpisodeList(seriesId, EpisodeType.Normal, 0, _token) ?? new List<Episode>()).First().Order;
            // 本季之前的总序号
            var lastSeasonEpisodeOrder = seasonEpisodeCount;
            // 避免因为缓存导致修改序号时只修改了当前剧集序号，统一调整为：继续排序，方便重新给剧集 Order 重新赋值
            // 索性第一集为1的直接把所有集数都增加增量
            if (getFirstEpisodeOrder == 1)
            {
                getFirstEpisodeOrder = getFirstEpisodeOrder + seasonEpisodeCount;
                episodeListData.ForEach(e => e.Order = e.Order + seasonEpisodeCount);
            }
            // 集数相加，但要减去下一季的第一集序号，然后再加 1，保证多季度序号是否重排或继续排序都不影响正确的序号
            seasonEpisodeCount = lastSeasonEpisodeOrder + episodeListData.Last().Order - getFirstEpisodeOrder + 1;
            if (episodeIndex > seasonEpisodeCount || episodeIndexAlt > seasonEpisodeCount)
                goto nextSeason;

            // 匹配剧集元数据
            var episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Order.Equals(episodeIndex));
            if (episode is null)
            {
                episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Order.Equals(episodeIndex - lastSeasonEpisodeOrder));
            }
            // 重新赋值，保留集数序号，避免多季序号不连续时，同时出现多个第一集
            if (episode is not null)
                episode.Order = episodeIndex;
            
            if (episode is null && episodeIndexAlt != -1)
            {
                episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Order.Equals(episodeIndexAlt));
                if (episode is not null)
                    episode.Order = episodeIndexAlt;
            }
            if (episode is null && episodeIndexAlt != -1)
            {
                episode = episodeListData.OrderBy(x => x.Type).FirstOrDefault(x => x.Order.Equals(episodeIndexAlt - lastSeasonEpisodeOrder));
                if (episode is not null)
                    episode.Order = episodeIndexAlt;
            }

            return episode;
        }
        /// <summary>
        /// 获取剧集类型
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="anitomy"></param>
        /// <returns></returns>
        private (string?, EpisodeType?) GetEpisodeType(string fileName, Bangumi.Anitomy anitomy)
        {
            var (anitomyEpisodeType, bangumiEpisodeType) = AnitomyEpisodeTypeMapping.GetEpisodeType(anitomy.ExtractAnimeType());
            _log.LogDebug("Bangumi episode type: {bangumiEpisodeType}", bangumiEpisodeType);
            // 判断文件夹/ Jellyfin 季度是否为 Special
            if (bangumiEpisodeType is null)
            {
                try
                {
                    string[] parent = { (_libraryManager.FindByPath(Path.GetDirectoryName(_info.Path), true)).Name };
                    // 路径类型
                    var (anitomyPathType, bangumiPathType) = AnitomyEpisodeTypeMapping.GetEpisodeType(parent);
                    // 存在误判的可能性
                    anitomyEpisodeType = anitomyPathType;
                    bangumiEpisodeType = bangumiPathType;
                    _log.LogDebug("Jellyfin parent name: {parent}. Path type: {type}", parent, anitomyPathType);
                }
                catch
                {
                    _log.LogWarning("Failed to get jellyfin parent of {fileName}", fileName);
                }
            }
            return (anitomyEpisodeType, bangumiEpisodeType);
        }

        /// <summary>
        /// 特殊剧集标题
        /// #TODO 命名规则可选
        /// </summary>
        /// <param name="anitomy"></param>
        /// <param name="anitomyEpisodeType"></param>
        /// <returns></returns>
        private string TitleOfSpecialEpisode(Jellyfin.Plugin.Bangumi.Anitomy anitomy, string? anitomyEpisodeType)
        {
            string[] parts = new string[]
                        {
                            anitomy.ExtractAnimeTitle()?.Trim() ?? "",
                            anitomy.ExtractEpisodeTitle()?.Trim() ?? "",
                            anitomyEpisodeType?.Trim() ?? "",
                            anitomy.ExtractAnimeSeason()?.Trim()==null? "":"S"+anitomy.ExtractAnimeSeason()?.Trim(),
                            anitomy.ExtractVolumeNumber()?.Trim()==null? "":"V"+anitomy.ExtractVolumeNumber()?.Trim(),
                            anitomy.ExtractEpisodeNumber()?.Trim()==null? "":"E"+anitomy.ExtractEpisodeNumber()?.Trim(),
                            anitomy.ExtractEpisodeNumberAlt()?.Trim()==null? "":"("+anitomy.ExtractEpisodeNumberAlt()?.Trim()+")"
                        };
            string separator = " ";
            var titleOfSpecialEpisode = string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return titleOfSpecialEpisode;
        }

        public double GetEpisodeIndex(string fileName, double episodeIndex)
        {
            var anitomy = new Jellyfin.Plugin.Bangumi.Anitomy(fileName);
            var anitomyIndex = anitomy.ExtractEpisodeNumber();
            var fileInfo = _fileSystem.GetFileSystemInfo(_info.Path);
            if (!string.IsNullOrEmpty(anitomyIndex))
            {
                try
                {
                    episodeIndex = double.Parse(anitomyIndex);
                }
                catch
                {
                    _log.LogWarning("Input string was not in a correct format: {anitomyIndex}", anitomyIndex);
                }
            }
            else if (fileInfo is not null && fileInfo.Length > 100000000)
            {
                // 大于 100MB 的可能是 Movie 等类型
                // 存在误判的可能性，导致被识别为第一集。配合 SP 文件夹判断可降低误判的副作用
                // #TODO 由用户决定是否开启
                episodeIndex = 1;
                _log.LogDebug("Use episode number: {episodeIndex} for {fileName}, because file size is {size} GB", episodeIndex, fileName, fileInfo.Length / 1000000000);
            }
            else
            {
                // default value
                episodeIndex = 0;
            }
            _log.LogInformation("Use episode number: {episodeIndex} for {fileName}", episodeIndex, fileName);

            // 特典不应用本地配置的偏移值
            var (anitomyEpisodeType, bangumiEpisodeType) = GetEpisodeType(fileName, anitomy);
            if (bangumiEpisodeType is not null or EpisodeType.Normal)
            {
                episodeIndex += _localConfiguration.Offset;
            }

            return episodeIndex;
        }

        /// <summary>
        /// 根据文件夹名称搜索，或者使用已存在的 id 
        /// 另外，推荐同时修改季度值
        /// #TODO 效果一般
        /// </summary>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        private async Task<int> ProcessMultiSeasonFolder(int seriesId)
        {
            // 使用此媒体文件的父目录获取名称，然后进行搜索
            var parent = _libraryManager.FindByPath(Path.GetDirectoryName(_info.Path), true);

            _log.LogDebug("Jellyfin parent name: {parent}", parent);
            string[] skipFolders = { "映像特典", "特典", "特典アニメ", "SPECIAL", "SPECIALS", "SP", "SPs", "CDs", "Scans", "Bonus" };
            if (!parent.Name.Contains("第", StringComparison.OrdinalIgnoreCase) && !parent.Name.Contains("SEASON", StringComparison.OrdinalIgnoreCase) && !skipFolders.Contains(parent.Name, StringComparer.OrdinalIgnoreCase))
            {
                var subjectId = 0;
                _ = int.TryParse(parent.ProviderIds.GetOrDefault(Constants.ProviderName), out subjectId);
                if (subjectId > 0)
                {
                    _log.LogInformation("Multi season folder, use exist id: {subjectId}", subjectId);
                    return subjectId;
                }

                var anitomy = new Jellyfin.Plugin.Bangumi.Anitomy(parent.Name);
                var searchName = anitomy.ExtractAnimeTitle();
                if (searchName is null) return seriesId;
                _log.LogInformation("Multi season folder, Searching {Name} in bgm.tv", searchName);
                var searchResult = await _api.SearchSubject(searchName, _token);

                // 使用年份进行精确匹配
                // 示例: [2022 Movie][Bubble][BDRIP][1080P+SP]
                var animeYear = anitomy.ExtractAnimeYear();
                if (animeYear != null)
                    searchResult = searchResult.FindAll(x => x.ProductionYear == animeYear);
                if (searchResult.Count > 0)
                {
                    if (searchResult.Count > 1)
                    {
                        var subjectWithInfobox = new List<Subject>();
                        for (int i = 0; i < Math.Min(3, searchResult.Count); i++)
                        {
                            var ss = await _api.GetSubject(searchResult[i].Id, _token);
                            if (ss != null)
                            {
                                _log.LogDebug("Multi season folder, sort subject: {on} with infobox", ss.OriginalName);
                                subjectWithInfobox.Add(ss);
                            }
                        }
                        searchResult = Subject.SortBySimilarity(subjectWithInfobox, searchName);
                    }
                    subjectId = searchResult[0].Id;
                    _log.LogDebug("Multi season folder, Use subject id: {id}", subjectId);

                    parent.ProviderIds.Add(Constants.ProviderName, subjectId.ToString());
                    await _libraryManager.UpdateItemAsync(parent, parent, ItemUpdateType.MetadataEdit, _token);

                    return subjectId;
                }
            }

            // #TODO 检查与旧 seriesId 的关联性，如果无联系则说明可能匹配错误

            return seriesId;
        }
        private int ProcessMultiSeasonInOneFolder(int seriesId)
        {
            // #TODO 
            // 1. 获取季度
            // 2. 获取 Bangumi 上的季度关系清单
            // 3. 匹配

            return 0;
        }


    }
}
