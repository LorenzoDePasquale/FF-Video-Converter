using System;

namespace FFVideoConverter.Encoders
{
    public class AV1Encoder : VideoEncoder
    {
        public override string Name => "AV1 (aom)";
        public override bool IsDoublePassSupported => true;
        public override PixelFormat[] SupportedPixelFormats => Enum.GetValues<PixelFormat>();

        private int Crf => 28 + (int)Quality * 5;
        private int CPU_Used => 3 + (int)Preset;

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            //string hdrParameters = "";

            //if (ColorInfo != null)
            //{
                //hdrParameters = $"colorprim={ColorInfo.ColorPrimaries}:transfer={ColorInfo.ColorTransfer}:colormatrix={ColorInfo.ColorSpace}:master-display={ColorInfo.DisplayMetadata}:max-cll={ColorInfo.LightLevelMetadata.maxContent},{ColorInfo.LightLevelMetadata.maxAverage}";
            //}

            //TODO: add a method that finds the ideal tiles settings
            //TODO: add hdr support
            string command = encodingMode switch
            {
                EncodingMode.ConstantQuality => $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -crf {Crf} -b:v 0",
                EncodingMode.AverageBitrate_SinglePass => $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -b:v {Bitrate.Kbps}k",
                EncodingMode.AverageBitrate_FirstPass => $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -b:v {Bitrate.Kbps}k -pass 1",
                EncodingMode.AverageBitrate_SecondPass => $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -b:v {Bitrate.Kbps}k -pass 2",
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