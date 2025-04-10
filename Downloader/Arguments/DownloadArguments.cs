using CommandLine;

namespace MusicApiDownloader.Arguments;

[Verb("download", HelpText = "Downloads whole playlist")]
internal class DownloadArguments : PathArgument {

    [Option('l', "list", Required = true, HelpText = "Id of the play list to download. You can find it by going to the desired playlist in the browser in the old website design. In the link, the last digit will be the playlist id.")]
    public int PlayListId { get; set; } = -1;

    [Option('p', "path", HelpText = "Path to the folder to save. Default is desktop")]
    public override string? SavePath { get; set; }

    [Option('u', "user", HelpText = "User name who playlist will download. (default is access token username)")]
    public string? UserName { get; set; }

    public void ValidateSavePath(string folderName) {
        SavePath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), folderName);
        if (!Directory.Exists(SavePath)) {
            Directory.CreateDirectory(SavePath);
        }
    }

}
