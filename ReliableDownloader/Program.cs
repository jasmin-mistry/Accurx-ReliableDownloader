using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ReliableDownloader
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            var startup = new Startup();
            startup.ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            var fileDownload = serviceProvider.GetService<IFileDownloader>();

            var left = Console.CursorLeft;
            try
            {
                bool result;
                do
                {
                    var downloadTask = fileDownload.DownloadFile(progress =>
                    {
                        Console.SetCursorPosition(left, 2);
                        Console.Write(progress.ProgressPercent < 100
                            ? $"{progress.ProgressPercent}% downloaded so far"
                            : "Download completed successfully.");
                    });

                    Console.SetCursorPosition(left, 0);
                    Console.WriteLine("Press 'q' to stop download.");
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Q)
                    {
                        Console.SetCursorPosition(Console.CursorLeft, 4);
                        Console.WriteLine("\rQuitting...");
                        fileDownload.CancelDownloads();
                    }

                    result = await downloadTask;
                } while (!result);
            }
            catch (Exception ex)
            {
                Console.SetCursorPosition(Console.CursorLeft, 5);
                Console.WriteLine(
                    $"\rUnable to download file after several retries with error: '{ex.Message}'. Exiting now.");
            }
        }
    }
}
