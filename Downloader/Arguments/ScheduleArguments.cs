using CommandLine;

namespace MusicApiDownloader.Arguments;
#nullable enable

[Verb("schedule", HelpText = "Schedules playlist verification and downloads")]
internal class ScheduleArguments : VerifyArguments {

    [Option('m', "time", HelpText = $"Time of day to perform the check ({TimeFormat})", Default = "12:00")]
    public string CheckTime { get; set; } = "";

    [Option('d', "interval", HelpText = "Interval in days between checks", Default = 1)]
    public int IntervalDays { get; set; }

    [Option('r', "remove", HelpText = "Remove the scheduled task. Ignore all other options.")]
    public bool RemoveSchedule { get; set; }

    internal TimeOnly CheckTimeParsed => TimeOnly.ParseExact(CheckTime, TimeFormat);

    private const string TimeFormat = "HH:mm";

}
