namespace FFVideoConverter.Encoders
{
    public class H264QuickSync : VideoEncoder
    {
        public override string Name => "H264 (QuickSync)";
        public override bool IsDoublePassSupported => false;
        public override PixelFormat[] SupportedPixelFormats => new[] { PixelFormat.yuv420p, PixelFormat.yuv420p10le }; //No copy because qsv is more restrictive on the allowed pixel formats, so it's necessary to force a choice

        private int GlobalQuality => 22 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string command = encodingMode switch
            {
                EncodingMode.ConstantQuality => $"h264_qsv -profile high -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1",
                EncodingMode.AverageBitrate_SinglePass => $"h264_qsv -profile high -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -look_ahead 1",
                _ => "",
            };

            if (PixelFormat == PixelFormat.yuv420p)
            {
                command += " -pix_fmt nv12"; // yuv420p -> nv12
            }
            else if (PixelFormat == PixelFormat.yuv420p10le)
            {
                command += " -pix_fmt p010le"; // yuv420p10le -> p010le
            }

            return command;
        }
    }


    public class H265QuickSync : VideoEncoder
    {
        public override string Name => "H265 (QuickSync)";
        public override bool IsDoublePassSupported => false;
        public override PixelFormat[] SupportedPixelFormats => new[] { PixelFormat.yuv420p, PixelFormat.yuv420p10le }; //No copy because qsv is more restrictive on the allowed pixel formats, so it's necessary to force a choice

        private int GlobalQuality => 22 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string command = encodingMode switch
            {
                EncodingMode.ConstantQuality => $"hevc_qsv -profile main -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1",
                EncodingMode.AverageBitrate_SinglePass => $"hevc_qsv -profile main -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -look_ahead 1",
                _ => "",
            };

            if (PixelFormat == PixelFormat.yuv420p)
            {
                command += " -pix_fmt nv12"; // yuv420p -> nv12
            }
            else if (PixelFormat == PixelFormat.yuv420p10le)
            {
                command += " -pix_fmt p010le"; // yuv420p10le -> p010le
            }

            return command;
        }
    }
}