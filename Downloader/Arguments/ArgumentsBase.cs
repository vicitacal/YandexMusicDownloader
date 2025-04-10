using CommandLine;
using MusicApiDownloader.Properties;

namespace MusicApiDownloader.Arguments;

internal abstract class ArgumentsBase {

    [Option('t', "token", HelpText = "Access token. How to get: https://yandex-music.readthedocs.io/en/main/token.html The last entered token will be saved and will be used in the future.")]
    public string? AccessToken { get; set; }

    [Option('s', "silent", HelpText = "Hide console and show only result by message box if any track change status.")]
    public bool Silent { get; set; }

    [Option('i', "incog", HelpText = "Do not save access token for future use.")]
    public bool Incognito { get; set; } = false;

    public abstract string? SavePath { get; set; }

    public string InfoFilePath => Path.Combine(SavePath!, Constants.PlaylistInfoFileName);

}
