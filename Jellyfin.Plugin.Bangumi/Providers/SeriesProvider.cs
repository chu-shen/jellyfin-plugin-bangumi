﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.Bangumi.Model;

namespace Jellyfin.Plugin.Bangumi.Providers
{
    public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly BangumiApi _api;
        private readonly ILogger<SeriesProvider> _log;
        private readonly Plugin _plugin;

        public SeriesProvider(Plugin plugin, BangumiApi api, ILogger<SeriesProvider> log)
        {
            _plugin = plugin;
            _api = api;
            _log = log;
        }

        public int Order => -5;
        public string Name => Constants.ProviderName;

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = new MetadataResult<Series> { ResultLanguage = Constants.Language };

            var subjectId = info.ProviderIds.GetOrDefault(Constants.ProviderName);
            if (string.IsNullOrEmpty(subjectId))
            {
                _log.LogInformation("Searching {Name} in bgm.tv", info.Name);
                var searchResult = await _api.SearchSubject(info.Name, token);
                if (searchResult.Count > 0)
                    subjectId = SortResult(searchResult, info.Name);
            }

            if (string.IsNullOrEmpty(subjectId))
                return result;

            var subject = await _api.GetSubject(subjectId, token);
            if (subject == null)
                return result;

            result.Item = new Series();
            result.HasMetadata = true;

            result.Item.ProviderIds.Add(Constants.ProviderName, subjectId);
            if (!string.IsNullOrEmpty(subject.AirDate))
            {
                result.Item.AirTime = subject.AirDate;
                result.Item.AirDays = new[] { DateTime.Parse(subject.AirDate).DayOfWeek };
                result.Item.PremiereDate = DateTime.Parse(subject.AirDate);
                result.Item.ProductionYear = DateTime.Parse(subject.AirDate).Year;
            }

            result.Item.CommunityRating = subject.Rating?.Score;
            result.Item.Name = subject.GetName(_plugin.Configuration);
            result.Item.OriginalTitle = subject.OriginalName;
            result.Item.Overview = subject.Summary;
            result.Item.Tags = subject.PopularTags;

            (await _api.GetSubjectPeople(subjectId, token)).ForEach(result.AddPerson);
            (await _api.GetSubjectCharacters(subjectId, token)).ForEach(result.AddPerson);

            return result;
        }

        private String SortResult(List<Subject> searchResults, String name){
            SimilarityTool similarityTool = new SimilarityTool();
            var degree = -1;
            string resultId;
            foreach (Subject searchResult in searchResults)
            {
                var temp = similarityTool.CompareStrings(name,searchResult.OriginalName);
                if (degree < temp){
                    degree = temp;
                    resultId = searchResult.Id;
                }
            }
            _log.LogInformation("best match in bgm.tv: {Name}", resultId);
            return resultId;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var results = new List<RemoteSearchResult>();

            var id = searchInfo.ProviderIds.GetOrDefault(Constants.ProviderName);

            if (!string.IsNullOrEmpty(id))
            {
                var subject = await _api.GetSubject(id, token);
                if (subject == null)
                    return results;
                var result = new RemoteSearchResult
                {
                    Name = subject.GetName(_plugin.Configuration),
                    SearchProviderName = subject.OriginalName,
                    ImageUrl = subject.DefaultImage,
                    Overview = subject.Summary
                };

                if (!string.IsNullOrEmpty(subject.AirDate))
                {
                    result.PremiereDate = DateTime.Parse(subject.AirDate);
                    result.ProductionYear = DateTime.Parse(subject.AirDate).Year;
                }

                result.SetProviderId(Constants.ProviderName, id);
                results.Add(result);
            }
            else if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                var series = await _api.SearchSubject(searchInfo.Name, token);
                foreach (var item in series)
                {
                    var itemId = $"{item.Id}";
                    var result = new RemoteSearchResult
                    {
                        Name = item.GetName(_plugin.Configuration),
                        SearchProviderName = item.OriginalName,
                        ImageUrl = item.DefaultImage,
                        Overview = item.Summary
                    };
                    result.SetProviderId(Constants.ProviderName, itemId);
                    results.Add(result);
                }
            }

            return results;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken token)
        {
            return await _plugin.GetHttpClient().GetAsync(url, token).ConfigureAwait(false);
        }
    }
}