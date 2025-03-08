using Newtonsoft.Json;
using Yandex.Music.Downloader;

namespace MusicApiDownloader;

internal class VerifyReport {

    public const string ReportsFolderName = "VerifyReports";
    public const string ReportTimeFormat = @"dd.MM.yyyy_HH.mm.ss";

    public TimeSpan ExecutingTime => DateTime.Now - _startTime;

    [JsonRequired]
    public List<TrackInfo> RemovedTracks { get; } = []; //Tracks, that still exist in YaMusic, but removed from playlist

    [JsonRequired]
    public List<TrackInfo> LostTracks { get; } = [];  //Tracks, that removed from YaMusic or become unavailable

    [JsonRequired]
    public List<TrackInfo> NewTracks { get; } = []; //Tracks, that added to the playlist

    [JsonRequired]
    public List<TrackTransitionInfo> RecoveredTracks { get; } = []; //Tracks, that was unavailable, but now its available

    public VerifyReport() {
        _startTime = DateTime.Now;
    }

    public void Print(bool useMessageBox) {
        var message = "[Verification report]\n\n" +
                      $"__Removed tracks__:\n{string.Join('\n', RemovedTracks.Select(t => t.ToString()))}\n\n" +
                      $"__Lost tracks__:\n{string.Join('\n', LostTracks.Select(t => $"{t} is {t.Status}"))}\n\n" +
                      $"__Recovered tracks__:\n{string.Join('\n', RecoveredTracks.Select(t => t.ToString()))}\n\n" +
                      $"__New tracks__:\n{string.Join('\n', NewTracks.Select(t => t.ToString()))}";
        if (useMessageBox) {
            if (LostTracks.Count > 0 || RecoveredTracks.Count > 0 || RemovedTracks.Count > 0) {
                Program.ShowMessage(message);
            }
        } else {
            Console.WriteLine(message);
        }
    }

    public void Save(string basePath) {
        var folderPath = Path.Combine(basePath, ReportsFolderName);
        if (!Directory.Exists(folderPath)) {
            Directory.CreateDirectory(folderPath);
        }
        var targetPath = Path.ChangeExtension(Path.Combine(folderPath, DateTime.Now.ToString(ReportTimeFormat)), ".json");
        File.WriteAllText(targetPath, JsonConvert.SerializeObject(this));
    }

    private readonly DateTime _startTime;

}
