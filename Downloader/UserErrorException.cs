namespace MusicApiDownloader;
#nullable enable

internal class UserErrorException : Exception {

    public UserErrorException() {

    }

    public UserErrorException(string? message) : base(message) {

    }

}
