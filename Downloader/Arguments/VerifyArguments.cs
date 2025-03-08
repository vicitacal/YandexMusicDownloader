using CommandLine;

namespace MusicApiDownloader.Arguments;
#nullable disable

[Verb("verify", HelpText = "Check all tracks for availability in the playlist")]
internal class VerifyArguments : ArgumentsBase
{

    [Option('p', "path", Required = true, HelpText = "Path to the folder to verify")]
    public override string SavePath { get; set; }

}
