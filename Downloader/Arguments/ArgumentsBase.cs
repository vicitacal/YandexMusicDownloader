using CommandLine;
using MusicApiDownloader.Properties;

namespace MusicApiDownloader.Arguments;

internal abstract class ArgumentsBase
{

    [Option('t', "token", Required = true, HelpText = "Access token (https://yandex-music.readthedocs.io/en/main/token.html)")]
    public string? AccessToken { get; set; }

    [Option('s', "silent", HelpText = "Hide console and show only result by message box if any track change status.")]
    public bool Silent { get; set; }

    public abstract string? SavePath { get; set; }

    public string InfoFilePath => Path.Combine(SavePath!, Constants.PlaylistInfoFileName);

}
