namespace MusicApiDownloader;
#nullable disable

internal class TrackTransitionInfo {

    public TrackInfo TargetTrack { get; init; }

    public TrackStatus OldStatus { get; init; }

    public override string ToString() => $"{TargetTrack} from {OldStatus} to {TargetTrack.Status}";

}
