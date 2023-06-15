using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediaHelpers.Util;
using MediaHelpers.Util.Extensions;

namespace MediaHelpers;

public partial class YoutubePlayerResponse
{
    private readonly JsonElement _content;

    private JsonElement? Playability => _content.GetPropertyOrNull("playabilityStatus");

    private string? PlayabilityStatus => Playability?
            .GetPropertyOrNull("status")?
            .GetStringOrNull();

    public string? PlayabilityError => Playability?
            .GetPropertyOrNull("reason")?
            .GetStringOrNull();

    public bool IsAvailable => !string.Equals(PlayabilityStatus, "error", StringComparison.OrdinalIgnoreCase) &&
        Details is not null;

    public bool IsPlayable => string.Equals(PlayabilityStatus, "ok", StringComparison.OrdinalIgnoreCase);

    private JsonElement? Details => _content.GetPropertyOrNull("videoDetails");

    public string? Title => Details?
            .GetPropertyOrNull("title")?
            .GetStringOrNull();

    public string? ChannelId => Details?
            .GetPropertyOrNull("channelId")?
            .GetStringOrNull();

    public string? Author => Details?
            .GetPropertyOrNull("author")?
            .GetStringOrNull();

    public DateTimeOffset? UploadDate => _content
            .GetPropertyOrNull("microformat")?
            .GetPropertyOrNull("playerMicroformatRenderer")?
            .GetPropertyOrNull("uploadDate")?
            .GetDateTimeOffset();

    public TimeSpan? Duration => Details?
            .GetPropertyOrNull("lengthSeconds")?
            .GetStringOrNull()?
            .ParseDoubleOrNull()?
            .Pipe(TimeSpan.FromSeconds);

    public IReadOnlyList<YoutubeThumbnailData> Thumbnails => Details?
            .GetPropertyOrNull("thumbnail")?
            .GetPropertyOrNull("thumbnails")?
            .EnumerateArrayOrNull()?
            .Select(j => new YoutubeThumbnailData(j))
            .ToArray() ??
        Array.Empty<YoutubeThumbnailData>();

    public IReadOnlyList<string> Keywords => Details?
            .GetPropertyOrNull("keywords")?
            .EnumerateArrayOrNull()?
            .Select(j => j.GetStringOrNull())
            .WhereNotNull()
            .ToArray() ??
        Array.Empty<string>();

    public string? Description => Details?
            .GetPropertyOrNull("shortDescription")?
            .GetStringOrNull();

    public long? ViewCount => Details?
            .GetPropertyOrNull("viewCount")?
            .GetStringOrNull()?
            .ParseLongOrNull();

    public string? PreviewVideoId => Playability?
            .GetPropertyOrNull("errorScreen")?
            .GetPropertyOrNull("playerLegacyDesktopYpcTrailerRenderer")?
            .GetPropertyOrNull("trailerVideoId")?
            .GetStringOrNull() ??

        Playability?
            .GetPropertyOrNull("errorScreen")?
            .GetPropertyOrNull("ypcTrailerRenderer")?
            .GetPropertyOrNull("playerVars")?
            .GetStringOrNull()?
            .Pipe(UrlEx.GetQueryParameters)
            .GetValueOrDefault("video_id") ??

        Playability?
            .GetPropertyOrNull("errorScreen")?
            .GetPropertyOrNull("ypcTrailerRenderer")?
            .GetPropertyOrNull("playerResponse")?
            .GetStringOrNull()?
            // YouTube uses weird base64-like encoding here that I don't know how to deal with.
            // It's supposed to have JSON inside, but if extracted as is, it contains garbage.
            // Luckily, some of the text gets decoded correctly, which is enough for us to
            // extract the preview video ID using regex.
            .Replace('-', '+')
            .Replace('_', '/')
            .Pipe(Convert.FromBase64String)
            .Pipe(Encoding.UTF8.GetString)
            .Pipe(s => Regex.Match(s, @"video_id=(.{11})").Groups[1].Value)
            .NullIfWhiteSpace();

    private JsonElement? StreamingData => _content.GetPropertyOrNull("streamingData");

    public string? DashManifestUrl => StreamingData?
            .GetPropertyOrNull("dashManifestUrl")?
            .GetStringOrNull();

    public string? HlsManifestUrl => StreamingData?
            .GetPropertyOrNull("hlsManifestUrl")?
            .GetStringOrNull();

    public IReadOnlyList<IYoutubeStreamData> GetStreams()
    {
        var result = new List<IYoutubeStreamData>();

        var muxedStreams = StreamingData?
            .GetPropertyOrNull("formats")?
            .EnumerateArrayOrNull()?
            .Select(j => new StreamData(j));

        if (muxedStreams is not null)
            result.AddRange(muxedStreams);

        var adaptiveStreams = StreamingData?
            .GetPropertyOrNull("adaptiveFormats")?
            .EnumerateArrayOrNull()?
            .Select(j => new StreamData(j));

        if (adaptiveStreams is not null)
            result.AddRange(adaptiveStreams);

        return result;
    }

