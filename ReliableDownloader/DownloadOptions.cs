namespace ReliableDownloader
{
    public class DownloadOptions
    {
        public string BaseUrl { get; set; }

        public string Endpoint { get; set; }

        public string FilePath { get; set; }

        public int DownloadBatchSize { get; set; }
        
        public int RetryCount { get; set; }
    }
}