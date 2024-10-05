using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Bangumi.ScheduledTasks
{
    public class AddCollection : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;

        private readonly BangumiApi _api;
        private readonly ICollectionManager _collectionManager;
        private readonly ILogger<AddCollection> _log;

        public AddCollection(BangumiApi api, ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<AddCollection> log)
        {
            _api = api;
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _log = log;
        }

        public string Key => Constants.PluginName + "AddCollection";

        public string Name => "Add Collection";

        public string Description => "根据关联条目创建合集";

        public string Category => Constants.PluginName;


        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            await Task.Yield();
            progress?.Report(0);

            var query = new InternalItemsQuery { };
            var items = _libraryManager.GetItemList(query)
                // .Where(o => o.ProviderIds.ContainsKey(Constants.PluginName) && o.GetClientTypeName() == "Series")
                .Where(o => o.ProviderIds.ContainsKey(Constants.PluginName) && (o.GetClientTypeName() == "Series" || o.GetClientTypeName() == "Movie"))
                .ToList();

            var processed = new List<Guid>();
            foreach (var (idx, item) in items.WithIndex())
            {
                progress?.Report((double)idx / items.Count * 100);
                // 跳过已添加至合集的
                if (processed.Contains(item.Id))
                    continue;
                _log.LogDebug("process item: {itemName}", item.Name);
                var bangumiSeriesIds = new List<int>();
                var providerIds = item.ProviderIds.GetValueOrDefault(Constants.PluginName);
                if (providerIds is null)
                    continue;
                // 获取此 id 对应的系列所有 id
                bangumiSeriesIds = await _api.GetAllSeriesSubjectIds(int.Parse(providerIds), cancellationToken);

                // 匹配 items 的 id
                var collections = items
                    .Where(o => bangumiSeriesIds.Contains(int.Parse(o.ProviderIds.GetValueOrDefault(Constants.PluginName) ?? "-1")))
                    .ToList();
                // 跳过数量小于 2 的
                if (collections.Count < 2)
                    continue;

                // 使用系列中最小 id 对应的名字作为合集名
                // #TODO 应如何确定系列名
                var firstSeries = await _api.GetSubject(bangumiSeriesIds.Min(), cancellationToken);
                if (firstSeries is null)
                    continue;
                var option = new CollectionCreationOptions
                {
                    Name = (firstSeries.ChineseName ?? firstSeries.OriginalName) + "（系列）",
                    ItemIdList = collections.Select(o => o.Id.ToString()).ToArray(),
                };

                var collection = await _collectionManager.CreateCollectionAsync(option).ConfigureAwait(false);
                // 添加已处理的 item，避免后面重复处理
                processed.AddRange(collections.Select(c => c.Id));

                var moviesImages = collections.Where(o => o.HasImage(ImageType.Primary));
                if (moviesImages.Any())
                {
                    collection.SetImage(moviesImages.Random().GetImageInfo(ImageType.Primary, 0), 0);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }

            progress?.Report(100);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();
    }
}