    public IReadOnlyList<ClosedCaptionTrackData> ClosedCaptionTracks =>
        _content
            .GetPropertyOrNull("captions")?
            .GetPropertyOrNull("playerCaptionsTracklistRenderer")?
            .GetPropertyOrNull("captionTracks")?
            .EnumerateArrayOrNull()?
            .Select(j => new ClosedCaptionTrackData(j))
            .ToArray() ??

        Array.Empty<ClosedCaptionTrackData>();

    public YoutubePlayerResponse(JsonElement content) => _content = content;
}

partial class YoutubePlayerResponse
{
    public class ClosedCaptionTrackData
    {
        private readonly JsonElement _content;

        public string? Url => _content
                .GetPropertyOrNull("baseUrl")?
                .GetStringOrNull();

        public string? LanguageCode => _content
                .GetPropertyOrNull("languageCode")?
                .GetStringOrNull();

        public string? LanguageName => _content
                .GetPropertyOrNull("name")?
                .GetPropertyOrNull("simpleText")?
                .GetStringOrNull() ??

            _content
                .GetPropertyOrNull("name")?
                .GetPropertyOrNull("runs")?
                .EnumerateArrayOrNull()?
                .Select(j => j.GetPropertyOrNull("text")?.GetStringOrNull())
                .WhereNotNull()
                .ConcatToString();

        public bool IsAutoGenerated => _content
                .GetPropertyOrNull("vssId")?
                .GetStringOrNull()?
                .StartsWith("a.", StringComparison.OrdinalIgnoreCase) ?? false;

        public ClosedCaptionTrackData(JsonElement content) => _content = content;
    }
}

partial class YoutubePlayerResponse
{
    public class StreamData : IYoutubeStreamData
    {
        private readonly JsonElement _content;

        public int? Itag => _content
                .GetPropertyOrNull("itag")?
                .GetInt32OrNull();

        private IReadOnlyDictionary<string, string>? CipherData => _content
                .GetPropertyOrNull("cipher")?
                .GetStringOrNull()?
                .Pipe(UrlEx.GetQueryParameters) ??

            _content
                .GetPropertyOrNull("signatureCipher")?
                .GetStringOrNull()?
                .Pipe(UrlEx.GetQueryParameters);

        public string? Url => _content
                .GetPropertyOrNull("url")?
                .GetStringOrNull() ??

            CipherData?.GetValueOrDefault("url");

        public string? Signature => CipherData?.GetValueOrDefault("s");

        public string? SignatureParameter => CipherData?.GetValueOrDefault("sp");

        public long? ContentLength => _content
                .GetPropertyOrNull("contentLength")?
                .GetStringOrNull()?
                .ParseLongOrNull() ??

            Url?
                .Pipe(s => UrlEx.TryGetQueryParameterValue(s, "clen"))?
                .NullIfWhiteSpace()?
                .ParseLongOrNull();

        public long? Bitrate => _content
                .GetPropertyOrNull("bitrate")?
                .GetInt64OrNull();

        private string? MimeType => _content
                .GetPropertyOrNull("mimeType")?
                .GetStringOrNull();

        public string? Container => MimeType?
                .SubstringUntil(";")
                .SubstringAfter("/");

        private bool IsAudioOnly => MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ?? false;

        public string? Codecs => MimeType?
                .SubstringAfter("codecs=\"")
                .SubstringUntil("\"");

        public string? AudioCodec => IsAudioOnly
                ? Codecs
                : Codecs?.SubstringAfter(", ").NullIfWhiteSpace();

        public string GetVideoCodec()
        {
            var codec = IsAudioOnly
                ? null
                : Codecs?.SubstringUntil(", ").NullIfWhiteSpace();

            // "unknown" value indicates av01 codec
            if (string.Equals(codec, "unknown", StringComparison.OrdinalIgnoreCase))
                return "av01.0.05M.08";

            return codec;
        }

        public string? VideoQualityLabel => _content
                .GetPropertyOrNull("qualityLabel")?
                .GetStringOrNull();

        public int? VideoWidth => _content
                .GetPropertyOrNull("width")?
                .GetInt32OrNull();

        public int? VideoHeight => _content
                .GetPropertyOrNull("height")?
                .GetInt32OrNull();

        public int? VideoFramerate => _content
                .GetPropertyOrNull("fps")?
                .GetInt32OrNull();

        public StreamData(JsonElement content) => _content = content;
    }
}

public partial class YoutubePlayerResponse
{
    public static YoutubePlayerResponse Parse(string raw) => new(Json.Parse(raw));
}


public class YoutubeThumbnailData
{
    private readonly JsonElement _content;

    public YoutubeThumbnailData(JsonElement content) => _content = content;

    public string? Url => _content.GetPropertyOrNull("url")?.GetStringOrNull();

    public int? Width => _content.GetPropertyOrNull("width")?.GetInt32OrNull();

    public int? Height => _content.GetPropertyOrNull("height")?.GetInt32OrNull();
}

public interface IYoutubeStreamData
{
    int? Itag { get; }

    string? Url { get; }

    string? Signature { get; }

    string? SignatureParameter { get; }

    long? ContentLength { get; }

    long? Bitrate { get; }

    string? Container { get; }

    string? AudioCodec { get; }

    string? VideoQualityLabel { get; }

    int? VideoWidth { get; }

    int? VideoHeight { get; }

    int? VideoFramerate { get; }
}