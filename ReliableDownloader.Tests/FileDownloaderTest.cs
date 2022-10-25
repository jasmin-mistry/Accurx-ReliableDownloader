using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace ReliableDownloader.Tests
{
    public class FileDownloaderTest
    {
        private readonly Fixture _fixture;
        private readonly IOptions<DownloadOptions> mockOptions;

        public FileDownloaderTest()
        {
            mockOptions = Options.Create(new DownloadOptions
            {
                BaseUrl = "http://example.com",
                Endpoint = "/testImage/orig.jpg",
                DownloadBatchSize = 100,
                FilePath = Path.Combine(Environment.CurrentDirectory, $"testImage_{DateTime.Now.ToFileTime()}.jpg")
            });
            _fixture = new Fixture();
        }

        [Fact]
        public async Task DownloadFile_ShouldThrowException_WhenFilePathIsNotAvailable()
        {
            // Arrange
            mockOptions.Value.FilePath = string.Empty;
            var sut = GetSut(10);

            // Act
            var ex = await Record.ExceptionAsync(async () => await sut.DownloadFile(progress => { }));

            // Assert
            ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
        }

        [Fact]
        public async Task DownloadFile_ShouldThrowException_WhenBaseUrlIsNotAvailable()
        {
            // Arrange
            mockOptions.Value.BaseUrl = string.Empty;
            var sut = GetSut(10);

            // Act
            var ex = await Record.ExceptionAsync(async () => await sut.DownloadFile(progress => { }));

            // Assert
            ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
        }

        [Fact]
        public async Task DownloadFile_ShouldThrowException_WhenEndPointIsNotAvailable()
        {
            // Arrange
            mockOptions.Value.Endpoint = string.Empty;
            var sut = GetSut(10);

            // Act
            var ex = await Record.ExceptionAsync(async () => await sut.DownloadFile(progress => { }));

            // Assert
            ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
        }

        [Fact]
        public async Task DownloadFile_ShouldDownloadEntireFile()
        {
            // Arrange
            mockOptions.Value.FilePath =
                Path.Combine(Environment.CurrentDirectory, $"test1_{DateTime.Now.ToFileTime()}.jpg");
            const int contentLength = 100;
            var sut = GetSut(contentLength);

            // Act
            var result = await sut.DownloadFile(progress =>
                Console.WriteLine($"{progress.ProgressPercent}% downloaded so far"));

            // Assert
            result.Should().Be(true);
            var file = new FileInfo(mockOptions.Value.FilePath);
            file.Exists.Should().BeTrue();
            file.Length.Should().Be(contentLength);
        }

        [Fact]
        public async Task DownloadFile_ShouldDownloadPartialFile_WhenDownloadIsCancelled()
        {
            // Arrange
            const int contentLength = 1234;
            mockOptions.Value.DownloadBatchSize = 100;
            mockOptions.Value.FilePath =
                Path.Combine(Environment.CurrentDirectory, $"test2_{DateTime.Now.ToFileTime()}.jpg");
            var sut = GetSut(contentLength);

            // Act
            var result = await sut.DownloadFile(progress =>
            {
                if (progress.ProgressPercent > 20) sut.CancelDownloads();
            });

            // Assert
            result.Should().Be(false);
            var file = new FileInfo(mockOptions.Value.FilePath);
            file.Exists.Should().BeTrue();
            file.Length.Should().Be(mockOptions.Value.DownloadBatchSize);
        }

        [Fact]
        public async Task DownloadFile_ShouldDownloadContent()
        {
            // Arrange
            mockOptions.Value.FilePath =
                Path.Combine(Environment.CurrentDirectory, $"test3_{DateTime.Now.ToFileTime()}.jpg");
            const int contentLength = 100;
            var sut = GetSut(contentLength, acceptRanges: false);

            // Act
            var result = await sut.DownloadFile(progress =>
                Console.WriteLine($"{progress.ProgressPercent}% downloaded so far"));

            // Assert
            result.Should().Be(true);
            var file = new FileInfo(mockOptions.Value.FilePath);
            file.Exists.Should().BeTrue();
            file.Length.Should().Be(contentLength);
        }

        [Fact]
        public async Task DownloadFile_ShouldThrowException_WhenGetHeadersFails()
        {
            // Arrange
            var failedResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            };
            var sut = GetSut(10, failedResponse);

            // Act
            var ex = await Record.ExceptionAsync(async () => await sut.DownloadFile(progress => { }));

            // Assert
            ex.Should().NotBeNull().And.BeOfType<HttpRequestException>();
        }

        [Fact]
        public async Task DownloadFile_ShouldThrowException_WhenDownloadPartialContentFails()
        {
            // Arrange
            var failedResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            };
            var sut = GetSut(10, responseFile: failedResponse);

            // Act
            var ex = await Record.ExceptionAsync(async () => await sut.DownloadFile(progress => { }));

            // Assert
            ex.Should().NotBeNull().And.BeOfType<HttpRequestException>();
        }

        private FileDownloader GetSut(int contentLength, HttpResponseMessage responseFileInfo = default,
            HttpResponseMessage responseFile = default, bool acceptRanges = true)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            SetupFileInfoResponse(contentLength, responseFileInfo, acceptRanges, handlerMock);

            SetupPartialContentResponse(responseFile, handlerMock);

            var httpClient = new HttpClient(handlerMock.Object);
            var webCalls = new WebSystemCalls(httpClient);
            return new FileDownloader(webCalls, mockOptions);
        }

        private void SetupPartialContentResponse(HttpResponseMessage responseFile, Mock<HttpMessageHandler> handlerMock)
        {
            responseFile ??= new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(_fixture.CreateMany<byte>(mockOptions.Value.DownloadBatchSize).ToArray())
            };

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(message => message.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseFile);
        }

        private void SetupFileInfoResponse(int contentLength, HttpResponseMessage responseFileInfo, bool acceptRanges,
            Mock<HttpMessageHandler> handlerMock)
        {
            responseFileInfo ??= new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(_fixture.CreateMany<byte>(contentLength).ToArray())
            };
            var headers = responseFileInfo.Headers;
            if (acceptRanges) headers.TryAddWithoutValidation("Accept-Ranges", new[] { "bytes" });
            headers.TryAddWithoutValidation("ContentLength", contentLength.ToString());

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(message => message.Method == HttpMethod.Head),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseFileInfo);
        }
    }
}