using System.Collections.Generic;
using System.Management;


namespace FFVideoConverter
{
    public enum Quality { Best, VeryGood, Good, Medium, Low, VeryLow }
    public enum Preset { Slower, Slow, Medium, Fast, Faster, VeryFast }
    public enum EncodingMode { ConstantQuality, AverageBitrate_SinglePass, AverageBitrate_FirstPass, AverageBitrate_SecondPass, NoEncoding }


    public static class VideoAdapters
    {
        public static List<string> ADAPTERS = new List<string>();

        static VideoAdapters()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                ADAPTERS.Add(obj["Name"].ToString().ToLower());
            }
        }

        public static bool Contains(string name)
        {
            foreach (var item in ADAPTERS)
            {
                if (item.Contains(name))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public abstract class Encoder
    {
        public Quality Quality { get; set; }
        public Preset Preset { get; set; }
        public abstract string Name { get; }
        /// <summary>
        /// Target bitrate (in kbps) for 2-pass encoding
        /// </summary>
        public int Bitrate { get; set; }

        public abstract string GetFFMpegCommand(EncodingMode encodingMode);

        public override string ToString()
        {
            return Name;
        }
    }

    public class NativeEncoder : Encoder
    {
        public override string Name => "Native";

        public NativeEncoder()
        {
        }

        public override string GetFFMpegCommand(EncodingMode commandTipe)
        {
            return " copy";
        }
    }

    public class H264Encoder : Encoder
    {
        public override string Name => "H264 (x264)";
        private int Crf => 18 + (int)Quality * 4;

        public H264Encoder()
        {
        }

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf}";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k";
                case EncodingMode.AverageBitrate_FirstPass:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k -pass 1";
                case EncodingMode.AverageBitrate_SecondPass:
                    return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k -pass 2";
                default:
                    return "";
            }
        }
    }

    public class H265Encoder : Encoder
    {
        public override string Name => "H265 (x265)";
        private int Crf => 21 + (int)Quality * 4;

        public H265Encoder()
        {
        }

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf}";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k";
                case EncodingMode.AverageBitrate_FirstPass:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k -x265-params pass=1";
                case EncodingMode.AverageBitrate_SecondPass:
                    return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k -x265-params pass=2";
                default:
                    return "";
            }
        }
    }

    public class H264Nvenc : Encoder
    {
        private int Cq => 20 + (int)Quality * 3;

        public override string Name => "H264 (Nvenc)";

        public H264Nvenc()
        {
        }

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"h264_nvenc -profile high -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq} -qmax {Cq} -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"h264_nvenc -profile high -preset medium -rc vbr_hq -rc-lookahead 32 -b:v {Bitrate}k -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }

    public class H265Nvenc : Encoder
    {
        private int Cq => 21 + (int)Quality * 3;

        public override string Name => "H265 (Nvenc)";

        public H265Nvenc()
        {
        }

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"hevc_nvenc -profile rext -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq} -qmax {Cq} -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"hevc_nvenc -profile rext -preset medium -rc vbr_hq -rc-lookahead 32 -b:v {Bitrate}k -bf 3 -b_ref_mode middle -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }

    public class H264QuickSync : Encoder
    {
        private int GlobalQuality => 22 + (int)Quality * 4;

        public override string Name => "H264 (QuickSync)";

        public H264QuickSync()
        {
        }

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"h264_qsv -profile high -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1 -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"h264_qsv -profile high -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k -look_ahead 1 -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }

    public class H265QuickSync : Encoder
    {
        private int GlobalQuality => 22 + (int)Quality * 4;

        public override string Name => "H265 (QuickSync)";

        public H265QuickSync()
        {
        }

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            switch (encodingMode)
            {
                case EncodingMode.ConstantQuality:
                    return $"hevc_qsv -profile main -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1 -pix_fmt yuv420p";
                case EncodingMode.AverageBitrate_SinglePass:
                    return $"hevc_qsv -profile main -preset {Preset.GetName().Replace(" ", "").ToLower()} -b:v {Bitrate}k -look_ahead 1 -pix_fmt yuv420p";
                default:
                    return "";
            }
        }
    }
}