using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Translator
{
    public class CachedWebClient
    {
        private readonly int _capacity;
        private readonly Dictionary<string, string> _urlsToPages;
        private readonly Queue<string> _urls;

        public CachedWebClient(int capacity)
        {
            _capacity = capacity;
            _urlsToPages = new Dictionary<string, string>(_capacity);
            _urls = new Queue<string>(_capacity);
        }

        public async Task<string> DownloadOrGetFromCacheAsync(string url, CancellationToken ct)
        {
            bool isCached;
            string page;
            lock (_urls)
            {
                isCached = _urlsToPages.TryGetValue(url, out page);
            }

            if (!isCached)
            {
                using (var client = new HttpClient())
                {
                    // TODO use cancellation token in .NET5
                    page = await client.GetStringAsync(url);

                    if (ct.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                }

                lock (_urls)
                {
                    if (!_urls.Contains(url))
                    {
                        if (_urls.Count == _capacity)
                        {
                            var removedUrl = _urls.Dequeue();
                            _urlsToPages.Remove(removedUrl);
                        }

                        _urls.Enqueue(url);
                        _urlsToPages[url] = page;
                    }
                }
            }

            Debug.WriteLineIf(isCached, $"Getting cached page by URL {url}");
            Debug.WriteLineIf(!isCached, $"Getting online page by URL {url}");

            return page;
        }
    }
}