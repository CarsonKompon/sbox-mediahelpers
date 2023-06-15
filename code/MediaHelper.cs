using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using MediaHelpers.Util.Extensions;

namespace MediaHelpers;

public static class MediaHelper
{
    const string YOUTUBE_PLAYER = "https://www.youtube.com/youtubei/v1/player";

    public static bool IsYoutubeUrl(string url)
    {
        if(url.StartsWith("https://www.youtube.com/watch?v=")) return true;
        if(url.StartsWith("http://www.youtube.com/watch?v=")) return true;
        if(url.StartsWith("https://youtu.be/")) return true;
        if(url.StartsWith("http://youtu.be/")) return true;
        return false;
    }

    public static string GetIdFromYoutubeUrl(string url)
    {
        var uri = new Uri(url);
        var query = uri.Query;
        var queryDict = System.Web.HttpUtility.ParseQueryString(query);
        var v = queryDict.Get("v");
        return v;
    }

    public static async Task<string> GetUrlFromYoutubeId(string id)
    {
        YoutubePlayerResponse response = await GetYoutubePlayerResponse(id);
        if (response == null)
            return null;

        // TODO: A way to specify the preferred quality
        // Get the first format with VideoQualityLabel set to "1080p", and if none are found then "720p" and if none are found then "480p" and if none are found then the first
        var streams = response.GetStreams();
        var format = streams
            .WhereNotNull()
            .Where(f => f.VideoQualityLabel == "1080p" && f.AudioCodec != null)
            .FirstOrDefault()
            ?? streams
                .WhereNotNull()
                .Where(f => f.VideoQualityLabel == "720p" && f.AudioCodec != null)
                .FirstOrDefault()
                ?? streams
                    .WhereNotNull()
                    .Where(f => f.VideoQualityLabel == "480p" && f.AudioCodec != null)
                    .FirstOrDefault()
                    ?? streams
                        .WhereNotNull()
                        .Where(f => f.AudioCodec != null)
                        .FirstOrDefault()
                        ?? streams
                            .WhereNotNull()
                            .FirstOrDefault();
        
        if (format == null)
            return null;

        return format.Url;
    }

    public static async Task<string> GetUrlFromYoutubeUrl(string url)
    {
        string id = GetIdFromYoutubeUrl(url);
        return await GetUrlFromYoutubeId(id);
    }

    public static async Task<YoutubePlayerResponse> GetYoutubePlayerResponse(string videoId, CancellationToken cancellationToken = default)
    {
        // Pretend we are an android... thanks YoutubeExplode :)
        HttpContent content = new StringContent(
            // lang=json
            $$"""
            {
                "videoId": "{{videoId}}",
                "context": {
                    "client": {
                        "clientName": "ANDROID_TESTSUITE",
                        "clientVersion": "1.9",
                        "androidSdkVersion": 30,
                        "hl": "en",
                        "gl": "US",
                        "utcOffsetMinutes": 0
                    }
                }
            }
            """
        );

        Dictionary<string, string> headers = new Dictionary<string, string>()
        {
            {"User-Agent", "com.google.android.youtube/17.36.4 (Linux; U; Android 12; GB) gzip"}
        };

        var response = await Http.RequestAsync("POST", YOUTUBE_PLAYER, content);

        var playerResponse = YoutubePlayerResponse.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken)
        );

        if (!playerResponse.IsAvailable)
            return null;

        return playerResponse;
    }

}