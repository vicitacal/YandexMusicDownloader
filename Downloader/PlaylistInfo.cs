using Newtonsoft.Json;
using Yandex.Music.Api.Models.Playlist;
using Yandex.Music.Api.Models.Track;
#nullable disable

namespace MusicApiDownloader;

internal class PlaylistInfo
{

    public PlaylistInfo()
    {
        Tracks = [];
    }

    public PlaylistInfo(YPlaylist playlist)
    {
        Title = playlist.Title;
        Id = playlist.Kind;
        Tracks = playlist.Tracks
            .SelectMusic()
            .Select(t => new TrackInfo(t))
            .ToList();
    }

    public string Title { get; init; }

    public string Id { get; init; }

    public string UserName { get; init; }

    public List<TrackInfo> Tracks { get; }

    public void Serialize(string path) {
        File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public static PlaylistInfo Deserialize(string path) {
        if (!File.Exists(path)) {
            throw new Exception($"Cannot find track info file at \"{path}\"");
        }
        return JsonConvert.DeserializeObject<PlaylistInfo>(File.ReadAllText(path))
            ?? throw new Exception("Cannot desirialize playlist info");
    }

}
