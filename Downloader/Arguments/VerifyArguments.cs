using CommandLine;

namespace MusicApiDownloader.Arguments;

[Verb("verify", HelpText = "Compares the current state of the playlist with the last saved one and displays a message about the changes")]
internal class VerifyArguments : PathArgument {

}
