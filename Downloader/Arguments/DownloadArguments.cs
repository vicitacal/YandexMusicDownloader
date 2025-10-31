using CommandLine;
using MusicApiDownloader.Properties;

namespace MusicApiDownloader.Arguments;

[Verb("download", HelpText = "Downloads whole playlist")]
internal class DownloadArguments : PathArgument {

    [Option('l', "list", Required = true, HelpText = "Id or Uid of the play list to download. You can find it by going to the desired playlist in the browser in the old website design. In the link, the last digit will be the playlist id.")]
    public string PlayListId { get; set; } = string.Empty;

    [Option('p', "path", HelpText = "Path to the folder to save. Last choose will save and use for later runs. Default is desktop.")]
    public override string? SavePath { get; set; }

    [Option('u', "user", HelpText = "User name who playlist will download. (default is access token username)")]
    public string? UserName { get; set; }

    public void ValidateSavePath(string folderName) {
        SavePath ??= PropertiesStorage.Instance.SavePath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), folderName);
        if (!Directory.Exists(SavePath)) {
            Directory.CreateDirectory(SavePath);
        }
    }

}
