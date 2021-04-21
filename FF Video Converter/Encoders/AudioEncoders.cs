namespace FFVideoConverter.Encoders
{
    public abstract class AudioEncoder
    {
        public abstract string Name { get; }
        public Bitrate Bitrate { get; set; }

        public abstract string GetFFMpegCommand(int streamIndex);

        public override string ToString()
        {
            return Name;
        }
    }

    public class AACEncoder : AudioEncoder
    {
        public override string Name => "AAC";

        public override string GetFFMpegCommand(int streamIndex)
        {
            return $"aac -b:{streamIndex} {Bitrate.Kbps}k";
        }
    }

    public class OpusEncoder : AudioEncoder
    {
        public override string Name => "Opus";

        public override string GetFFMpegCommand(int streamIndex)
        {
            return $"libopus -b:{streamIndex} {Bitrate.Kbps}k -mapping_family 1"; //TODO: test differences between opus and libopus when opus is not experimental anymore
        }
    }
}