# sbox-mediahelpers
A library used to retrieve media urls for the VideoPlayer class in S&box

## Helpful Information

The Kick.com-related functions are currently not working as intended as they require a cloudflare bypass. I'm not sure I can natively build that in S&box so I am currently waiting for an official Kick.com API :)

## How to use

1. Add the package to your game/addon:
![image](https://github.com/CarsonKompon/sbox-mediahelpers/assets/5159369/c982d29e-d155-4a48-83e8-4f37ef5685d6)

2. Make sure you're `using MediaHelpers;` and you're good to go! Here's some example code:

```cs
public void PlayVideo(string url)
{
  if(MediaHelper.IsYoutubeUrl(url))
  {
    string streamUrl = MediaHelper.GetUrlFromYoutubeUrl(url);
    videoPlayer.Play(streamUrl);
  }
  else
  {
    videoPlayer.Play(url);
  }
}
```

## Credit

Based on YoutubeExplode: https://github.com/Tyrrrz/YoutubeExplode
