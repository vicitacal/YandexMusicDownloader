using System.Diagnostics;
using Yandex.Music.Api.Extensions.API;
using MusicApiDownloader.Arguments;
using Yandex.Music.Client;
using Newtonsoft.Json;
using Yandex.Music.Api.Models.Playlist;
using Yandex.Music.Downloader;
using MusicApiDownloader.Properties;
using System.Text;

namespace MusicApiDownloader;

internal class MusicDownloader {

    public MusicDownloader() {

    }

    private async Task AuthorizeAsync(string accessToken) {
        var client = new YandexMusicClientAsync();
        var authSuccess = await RetryAsync(() => client.Authorize(accessToken));
        if (!authSuccess) {
            throw new Exception("Cannot authorize");
        }
        _client = client;
    }

    public async Task Download(DownloadArguments args) {
        await AuthorizeAsync(GetAndSetAccessToken(args));
        args.UserName ??= _client.Account.Login;
        _targetPlaylist = await _client.GetPlaylist(args.UserName, args.PlayListId.ToString())
            ?? throw new Exception($"Cannot find playlist {args.UserName}:{args.PlayListId}");
        args.ValidateSavePath(_targetPlaylist.Title);

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
            if (_targetPlaylist == null) { throw new Exception($"Cannot serialize playlist info at {args.InfoFilePath}"); }
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
        await AuthorizeAsync(GetAndSetAccessToken(args));
        var info = await GetPlaylistInfoAsync(args);
        _targetPlaylist ??= await _client.GetPlaylist(info.UserName, info.Id.ToString());
        var report = new VerifyReport();
        var savePath = GetAndSetSavePath(args);

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

    private static string GetAndSetSavePath(PathArgument args) {
        if (args.SavePath is null) {
            args.SavePath = PropertiesStorage.Instance.SavePath ?? throw new Exception("Save path must be specified at least once");
        } else {
            PropertiesStorage.Instance.SavePath = args.SavePath;
            PropertiesStorage.Instance.Save();
        }
        return args.SavePath;
    }

    private static string GetAndSetAccessToken(PathArgument args) {
        if (args.AccessToken is null) {
            return PropertiesStorage.Instance.ApiToken ?? throw new Exception("Access token must be specified at least once");
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

}
