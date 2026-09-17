using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// ObservableCollection of PostItem that supports GridView infinite scroll via
    /// ISupportIncrementalLoading. The page supplies a page-fetch delegate.
    /// </summary>
    public class IncrementalPostCollection : ObservableCollection<PostItem>, ISupportIncrementalLoading
    {
        private readonly Func<int, CancellationToken, Task<List<PostItem>>> _fetchPage;
        private int _page = 1;
        private bool _hasMore = true;
        private bool _isLoading;

        public event Action<bool>? LoadingStateChanged;

        public IncrementalPostCollection(Func<int, CancellationToken, Task<List<PostItem>>> fetchPage)
        {
            _fetchPage = fetchPage;
        }

        public bool HasMoreItems => _hasMore;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return LoadMoreAsync().AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> LoadMoreAsync()
        {
            if (_isLoading || !_hasMore) return new LoadMoreItemsResult { Count = 0 };
            _isLoading = true;
            LoadingStateChanged?.Invoke(true);
            uint added = 0;
            try
            {
                var items = await _fetchPage(_page, CancellationToken.None);
                if (items == null || items.Count == 0)
                {
                    _hasMore = false;
                }
                else
                {
                    _page++;
                    foreach (var item in items)
                    {
                        Add(item);
                        added++;
                    }
                }
            }
            catch
            {
                _hasMore = false;
            }
            finally
            {
                _isLoading = false;
                LoadingStateChanged?.Invoke(false);
            }
            return new LoadMoreItemsResult { Count = added };
        }
    }
}
