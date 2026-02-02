using MusicApiDownloader.Arguments;
using MusicApiDownloader.Properties;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;
using System.Reflection;
using System.Text;
using Yandex.Music.Api.Extensions.API;
using Yandex.Music.Api.Models.Playlist;
using Yandex.Music.Client;
using Yandex.Music.Downloader;
using Task = System.Threading.Tasks.Task;

namespace MusicApiDownloader;

internal class MusicDownloader {

    public MusicDownloader() {

    }

    private async Task AuthorizeAsync(string accessToken) {
        var client = new YandexMusicClientAsync();
        var authSuccess = await RetryAsync(() => client.Authorize(accessToken));
        if (!authSuccess) {
            throw new UserErrorException("Cannot authorize");
        }
        _client = client;
    }

    public async Task Download(DownloadArguments args) {
        await AuthorizeAsync(GetAndSetAccessToken(args));
        args.UserName ??= _client.Account.Login;
        YPlaylist? playlist = null;
        if (int.TryParse(args.PlayListId, out var playListId)) {
            playlist = await _client.GetPlaylist(args.UserName, args.PlayListId);
        } else {
            playlist = await _client.GetPlaylist(args.PlayListId);
        }
        _targetPlaylist = playlist ?? throw new UserErrorException($"Cannot find playlist {args.UserName}:{args.PlayListId}");
        GetAndSetSavePath(args, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _targetPlaylist.Title));

        var playlistInfo = await GetPlaylistInfoAsync(args);

        Console.WriteLine($"Album: {_targetPlaylist.Title}");
        await DownloadTracks(playlistInfo.Tracks, args.SavePath!);

