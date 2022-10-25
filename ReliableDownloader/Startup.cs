using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace ReliableDownloader
{
    public class Startup
    {
        public Startup()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json");

            Configuration = builder.Build();
        }

        private IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();

            var downloadSettings = Configuration.GetSection("DownloadSettings");
            services.Configure<DownloadOptions>(options => { downloadSettings.Bind(options); });

            var downloadOptions = downloadSettings.Get<DownloadOptions>() ?? new DownloadOptions();
            services.AddSingleton<IFileDownloader, FileDownloader>();
            services.AddHttpClient<IWebSystemCalls, WebSystemCalls>(client =>
                {
                    client.BaseAddress = new Uri(downloadOptions.BaseUrl);
                })
                .AddPolicyHandler(GetRetryPolicy(services, downloadOptions.RetryCount));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IServiceCollection serviceCollection,
            int retryCount)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<SocketException>()
                .OrResult(msg =>
                    msg.StatusCode == HttpStatusCode.TooManyRequests ||
                    msg.StatusCode == HttpStatusCode.InternalServerError)
                .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (outcome, timeSpan, retryAttempt, context) =>
                    {
                        Console.SetCursorPosition(Console.CursorLeft, 3);
                        Console.WriteLine(
                            $"\rRetry {retryAttempt}: Delaying for {timeSpan.TotalSeconds}secs due to '{outcome.Exception.Message}'.");
                    });
        }
    }
}