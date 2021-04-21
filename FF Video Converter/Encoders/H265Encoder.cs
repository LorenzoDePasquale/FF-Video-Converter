using System;

namespace FFVideoConverter.Encoders
{
    public class H265Encoder : VideoEncoder
    {
        public override string Name => "H265 (x265)";
        public override bool IsDoublePassSupported => true;
        public override PixelFormat[] SupportedPixelFormats => Enum.GetValues<PixelFormat>();

        private int Crf => 21 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string hdrParameters = "";
            if (ColorInfo != null)
            {
                hdrParameters = $"hdr-opt=1:repeat-headers=1:colorprim={ColorInfo.ColorPrimaries}:transfer={ColorInfo.ColorTransfer}:colormatrix={ColorInfo.ColorSpace}:master-display={ColorInfo.DisplayMetadata}:max-cll={ColorInfo.LightLevelMetadata.maxContent},{ColorInfo.LightLevelMetadata.maxAverage}";
            }

            string command = encodingMode switch
            {
                EncodingMode.ConstantQuality => $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf} {(ColorInfo != null ? $"-x265-params {hdrParameters}" : "")}",
                EncodingMode.AverageBitrate_SinglePass => $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k {(ColorInfo != null ? $"-x265-params {hdrParameters}" : "")}",
                EncodingMode.AverageBitrate_FirstPass => $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -x265-params pass=1",
                EncodingMode.AverageBitrate_SecondPass => $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -x265-params pass=2{(ColorInfo != null ? $":{hdrParameters}" : "")}",
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