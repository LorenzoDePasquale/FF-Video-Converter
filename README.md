# FF Video Converter
Simple video converter with cutting and cropping functionality, powered by the ffmpeg project

[Youtube demo](https://youtu.be/t5fsI6cLboo)

## Features

- Encode video files in H264 and H265
- Change framerate and resolution
- Cut video, with or without re-encoding it (with possibility of cutting at keyframes)
- Crop video
- Preview encoding quality settings with before-after clips
- Open network media (direct url to media file) and convert it while it's still downloading <p align="center"><img src="https://j.gifs.com/oV9xAB.gif"></p>
- Open videos from Reddit posts at every quality
- Open Youtube videos at every audio and video quality<p align="center"><img src="https://j.gifs.com/q7XzVp.gif"></p>


## Current Issues/Limitations

- Opening a Reddit or Youtube video won't play audio in the internal player, because the original video stream has no audio; when converting, however, audio will be included
- A comprehensive error management system is missing; in particular, opening unsupported files will will crash the application
- Lots of bugs still to be fixed

## Credits
- [FFmpeg](https://www.ffmpeg.org/)
- [FFME](https://github.com/unosquare/ffmediaelement)
- [Youtube Explode](https://github.com/Tyrrrz/YoutubeExplode)
