namespace FFVideoConverter
{
    public enum Quality 
    { 
        Best, VeryGood, Good, Medium, Low, VeryLow 
    }
    public enum Preset 
    { 
        Slower, Slow, Medium, Fast, Faster, VeryFast
    }
    public enum EncodingMode 
    { 
        ConstantQuality, AverageBitrate_SinglePass, AverageBitrate_FirstPass, AverageBitrate_SecondPass, Copy, NoEncoding
    }

    public abstract class VideoEncoder
    {
        public abstract string Name { get; }
        public abstract bool IsDoublePassSupported { get; }
        public Quality Quality { get; set; }
        public Preset Preset { get; set; }
        public Bitrate Bitrate { get; set; }
        public ColorInfo ColorInfo { get; set; }

        public abstract string GetFFMpegCommand(EncodingMode encodingMode);

        public override string ToString()
        {
            return Name;
        }
    }

    public class CopyEncoder : VideoEncoder
    {
        public override string Name => "Copy";
        public override bool IsDoublePassSupported => false;

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            return "copy";
        }
    }

    public class H264Encoder : VideoEncoder
    {
        public override string Name => "H264 (x264)";
        public override bool IsDoublePassSupported => true;

        private int Crf => 18 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf}";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k";
                case EncodingMode.AverageBitrate_FirstPass:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -pass 1";
                case EncodingMode.AverageBitrate_SecondPass:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -pass 2";
                default:
                    return "";
            }
        }
    }

    public class H265Encoder : VideoEncoder
    {
        public override string Name => "H265 (x265)";
        public override bool IsDoublePassSupported => true;

        private int Crf => 21 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string hdrParameters = "";
            if (ColorInfo != null)
            {
                hdrParameters = $"hdr-opt=1:repeat-headers=1:colorprim={ColorInfo.ColorPrimaries}:transfer={ColorInfo.ColorTransfer}:colormatrix={ColorInfo.ColorSpace}:master-display={ColorInfo.DisplayMetadata}:max-cll={ColorInfo.LightLevelMetadata.maxContent},{ColorInfo.LightLevelMetadata.maxAverage}";
            }

            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf} {(ColorInfo != null ? $"-x265-params {hdrParameters}" : "")}";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k {(ColorInfo != null ? $"-x265-params {hdrParameters}" : "")}";
                case EncodingMode.AverageBitrate_FirstPass:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -x265-params pass=1";
                case EncodingMode.AverageBitrate_SecondPass:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -x265-params pass=2{(ColorInfo != null ? $":{hdrParameters}" : "")}";
                default:
                    return "";
            }
        }
    }

    public class H264Nvenc : VideoEncoder
    {
        public override string Name => "H264 (Nvenc)";
        public override bool IsDoublePassSupported => false;

        private int Cq => 20 + (int)Quality * 3;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"h264_nvenc -profile high -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq} -qmax {Cq} -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"h264_nvenc -profile high -preset medium -rc vbr_hq -rc-lookahead 32 -b:v {Bitrate.Kbps}k -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }

    public class H265Nvenc : VideoEncoder
    {
        public override string Name => "H265 (Nvenc)";
        public override bool IsDoublePassSupported => false;

        private int Cq => 21 + (int)Quality * 3;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"hevc_nvenc -profile rext -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq} -qmax {Cq} -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"hevc_nvenc -profile rext -preset medium -rc vbr_hq -rc-lookahead 32 -b:v {Bitrate.Kbps}k -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }

    public class H264QuickSync : VideoEncoder
    {
        public override string Name => "H264 (QuickSync)";
        public override bool IsDoublePassSupported => false;

        private int GlobalQuality => 22 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"h264_qsv -profile high -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1 -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"h264_qsv -profile high -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -look_ahead 1 -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }

    public class H265QuickSync : VideoEncoder
    {
        public override string Name => "H265 (QuickSync)";
        public override bool IsDoublePassSupported => false;

        private int GlobalQuality => 22 + (int)Quality * 4;


        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"hevc_qsv -profile main -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1 -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"hevc_qsv -profile main -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate.Kbps}k -look_ahead 1 -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }

    public class AV1Encoder : VideoEncoder
    {
        public override string Name => "AV1 (aom)";
        public override bool IsDoublePassSupported => true;

        private int Crf => 28 + (int)Quality * 5;
        private int CPU_Used => 3 + (int)Preset;

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            string hdrParameters = "";

            if (ColorInfo != null)
            {
                hdrParameters = $"colorprim={ColorInfo.ColorPrimaries}:transfer={ColorInfo.ColorTransfer}:colormatrix={ColorInfo.ColorSpace}:master-display={ColorInfo.DisplayMetadata}:max-cll={ColorInfo.LightLevelMetadata.maxContent},{ColorInfo.LightLevelMetadata.maxAverage}";
            }

            //TODO: add a method that finds the ideal tiles settings
            //TODO: add hdr support
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -crf {Crf} -b:v 0";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -b:v {Bitrate.Kbps}k";
                case EncodingMode.AverageBitrate_FirstPass:
                    return $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -b:v {Bitrate.Kbps}k -pass 1";
                case EncodingMode.AverageBitrate_SecondPass:
                    return $"libaom-av1 -cpu-used {CPU_Used} -row-mt 1 -tiles 2x2 -b:v {Bitrate.Kbps}k -pass 2";
                default:
                    return "";
            }
        }
    }
}