using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace ReliableDownloader
{
    public class FileDownloader : IFileDownloader
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly int _downloadBatchSize;
        private readonly string _downloadLocationFilePath;
        private readonly DownloadOptions _options;
        private readonly IWebSystemCalls _webSystemCalls;
        private bool _acceptsRanges;
        private DateTime _downloadStartTimeStamp;
        private Action<FileProgress> _onProgressChanged;
        private int _totalDownloaded;
        private long _totalFileSize;

        public FileDownloader(IWebSystemCalls webSystemCalls, IOptions<DownloadOptions> options)
        {
            _webSystemCalls = webSystemCalls;
            _options = options.Value;
            _cancellationTokenSource = new CancellationTokenSource();
            _downloadLocationFilePath = _options.FilePath;
            _downloadBatchSize = _options.DownloadBatchSize;
        }

        public void CancelDownloads()
        {
            _cancellationTokenSource.Cancel();
        }

        public async Task<bool> DownloadFile(Action<FileProgress> onProgressChanged)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
                throw new ArgumentNullException($"{nameof(DownloadOptions.BaseUrl)} is null or empty");
            if (string.IsNullOrWhiteSpace(_options.Endpoint))
                throw new ArgumentNullException($"{nameof(DownloadOptions.Endpoint)} is null or empty");
            if (string.IsNullOrWhiteSpace(_downloadLocationFilePath))
                throw new ArgumentNullException($"{nameof(DownloadOptions.FilePath)} is null is empty");

            Uri.TryCreate(new Uri(_options.BaseUrl), _options.Endpoint, out var url);
            var downloadFileUrl = url?.ToString();

            _onProgressChanged = onProgressChanged;
            long bytesWritten = 0;

            (_acceptsRanges, _totalFileSize) = await GetInstallerInfoAsync(downloadFileUrl);

            if (File.Exists(_downloadLocationFilePath)) bytesWritten = new FileInfo(_downloadLocationFilePath).Length;

            if (bytesWritten == _totalFileSize)
            {
                _onProgressChanged(new FileProgress(null, 0, 100, null));
                return true;
            }

            if (!_acceptsRanges)
            {
                var result = await _webSystemCalls.DownloadContent(downloadFileUrl, _cancellationTokenSource.Token);
                await using var fileStream = new FileStream(_downloadLocationFilePath, FileMode.Append);
                await (await result?.Content?.ReadAsStreamAsync()!).CopyToAsync(fileStream);
            }
            else
            {
                _downloadStartTimeStamp = DateTime.Now;
                await DownloadFileWithRange(downloadFileUrl, _downloadLocationFilePath, bytesWritten);
            }

            return !_cancellationTokenSource.Token.IsCancellationRequested;
        }

        private async Task DownloadFileWithRange(string contentFileUrl, string localFilePath, long rangeFrom)
        {
            while (true)
            {
                if (_cancellationTokenSource.IsCancellationRequested) return;

                var rangeTo = rangeFrom + _downloadBatchSize;
                if (rangeTo > _totalFileSize) rangeTo = _totalFileSize;

                var result = await _webSystemCalls.DownloadPartialContent(contentFileUrl, rangeFrom, rangeTo,
                    _cancellationTokenSource.Token);
                result.EnsureSuccessStatusCode();
                await using (var fileStream = new FileStream(localFilePath, FileMode.Append))
                {
                    await (await result?.Content?.ReadAsStreamAsync()!).CopyToAsync(fileStream);
                }

                var progressPercent = Math.Round(rangeTo / (double)_totalFileSize * 100);
                _totalDownloaded++;
                var estimatedDownloadTime = GetEstimatedDownloadTime(_totalFileSize, _downloadBatchSize, rangeTo);

                _onProgressChanged(new FileProgress(_totalFileSize, rangeTo, progressPercent, estimatedDownloadTime));

                if (rangeTo < _totalFileSize)
                {
                    rangeFrom = rangeTo + 1;
                    continue;
                }

                break;
            }
        }

        private TimeSpan GetEstimatedDownloadTime(long totalBytes, int chunkSize, long bytesDownloaded)
        {
            var timeSpan = DateTime.Now.Subtract(_downloadStartTimeStamp);

            var chunksRemaining = (totalBytes - bytesDownloaded) / chunkSize;

            return timeSpan / _totalDownloaded * chunksRemaining;
        }

        private async Task<(bool acceptsRanges, long contentLength)> GetInstallerInfoAsync(string contentFileUrl)
        {
            var result = await _webSystemCalls.GetHeadersAsync(contentFileUrl, _cancellationTokenSource.Token);

            result.EnsureSuccessStatusCode();

            var acceptsRanges = result.Headers.AcceptRanges.Contains("bytes");
            var contentLength = result.Content.Headers.ContentLength ?? 0;

            return (acceptsRanges, contentLength);
        }
    }
}