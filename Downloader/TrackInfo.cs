using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Yandex.Music.Api.Models.Track;

namespace MusicApiDownloader;

internal class TrackInfo {

    public TrackInfo() {

    }

    public TrackInfo(YTrackContainer track) {
        Id = track.Track.Id;
        Title = track.Track.Title;
        ArtistTitle = string.Join(", ", track.Track.Artists.Select(a => a.Name));
    }

    public string? DownloadError { get; set; }

    [JsonRequired]
    [JsonConverter(typeof(StringEnumConverter))]
    public TrackStatus Status { get; set; }

    [JsonRequired]
    public string Id { get; init; } = null!;

    [JsonRequired]
    public string Title { get; init; } = null!;

    public string ArtistTitle { get; init; } = null!;

    [JsonIgnore]
    public string FileName {
        get {
            var badSymbols = Path.GetInvalidFileNameChars().Append(']').Append('[');
            return $"[{Id}] {string.Concat((Title.Length > 40 ? Title[..40] : Title).Where(c => !badSymbols.Contains(c)))}{_musicExtension}";
        }
    }

    public override string ToString() => $"[{Id}] {(string.IsNullOrEmpty(ArtistTitle) ? "*Unknown artist*" : ArtistTitle)} - {Title}";

    private const string _musicExtension = ".mp3";

}

internal enum TrackType {
    music   //Do not rename
}

internal enum TrackStatus {
    Unknown = 0,        //Initial status that mean unchecked
    Valid,              //The track is exist and available for listen
    Unavailable,        //The track is exist, but not available for listen
    Unexist,            //The track is not exist
    Undownloadable,     //The track is exist, but download is failed for some reason
    Replaced            //The track was unavailable, but replaced with manual loaded copy
}
