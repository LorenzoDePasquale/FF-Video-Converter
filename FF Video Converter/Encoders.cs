using System.Management;

namespace FFVideoConverter
{
    public enum Quality { Best, VeryGood, Good, Medium, Low, VeryLow }
    public enum Preset { Slower, Slow, Medium, Fast, Faster, VeryFast }


    public abstract class Encoder
    {
        public Quality Quality { get; set; }
        public Preset Preset { get; set; }
        public abstract string Name { get; }

        public abstract string GetFFMpegCommand();

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

        public override string GetFFMpegCommand()
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

        public override string GetFFMpegCommand()
        {
            return $"libx264 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf}";
        }
    }

    public class H265Encoder : Encoder
    {
        public override string Name => "H265 (x265)";
        private int Crf => 21 + (int)Quality * 4;

        public H265Encoder()
        {
        }

        public override string GetFFMpegCommand()
        {
            return $"libx265 -preset {Preset.GetName().Replace(" ", "").ToLower()} -crf {Crf}";
        }
    }

    public class H264Nvenc : Encoder
    {
        private int Cq => 25 + (int)Quality * 3;

        public override string Name => "H264 (Nvenc)";

        public H264Nvenc()
        {
        }

        public override string GetFFMpegCommand()
        {
            return $"h264_nvenc -profile high -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq} -qmax {Cq} -bf 3 -b_ref_mode middle";
        }

        public static bool IsAvaiable()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["CurrentBitsPerPixel"] != null && obj["CurrentHorizontalResolution"] != null)
                {
                    string graphicsCard = obj["Name"].ToString();
                    if (graphicsCard.ToLower().Contains("nvidia geforce rtx"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public class H265Nvenc : Encoder
    {
        private int Cq => 26 + (int)Quality * 3;

        public override string Name => "H265 (Nvenc)";

        public H265Nvenc()
        {
        }

        public override string GetFFMpegCommand()
        {
            return $"hevc_nvenc -profile rext -preset medium -rc vbr_hq -rc-lookahead 32 -cq {Cq} -qmin {Cq} -qmax {Cq} -bf 3 -b_ref_mode middle";
        }

        public static bool IsAvaiable()
        {
            return H264Nvenc.IsAvaiable();
        }
    }

    public class H264QuickSync : Encoder
    {
        private int GlobalQuality => 26 + (int)Quality * 4;

        public override string Name => "H264 (QuickSync)";

        public H264QuickSync()
        {
        }

        public override string GetFFMpegCommand()
        {
            return $"h264_qsv -profile high -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1";
        }

        public static bool IsAvaiable()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["Name"].ToString().ToLower().Contains("intel"))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class H265QuickSync : Encoder
    {
        private int GlobalQuality => 26 + (int)Quality * 4;

        public override string Name => "H265 (QuickSync)";

        public H265QuickSync()
        {
        }

        public override string GetFFMpegCommand()
        {
            return $"hevc_qsv -profile main -preset {Preset.GetName().Replace(" ", "").ToLower()} -global_quality {GlobalQuality} -look_ahead 1";
        }

        public static bool IsAvaiable()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["Name"].ToString().ToLower().Contains("intel"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}