# FF Video Converter
Simple video converter with cutting and cropping functionality, powered by the ffmpeg project

[Youtube demo](https://youtu.be/F1RwbC_K_4o)

## Features

- Encode video files in H264 and H265 and export them in mp4 or mkv format <p align="center"><img width="700" src="https://i.imgur.com/SnKBqsh.png"></p>
- Experimental support for Nvidia H264 and H265 hardware accelerated encoders (only on RTX video cards, because  of lack of B-frame support on older gpus) 
- Experimental support for Intel H264 and H265 hardware accelerated encoders; on computers with a dedicated GPU, Intel iGPU must be manually enabled for QuickSync to work
- Change framerate and resolution
- Trim or cut the video, with or without re-encoding it (with precise cutting at keyframe position) <p align="center"><img width="700" src="https://i.imgur.com/YvKT9Mb.png"></p>
- Visually crop and rotate the video <p align="center"><img width="700" src="https://i.imgur.com/sb7YXSW.png"></p>
- Preview encoding quality settings with before-after clips
- Change default audio track, change volume per track, or remove them altogether <p align="center"><img width="700" src="https://i.imgur.com/uMWnNxM.png"></p>
- Open network media and convert it while it's still downloading <p align="center"><img width="700" src="https://i.imgur.com/71B5ixJ.gif"></p>
- Open videos from Youtube, Reddit, Twitter, Facebook and Instagram<p align="center"><img width="700" src="https://i.imgur.com/VuYrnTr.gif"></p>
- Queue system


## Credits
- [FFmpeg](https://www.ffmpeg.org/)
- [FFME](https://github.com/unosquare/ffmediaelement)
- [Youtube Explode](https://github.com/Tyrrrz/YoutubeExplode)
