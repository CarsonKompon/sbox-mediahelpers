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
        if(url.StartsWith("https://www.youtube.com/shorts/")) return true;
        if(url.StartsWith("http://www.youtube.com/shorts/")) return true;
        if(url.StartsWith("https://youtu.be/")) return true;
        if(url.StartsWith("http://youtu.be/")) return true;
        return false;
    }

    public static string GetIdFromYoutubeUrl(string url)
    {
        var uri = new Uri(url);

        if(url.Contains("youtu.be/") || url.Contains("/shorts/"))
        {
            return uri.Segments.Last();
        }



        var query = uri.Query;
        var queryDict = System.Web.HttpUtility.ParseQueryString(query);
        var v = queryDict.Get("v");
        return v;
    }

    public static async Task<YoutubePlayerResponse> GetYoutubePlayerResponseFromId(string id)
    {
        return await GetYoutubePlayerResponse(id);
    }

    public static async Task<YoutubePlayerResponse> GetYoutubePlayerResponseFromUrl(string url)
    {
        string id = GetIdFromYoutubeUrl(url);
        return await GetYoutubePlayerResponse(id);
    }

    public static async Task<string> GetUrlFromYoutubeId(string id)
    {
        YoutubePlayerResponse response = await GetYoutubePlayerResponse(id);
        if (response == null)
            return null;

        return response.GetStreamUrl();
    }

    public static async Task<string> GetUrlFromYoutubeUrl(string url)
    {
        string id = GetIdFromYoutubeUrl(url);
        return await GetUrlFromYoutubeId(id);
    }

    private static async Task<YoutubePlayerResponse> GetYoutubePlayerResponse(string videoId, CancellationToken cancellationToken = default)
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

        var response = await Http.RequestAsync(YOUTUBE_PLAYER, "POST", content);

        var playerResponse = YoutubePlayerResponse.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken)
        );

        if (!playerResponse.IsAvailable)
            return null;

        return playerResponse;
    }

}