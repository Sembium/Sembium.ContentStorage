﻿using Sembium.ContentStorage.Common.ContentNames.Vault;
using Sembium.ContentStorage.Misc.Utils;
using Sembium.ContentStorage.Storage.Tools;
using Sembium.ContentStorage.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Common.ContentNames
{
    public class ContentNamesRepository : IContentNamesRepository
    {
        private const int AddBlockContentNamesCount = 1000;
        private readonly IContentNamesRepositorySettings _contentNamesRepositorySettings;
        private readonly IContentNameProvider _contentNameProvider;
        private readonly IContentMonthProvider _contentMonthProvider;
        private readonly IContentNamesVault _contentNamesVault;

        public ContentNamesRepository(
            IContentNamesRepositorySettings contentNamesRepositorySettings,
            IContentNameProvider contentNameProvider,
            IContentMonthProvider contentMonthProvider,
            IContentNamesVault contentNamesVault)
        {
            _contentNamesRepositorySettings = contentNamesRepositorySettings;
            _contentNameProvider = contentNameProvider;
            _contentMonthProvider = contentMonthProvider;
            _contentNamesVault = contentNamesVault;
        }

        private string GenerateGuid()
        {
            return Guid.NewGuid().ToString().Replace("-", "");
        }

        private string GenerateContentNamesVaultItemName(string prefix)
        {
            return prefix + GenerateGuid() + ".txt";
        }

        private int NewRandom(int maxValue)
        {
            return new Random().Next(maxValue);
        }

        private IContentNamesVaultItem GetAppendContentNamesVaultItem(string contentsContainerName, DateTimeOffset contentMonth, IEnumerable<string> forbiddenVaultItemNames, bool compacting)
        {
            var prefix = MonthToPrefix(contentMonth);

            var isActiveMonth = contentMonth.AddMonths(1) > DateTimeOffset.Now;
            var monthCompacting = compacting && !isActiveMonth;

            var availableContentNamesVaultItems =
                    _contentNamesVault
                    .GetItems(contentsContainerName, prefix)
                    .Where(x => (forbiddenVaultItemNames == null) || (!forbiddenVaultItemNames.Contains(x.Name)))
                    .Where(x => x.CanAppend(monthCompacting))
                    .ToList();

            if (availableContentNamesVaultItems.Count() < (monthCompacting ? 1 : _contentNamesRepositorySettings.MonthActiveVaultItemCount))
            {
                return _contentNamesVault.GetNewItem(contentsContainerName, GenerateContentNamesVaultItemName(prefix));
            }
            else
            {
                return availableContentNamesVaultItems[NewRandom(availableContentNamesVaultItems.Count())];
            }
        }

        private void TryAction(int tryCount, TimeSpan delay, Func<bool> action, Action failureAction)
        {
            while (tryCount > 0)
            {
                if (action())
                {
                    return;
                }

                tryCount--;

                if (tryCount > 0)
                {
                    Task.Delay(delay).Wait();
                }
            }

            failureAction();
        }

        private void AddBlock(string contentsContainerName, string blockText, DateTimeOffset contentMonth, IEnumerable<string> forbiddenVaultItemNames, bool compacting, CancellationToken cancellationToken)
        {
            using (var stream = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(blockText);
                stream.Write(bytes, 0, bytes.Length);

                TryAction(3, TimeSpan.FromMilliseconds(300),
                    () =>
                    {
                        stream.Position = 0;

                        var contentNamesVaultItem = GetAppendContentNamesVaultItem(contentsContainerName, contentMonth, forbiddenVaultItemNames, compacting);

                        return AppendToVaultItem(contentNamesVaultItem, contentsContainerName, contentMonth, stream, cancellationToken);
                    },
                    () =>
                    {
                        throw new Exception("Could not find available names index file");
                    }
                );
            }
        }

        private bool AppendToVaultItem(IContentNamesVaultItem contentNamesVaultItem, string contentsContainerName, DateTimeOffset contentMonth, MemoryStream stream, CancellationToken cancellationToken)
        {
            using (var ms = new MemoryStream())
            {
                using (var rs = contentNamesVaultItem.OpenReadStream())
                {
                    rs.CopyTo(ms);
                    stream.CopyTo(ms);
                }

                if ((!contentNamesVaultItem.IsNew) && (ms.Length == stream.Length))
                {
                    return false;
                }

                ms.Position = 0;

                var prefix = MonthToPrefix(contentMonth);
                var newContentNamesVaultItem = _contentNamesVault.GetNewItem(contentsContainerName, GenerateContentNamesVaultItemName(prefix));

                newContentNamesVaultItem.LoadFromStream(ms);

                try
                {
                    contentNamesVaultItem.DeleteAsync(cancellationToken).Wait();
                }
                catch
                {
                    // do nothing, duplicates are not a problem, delete on compact
                }

                return true;
            }
        }

        private string MonthToPrefix(DateTimeOffset month)
        {
            return month.ToUniversalTime().ToString("yyyy-MM-");
        }

        private int AddContents(string contentsContainerName, IEnumerable<string> contentNames, DateTimeOffset contentMonth, IEnumerable<string> forbiddenVaultItemNames, bool compacting, CancellationToken cancellationToken)
        {
            var contentNamesList = contentNames.ToList();

            var contentNamesGroups = 
                    contentNamesList
                    .Select((x, i) => (Name: x, Index: i))
                    .GroupBy(y => y.Index / AddBlockContentNamesCount)
                    .Select(z => z.Select(q => q.Name));

            foreach (var contentNameGroup in contentNamesGroups)
            {
                var text = string.Join(Environment.NewLine, contentNameGroup);

                if (!string.IsNullOrEmpty(text))
                {
                    AddBlock(contentsContainerName, (text + Environment.NewLine), contentMonth, forbiddenVaultItemNames, compacting, cancellationToken);
                }
            }

            return contentNamesList.Count;
        }

        public void AddContent(string contentsContainerName, string contentName, DateTimeOffset contentDate, CancellationToken cancellationToken)
        {
            AddContents(contentsContainerName, new[] { contentName }, _contentMonthProvider.GetContentMonth(contentDate), null, false, cancellationToken);
        }

        private async Task<int> AddContentsAsync(string contentsContainerName, IEnumerable<string> contentNames, DateTimeOffset contentMonth, IEnumerable<string> forbiddenVaultItemNames, bool compacting, CancellationToken cancellationToken)
        {
            return await Task.Run(() => AddContents(contentsContainerName, contentNames, contentMonth, forbiddenVaultItemNames, compacting, cancellationToken));
        }

        public int AddContents(string contentsContainerName, IEnumerable<KeyValuePair<string, DateTimeOffset>> contents, CancellationToken cancellationToken)
        {
            var monthContents =
                    contents
                    .OrderBy(x => x.Value)
                    .Select(x => new { ContentName = x.Key, ContentMonth = _contentMonthProvider.GetContentMonth(x.Value) })
                    .GroupBy(x => x.ContentMonth);

            var tasks = monthContents.AsParallel().Select(month => AddContentsAsync(contentsContainerName, month.Select(y => y.ContentName), month.Key, null, false, cancellationToken)).ToArray();
            Task.WhenAll(tasks).Wait();

            return tasks.Sum(x => x.Result);
        }

        private IOrderedEnumerable<(DateTimeOffset Month, IEnumerable<IContentNamesVaultItem> VaultItems)> GetMonthVaultItems(string contentsContainerName, DateTimeOffset? beforeMonth, DateTimeOffset? afterMonth)
        {
            var contentNamesVaultItems =
                    _contentNamesVault.GetItems(contentsContainerName, "")
                    .Select(x => new { ContentNamesVaultItem = x, ContentsMonth = GetContentsMonth(x.Name) });

            if (beforeMonth.HasValue)
            {
                contentNamesVaultItems = contentNamesVaultItems.Where(x => x.ContentsMonth < beforeMonth.Value);
            }

            if (afterMonth.HasValue)
            {
                contentNamesVaultItems = contentNamesVaultItems.Where(x => x.ContentsMonth > afterMonth.Value);
            }

            return
                contentNamesVaultItems
                .GroupBy(x => x.ContentsMonth)
                .Select(x => (Month: x.Key, VaultItems: x.Select(y => y.ContentNamesVaultItem)))
                .OrderBy(x => x.Month);
        }

        private IEnumerable<string> GetChronologicallyOrderedContentNames(IEnumerable<IContentNamesVaultItem> vaultItems, string prefix, CancellationToken cancellationToken)
        {
            return
                vaultItems
                    .AsParallel()
                    .SelectMany(y => GetContentNames(y, prefix, cancellationToken))
                    .Select(y => new { ContentName = y, ContentIdentifier = _contentNameProvider.GetContentIdentifier(y) })
                    .OrderBy(y => y.ContentIdentifier.ModifiedMoment)
                    .ThenBy(y => y.ContentIdentifier.Guid)
                    .Select(y => y.ContentName)
                    .UniqueOnOrdered();
        }

        private IEnumerable<string> GetChronologicallyOrderedContentNames(IOrderedEnumerable<(DateTimeOffset Month, IEnumerable<IContentNamesVaultItem> VaultItems)> monthVaultItems, string prefix, CancellationToken cancellationToken)
        {
            return monthVaultItems.SelectMany(x => GetChronologicallyOrderedContentNames(x.VaultItems, prefix, cancellationToken));
        }

        public IEnumerable<string> GetChronologicallyOrderedContentNames(string contentsContainerName, string prefix, DateTimeOffset? beforeMonth, DateTimeOffset? afterMonth, CancellationToken cancellationToken)
        {
            return GetChronologicallyOrderedContentNames(GetMonthVaultItems(contentsContainerName, beforeMonth, afterMonth), prefix, cancellationToken);
        }

        private IEnumerable<string> GetContentNamesVaultItemLines(IContentNamesVaultItem contentNamesVaultItem, string prefix, CancellationToken cancellationToken)
        {
            using (var stream = contentNamesVaultItem.OpenReadStream())
            {
                foreach (var line in stream.ReadAllLines(Encoding.UTF8).Where(x => (prefix == null) || ((!string.IsNullOrEmpty(x)) && x.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))))  // to prevent stream disposal
                {
                    yield return line;
                }
            }
        }

        private IEnumerable<string> GetContentNames(IContentNamesVaultItem contentNamesVaultItem, string prefix, CancellationToken cancellationToken)
        {
            return 
                GetContentNamesVaultItemLines(contentNamesVaultItem, prefix, cancellationToken)
                .Where(x => !string.IsNullOrEmpty(x));
        }

        private DateTimeOffset GetContentsMonth(string contentNamesVaultItemName)
        {
            var parts = contentNamesVaultItemName.Split('/').Last().Split('-');

            if (parts.Length < 2)
            {
                throw new UserException($"Invalid content names vault item name: {contentNamesVaultItemName}");
            }

            var year = int.Parse(parts[0]);
            var month = int.Parse(parts[1]);

            return new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.FromHours(0));
        }

        public async Task CompactAsync(string containerName, CancellationToken cancellationToken)
        {
            var monthVaultItems = GetMonthVaultItems(containerName, null, null).ToList();

            foreach (var x in monthVaultItems)
            {
                if (x.VaultItems.Where(y => y.CanAppend(compacting: true)).Count() > 1)
                {
                    var vaultItemNames = x.VaultItems.Select(y => y.Name);
                    var monthContentNames = GetChronologicallyOrderedContentNames(x.VaultItems, null, cancellationToken);

                    AddContents(containerName, monthContentNames, x.Month, vaultItemNames, true, cancellationToken);

                    await DeleteVaultItems(x.VaultItems, cancellationToken);
                }
            }
        }

        private async Task DeleteVaultItems(IEnumerable<IContentNamesVaultItem> vaultItems, CancellationToken cancellationToken)
        {
            var tasks = vaultItems.AsParallel().Select(x => x.DeleteAsync(cancellationToken));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                // do nothing, delete tomorrow
            }
        }

        public void EnsureContent(string containerName, string contentName, DateTimeOffset contentDate, CancellationToken cancellationToken)
        {
            var contentMonth = _contentMonthProvider.GetContentMonth(contentDate);

            var contentNameExists = GetChronologicallyOrderedContentNames(containerName, contentName, contentMonth.AddMonths(1), contentMonth.AddMonths(-1), cancellationToken).Any();

            if (!contentNameExists)
            {
                AddContent(containerName, contentName, contentDate, cancellationToken);
            }
        }
    }
}
