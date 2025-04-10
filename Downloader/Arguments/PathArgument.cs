using CommandLine;

namespace MusicApiDownloader.Arguments;

abstract class PathArgument : ArgumentsBase {

    [Option('p', "path", HelpText = "Path to the folder where playlist was downloaded. The last path will be save for future use.")]
    public override string? SavePath { get; set; }

}
