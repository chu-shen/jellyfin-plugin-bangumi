using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Bangumi.Providers;

public class MovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
{
    private readonly BangumiApi _api;
    private readonly ILogger<MovieProvider> _log;
    private readonly Plugin _plugin;

    public MovieProvider(Plugin plugin, BangumiApi api, ILogger<MovieProvider> logger)
    {
        _plugin = plugin;
        _api = api;
        _log = logger;
    }

    private HttpClient HttpClient => _plugin.GetHttpClient();

    public int Order => -5;
    public string Name => Constants.ProviderName;

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var result = new MetadataResult<Movie> { ResultLanguage = Constants.Language };

        var subjectId = info.ProviderIds.GetOrDefault(Constants.ProviderName);
        if (string.IsNullOrEmpty(subjectId))
        {
            var searchName = BangumiHelper.NameHelper(info.Name, _plugin);
            _log.LogInformation("Searching {Name} in bgm.tv", searchName);
            var searchResult = await _api.SearchSubject(searchName, token);
            if (searchResult.Count > 0)
                subjectId = $"{searchResult[0].Id}";
        }

        // try search OriginalTitle
        if (string.IsNullOrEmpty(subjectId) && info.OriginalTitle != null && !String.Equals(info.OriginalTitle, info.Name, StringComparison.Ordinal))
        {
            var searchName = BangumiHelper.NameHelper(info.OriginalTitle, _plugin);
            _log.LogInformation("Searching {Name} in bgm.tv", searchName);
            var searchResult = await _api.SearchSubject(searchName, token);
            if (searchResult.Count > 0)
                subjectId = $"{searchResult[0].Id}";
        }

        if (string.IsNullOrEmpty(subjectId))
            return result;

        var subject = await _api.GetSubject(subjectId, token);
        if (subject == null)
            return result;

        result.Item = new Movie();
        result.HasMetadata = true;

        result.Item.ProviderIds.Add(Constants.ProviderName, subjectId);
        if (!string.IsNullOrEmpty(subject.AirDate))
        {
            result.Item.PremiereDate = DateTime.Parse(subject.AirDate);
            result.Item.ProductionYear = DateTime.Parse(subject.AirDate).Year;
        }

        result.Item.CommunityRating = subject.Rating?.Score;
        result.Item.Name = subject.ChineseName;
        result.Item.OriginalTitle = subject.OriginalName;
        result.Item.Overview = subject.Summary;
        result.Item.Tags = subject.PopularTags;

        (await _api.GetSubjectPeople(subjectId, token)).ForEach(result.AddPerson);
        (await _api.GetSubjectCharacters(subjectId, token)).ForEach(result.AddPerson);

        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo,
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
        return await HttpClient.GetAsync(url, token).ConfigureAwait(false);
    }
}