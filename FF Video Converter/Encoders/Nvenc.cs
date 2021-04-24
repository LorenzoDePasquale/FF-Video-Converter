namespace FFVideoConverter.Encoders
{
    public class H264Nvenc : VideoEncoder
    {
        public override string Name => "H264 (Nvenc)";
        public override bool IsDoublePassSupported => false;
        public override PixelFormat[] SupportedPixelFormats => new[] { PixelFormat.yuv420p, PixelFormat.yuv444p, PixelFormat.yuv420p10le }; // No copy because nvenc is more restrictive on the allowed pixel formats, so it's necessary to force a choice 

        private int Cq => 20 + (int)Quality * 3;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string command = encodingMode switch
            {
                EncodingMode.ConstantQuality => $"h264_nvenc -profile high -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq - 10} -qmax {Cq + 10} -bf 3 -b_ref_mode middle -spatial_aq 1 -aq-strength 8 -temporal_aq 1",
                EncodingMode.AverageBitrate_SinglePass => $"h264_nvenc -profile high -preset medium -rc vbr_hq -rc-lookahead 32 -b:v {Bitrate.Kbps}k -bf 3 -b_ref_mode middle -spatial_aq 1 -aq-strength 8 -temporal_aq 1",
                _ => "",
            };

            // For Nvenc it's necessary to use different 10bit pixel formats:
            if (PixelFormat == PixelFormat.yuv420p10le)
            {
                command += " -pix_fmt p010le"; // yuv420p10le -> p010le
            }
            else
            {
                command += $" -pix_fmt {PixelFormat}";
            }

            return command;
        }
    }


    public class H265Nvenc : VideoEncoder
    {

        public override string Name => "H265 (Nvenc)";
        public override bool IsDoublePassSupported => false;
        public override PixelFormat[] SupportedPixelFormats => new[] { PixelFormat.yuv420p, PixelFormat.yuv444p, PixelFormat.yuv420p10le }; // No copy because nvenc is more restrictive on the allowed pixel formats, so it's necessary to force a choice 

        private int Cq => 21 + (int)Quality * 3;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string command = encodingMode switch
            {
                EncodingMode.ConstantQuality => $"hevc_nvenc -profile rext -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq - 10} -qmax {Cq + 10} -bf 3 -b_ref_mode middle -spatial_aq 1 -aq-strength 8 -temporal_aq 1 -nonref_p 1",
                EncodingMode.AverageBitrate_SinglePass => $"hevc_nvenc -profile rext -preset medium -rc vbr_hq -rc-lookahead 32 -b:v {Bitrate.Kbps}k -bf 3 -b_ref_mode middle -spatial_aq 1 -aq-strength 8 -temporal_aq 1 -nonref_p 1",
                _ => "",
            };

            // For Nvenc it's necessary to use different 10bit pixel formats:
            if (PixelFormat == PixelFormat.yuv420p10le)
            {
                command += " -pix_fmt p010le"; // yuv420p10le -> p010le
            }
            else
            {
                command += $" -pix_fmt {PixelFormat}";
            }

            return command;
        }
    }
}