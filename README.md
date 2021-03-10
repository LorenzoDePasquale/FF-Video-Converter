# FF Video Converter
Simple video converter with cutting and cropping functionality, powered by the ffmpeg project

[Youtube demo](https://youtu.be/F1RwbC_K_4o)

## Features

- Encode video files in H264 and H265 and export them in mp4 or mkv format <p align="center"><img width="700" src="https://i.imgur.com/SnKBqsh.png"></p>
- Experimental support for Nvidia H264 and H265 hardware accelerated encoders (only on RTX video cards, because  of lack of B-frame support on older gpus) 
- Experimental support for Intel H264 and H265 hardware accelerated encoders; on computers with a dedicated GPU, Intel iGPU must be manually enabled for QuickSync to work
- Change framerate and resolution
- Trim or cut the video, with or without re-encoding it (with precise cutting at keyframe positions) <p align="center"><img width="700" src="https://i.imgur.com/vXd16ef.png"></p>
- Visually crop and rotate the video <p align="center"><img width="700" src="https://i.imgur.com/ZhDiTfX.png"></p>
- Preview encoding quality settings with before-after clips
- Change default audio track, change volume per track, or remove them altogether <p align="center"><img width="700" src="https://i.imgur.com/uMWnNxM.png"></p>
- Open network media and convert it while it's still downloading <p align="center"><img width="700" src="https://i.imgur.com/71B5ixJ.gif"></p>
- Open videos from Youtube, Reddit, Twitter, Facebook and Instagram<p align="center"><img width="700" src="https://i.imgur.com/VuYrnTr.gif"></p>
- Queue system


## Current issues and limitations

- Containers with multiple video streams are not supported (they will be opened correctly but only the first video stream will be considered, the others will be discarded)
- HDR movies are not supported (they can be opened, but hdr will be lost after conversion)
- [ffprobe bug] With some codecs (only vp9 confirmed for now, but could be more) ffprobe reports some i-frames as keyframes, although these frames can't be used as cut points. This means that when cutting on one of these fake keyframes the video will be cut at the previus real keyframe
- [ffprobe bug] It's impossible to retreive the title of an audio stream inside a mp4 container (tag:title missing from ffprobe show_streams output)
- [ffmpeg bug] ffmpeg matroska muxer doesn't write streams size and bitrate metadata in the output mkv container; this means that it's impossible to retreive these informations (by this program or any equivalent software) from .mkv files created by ffmpeg. [more info on this bug](https://trac.ffmpeg.org/ticket/7467)


## Compatibility
Requires .net framework 4.7.2 or newer, included with Windows 10 since v1803 (April 2018 Update) or avaiable as a [separate download](https://dotnet.microsoft.com/download/dotnet-framework/net472) for older versions of Windows


## Credits
- [FFmpeg](https://www.ffmpeg.org/)
- [FFME](https://github.com/unosquare/ffmediaelement)
- [Youtube Explode](https://github.com/Tyrrrz/YoutubeExplode)
