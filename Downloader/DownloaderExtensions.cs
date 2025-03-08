using Newtonsoft.Json;
using Yandex.Music.Api.Models.Track;

namespace MusicApiDownloader;

internal static class DownloaderExtensions
{

    public static IEnumerable<YTrackContainer> SelectMusic(this IEnumerable<YTrackContainer> traks) => 
        traks.Where(t => t.Track.Type == TrackType.music.ToString());

}
