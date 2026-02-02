using CommandLine;
using MusicApiDownloader;
using MusicApiDownloader.Arguments;
using System.Runtime.InteropServices;
using System.Text;

namespace Yandex.Music.Downloader;

public partial class Program {

    private static async Task Main(string[] args) {
        var silent = args.Any(a => a == "-s");
        if (!silent) {
            ShowConsole();
        }
        var downloader = new MusicDownloader();
        var result = Parser.Default.ParseArguments<DownloadArguments, VerifyArguments, StatusArguments, ScheduleArguments>(args);
        try {
            result.WithParsed<ScheduleArguments>(downloader.Schedule);
            await result.WithParsedAsync<DownloadArguments>(downloader.Download);
            await result.WithParsedAsync<VerifyArguments>(downloader.Verify);
            result.WithParsed<StatusArguments>(downloader.ShowStatus);
        } catch (Exception ex) {
            var message = ex is UserErrorException ? ex.Message : $"Action ends with unknown exception {ex}";
            if (silent) {
                ShowMessage(message);
            } else {
                Console.WriteLine(message);
            }
        }
        if (!silent) {
            Console.ReadKey();
        }
    }

    private static void ShowConsole() {
        AllocConsole();
        Console.OutputEncoding = Encoding.UTF8;
    }

    internal static int ShowMessage(string message) => MessageBox(0, message, "Yandex music downloader", 0);

    [DllImport("User32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr h, string m, string c, int type);

    [LibraryImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial int AllocConsole();

}

