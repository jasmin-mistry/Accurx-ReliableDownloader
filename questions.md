# Questions

## How did you approach solving the problem?

- Firstly, I wanted to make sure that I use the dependency registration 
    - to allow the `WebSystemCalls` class to inject the `HttpClient` by adding Typed HttpClient to the HttpClientFactory. This way I will be able to mock the HttpClientHandler in the unit tests,
    - register the `WebSystemCalls`, `FileDownloader` etc. in `Startup.cs` file,
    - injected the `IWebSystemCalls` in `FileDownloader`,
- created the `DownloadOptions` which contains the baseUrl, endPoint, filePath, downloadBatchSize and retryCount, this can be configured in the `appsettings.json` file,
- Wrote unit tests scenarios in `FileDownloadTest` class;
    - for ArgumentNullExceptions,
    - to call the get header info and get partial content by mocking the HttpClientHandler,
- updated the `Program.cs` to manually test the actual file download functionality works as expected and to allow the user to cancel the download if already started by using the `CancellationTokenSource` and passing it onto the `WebSystemCalls` API calls,
- To make the `WebSystemCalls` API calls resilient, added the Polly policy `WaitAndRetry` with `retryCount` configurable from the `appsettings.json`.

## How did you verify your solution works correctly?

First with the unit tests see `FileDownloaderTest.cs` file and the manually testing.

## How long did you spend on the exercise?

I took approx 3 hours and 30 mins

## What would you add if you had more time and how?

- Writing intergration test to make sure WebSystemCalls API code works by using Wiremock.Net,
- Implement the file integrity check using MD5,
- Add unit test scenario to check if the file already exists and resuming file download,
- Add more logging for observability and console message for better user experience,