        playlistInfo.Serialize(args.InfoFilePath);
        var resultMessage = $"Downloading done successfully.\nUnavailable tracks:\n{string.Join('\n', playlistInfo.Tracks.Where(t => t.Status != TrackStatus.Valid).Select(t => t.ToString()))}";
        if (args.Silent) {
            Program.ShowMessage(resultMessage);
        } else {
            Console.WriteLine(resultMessage);
        }
    }

    public void ShowStatus(StatusArguments args) {
        var savePath = GetAndSetSavePath(args);
        var info = PlaylistInfo.Deserialize(args.InfoFilePath);
        var builder = new StringBuilder();
        builder.AppendLine($"[Playlist {info.Title} Id: {info.Id} Owner: {info.UserName} ]\n");
        foreach(var state in info.Tracks.GroupBy(t => t.Status)) {
            var count = state.Count();
            if (count == 0) { continue; }
            builder.AppendLine($"Status: {state.Key}, {count} track{(count > 1 ? "s" : "")}");
            foreach (var track in state) {
                builder.AppendLine($"\t {track}");
            }
            builder.AppendLine();
        }
        Console.WriteLine(builder.ToString());
    }

    private async Task<PlaylistInfo> GetPlaylistInfoAsync(ArgumentsBase args) {
        PlaylistInfo? playlistInfo = null;
        if (File.Exists(args.InfoFilePath)) {
            playlistInfo = JsonConvert.DeserializeObject<PlaylistInfo>(File.ReadAllText(args.InfoFilePath));
        }
        if (playlistInfo == null) {
            if (_targetPlaylist == null) { throw new UserErrorException($"Cannot find or deserialize playlist info at \"{args.InfoFilePath}\""); }
            playlistInfo = new PlaylistInfo(_targetPlaylist) { UserName = _targetPlaylist.Owner.Login, Id = _targetPlaylist.Kind };
        } else {
            _targetPlaylist = await _client.GetPlaylist(playlistInfo.UserName, playlistInfo.Id.ToString());
            var newTracks = _targetPlaylist.Tracks
                .SelectMusic()
                .Where(t => !playlistInfo.Tracks.Any(i => i.Id == t.Id)).Select(t => new TrackInfo(t))
                .ToList();
            playlistInfo.Tracks.AddRange(newTracks);
        }
        return playlistInfo;
    }

    private async Task DownloadTracks(IEnumerable<TrackInfo> tracks, string savePath) {
        var stw = Stopwatch.StartNew();
        int currentCount = 1;
        int tracksCount = tracks.Count();
        await Parallel.ForEachAsync(tracks, async (track, t) => {
            var message = await SynchronizeTrack(savePath, track);
            Console.WriteLine($"{currentCount++,4}/{tracksCount,-4} | {stw.Elapsed:hh\\:mm\\:ss} | {track.Title} is {(track.Status == TrackStatus.Valid ? "success" : "fail")} {message}");
        });
    }

    private async Task<string?> SynchronizeTrack(string savePath, TrackInfo track) {
        string? message = null;
        try {
            var apiTrack = await RetryAsync(() => _client.GetTrack(track.Id));
            if (apiTrack == null) {
                message = "Not found";
                track.Status = TrackStatus.Unexist;
                return message;
            } else if (!apiTrack.Available) {
                message = "Unavailable";
                track.Status = TrackStatus.Unavailable;
                return message;
            }
            var path = Path.Combine(savePath, track.FileName);
            if (File.Exists(path)) {
                message = "already downloaded";
            } else {
                await RetryAsync(() => apiTrack.SaveAsync(path));
            }
            track.Status = TrackStatus.Valid;
        } catch (Exception ex) {
            track.Status = TrackStatus.Undownloadable;
            track.DownloadError = ex.ToString();
            message = ex.Message;
        }
        return message;
    }

    public async Task Verify(VerifyArguments args) {
        if (args is ScheduleArguments) { return; }
        await AuthorizeAsync(GetAndSetAccessToken(args));
        var savePath = GetAndSetSavePath(args);
        var info = await GetPlaylistInfoAsync(args);
        _targetPlaylist ??= await _client.GetPlaylist(info.UserName, info.Id.ToString());
        var report = new VerifyReport();

        Console.WriteLine($"---Playlist \'{info.Title}\' is found.---\n\nChecking removed tracks");
        var removedTracks = info.Tracks.Where(t => !_targetPlaylist.Tracks.Any(p => p.Id == t.Id)).ToList();
        foreach (var track in removedTracks) {
            info.Tracks.Remove(track);
            try {
                File.Delete(Path.Combine(savePath, track.FileName));
            } catch (Exception ex) {
                Console.WriteLine($"Failed to delete {track.FileName}. {ex.Message}");
            }
            report.RemovedTracks.Add(track);
        }

        Console.WriteLine($"Found {report.RemovedTracks.Count} removed tracks.\nChecking lost tracks");
        foreach (var item in info.Tracks.Where(t => t.Status != TrackStatus.Valid && t.Status != TrackStatus.Unknown)) {
            var oldStatus = item.Status;
            await SynchronizeTrack(savePath, item);
            if (item.Status == TrackStatus.Valid) {
                report.RecoveredTracks.Add(new() { TargetTrack = item, OldStatus = oldStatus });
            }
        }

        Console.WriteLine($"Found {report.LostTracks.Count} lost tracks.\nChecking recovered tracks");
        foreach (var item in info.Tracks.Where(t => t.Status == TrackStatus.Valid)) {
            await SynchronizeTrack(savePath, item);
            if (item.Status != TrackStatus.Valid) {
                report.LostTracks.Add(item);
            }
        }


        Console.WriteLine($"Found {report.RecoveredTracks.Count} recovered tracks.\nChecking new tracks");
        foreach (var item in info.Tracks.Where(t => t.Status == TrackStatus.Unknown)) {
            await SynchronizeTrack(savePath, item);
            report.NewTracks.Add(item);
        }

        Console.WriteLine($"Found {report.NewTracks.Count} new tracks.\n");
        info.Serialize(args.InfoFilePath);
        report.Save(savePath);
        report.Print(args.Silent);
    }

    internal void Schedule(ScheduleArguments args) {
        using TaskService taskService = new();
        if (args.RemoveSchedule) {
            taskService.RootFolder.DeleteTask(_scheduleTaskName, false);
            Console.WriteLine("The task is successfully removed.");
            return;
        }
        var savePath = Path.GetFullPath(GetAndSetSavePath(args));
        if (!File.Exists(args.InfoFilePath)) {
            throw new UserErrorException($"Cannot find playlist info file at \"{args.InfoFilePath}\". Please run download or verify to specified folder first.");
        }
        Console.WriteLine("Please make sure that this program path will not change. Otherwise scheduled task will not work.");
        if (taskService.RootFolder.Tasks.Any(t => t.Name == _scheduleTaskName)) {
            taskService.RootFolder.DeleteTask(_scheduleTaskName, false);
            Console.WriteLine("The existing task is successfully removed.");
        }
        GetAndSetAccessToken(args);
        var exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var newTask = taskService.NewTask();
        newTask.RegistrationInfo.Description = "Launching playlist verification and downloads";
        var trigger = new DailyTrigger {
            StartBoundary = DateTime.Today.Add(args.CheckTimeParsed.ToTimeSpan()),
            DaysInterval = (short)args.IntervalDays
        };
        newTask.Triggers.Add(trigger);
        newTask.Actions.Add(new ExecAction(exePath, $"verify -p \"{savePath}\" -s"));
        newTask.Settings.StartWhenAvailable = true;
        taskService.RootFolder.RegisterTaskDefinition(_scheduleTaskName, newTask);
        Console.WriteLine($"The task '{_scheduleTaskName}' was successfully scheduled on {args.CheckTime} every {args.IntervalDays} day.");
    }

    private static string GetAndSetSavePath(PathArgument args, string? defaultPath = null) {
        if (args.SavePath is null) {
            args.SavePath = PropertiesStorage.Instance.SavePath ?? defaultPath ?? throw new UserErrorException("Save path must be specified in any action at least once");
        } else {
            PropertiesStorage.Instance.SavePath = args.SavePath;
            PropertiesStorage.Instance.Save();
        }
        if (!Directory.Exists(args.SavePath)) {
            Directory.CreateDirectory(args.SavePath);
        }
        return args.SavePath;
    }

    private static string GetAndSetAccessToken(PathArgument args) {
        if (args.AccessToken is null) {
            return PropertiesStorage.Instance.ApiToken ?? throw new UserErrorException("Access token must be specified in any action at least once");
        } else {
            PropertiesStorage.Instance.ApiToken = args.AccessToken;
            PropertiesStorage.Instance.Save();
            return args.AccessToken;
        }
    }

    private async Task RetryAsync(Func<Task> action) {
        int tryLeft = Constants.RetryCount;
        while (tryLeft-- > 0) {
            try {
                await action();
                break;
            } catch (Exception) { }
        }
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> action) {
        int tryLeft = Constants.RetryCount;
        while (true) {
            try {
                return await action();
            } catch (Exception) {
                if (tryLeft-- <= 0) {
                    throw;
                }
            }
        }
    }

    private YPlaylist? _targetPlaylist;
    private YandexMusicClientAsync _client = null!;
    private const string _scheduleTaskName = "Yandex music playlist check";

}
