using System;

namespace FFVideoConverter.Encoders
{
    public class H264Encoder : VideoEncoder
    {
        public override string Name => "H264 (x264)";
        public override bool IsDoublePassSupported => true;
        public override PixelFormat[] SupportedPixelFormats => Enum.GetValues<PixelFormat>();

        private int Crf => 18 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string command = encodingMode switch
            {
                EncodingMode.ConstantQuality => $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf}",
                EncodingMode.AverageBitrate_SinglePass => $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k",
                EncodingMode.AverageBitrate_FirstPass => $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -pass 1",
                EncodingMode.AverageBitrate_SecondPass => $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -pass 2",
                _ => "",
            };

            if (PixelFormat != PixelFormat.copy)
            {
                command += $" -pix_fmt {PixelFormat}";
            }

            return command;
        }
    }
}