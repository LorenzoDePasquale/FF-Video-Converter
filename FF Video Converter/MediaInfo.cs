using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;


namespace FFVideoConverter
{
    public class MediaInfo
    {
        public string Title { get; set; }
        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
        public string Codec { get; set; }
        public double Framerate { get; set; }
        public double Bitrate { get; set; }
        public string Source { get; set; }
        public string AudioSource { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string AspectRatio { get; set; }
        public bool IsLocal { get { return !Source.StartsWith("http"); } }


        public MediaInfo()
        {
        }

        public static async Task<MediaInfo> Open(string source)
        {
            MediaInfo mediaInfo = new MediaInfo();
            mediaInfo.Source = source;
            await mediaInfo.FF_Open(source);
            return mediaInfo;
        }

        private async Task FF_Open(string source)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "ffprobe";
                process.StartInfo.Arguments = "-i \"" + source + "\" -print_format json -show_format -show_streams -hide_banner";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                await Task.Run(() =>
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    if (!BracketBalanced(stdout)) stdout += "}"; //Because fucking ffprobe sometimes fucking forgets the last fucking bracket ffs

                    using (JsonDocument jsonOutput = JsonDocument.Parse(stdout))
                    {
                        JsonElement streamElement = jsonOutput.RootElement.GetProperty("streams")[0];
                        Codec = streamElement.GetProperty("codec_name").GetString();
                        Width = streamElement.GetProperty("width").GetInt32();
                        Height = streamElement.GetProperty("height").GetInt32();
                        AspectRatio = streamElement.GetProperty("display_aspect_ratio").GetString();
                        string fps = streamElement.GetProperty("r_frame_rate").GetString();
                        if (fps != "N/A")
                        {
                            int a = Convert.ToInt32(fps.Remove(fps.IndexOf('/')));
                            int b = Convert.ToInt32(fps.Remove(0, fps.IndexOf('/') + 1));
                            Framerate = Math.Round(a / (double)b, 2);
                        }
                        JsonElement formatElement = jsonOutput.RootElement.GetProperty("format");
                        double totalSeconds = Double.Parse(formatElement.GetProperty("duration").GetString(), CultureInfo.InvariantCulture);
                        Duration = TimeSpan.FromSeconds(totalSeconds);
                        Size = Int64.Parse(formatElement.GetProperty("size").GetString());
                        Bitrate = Double.Parse(formatElement.GetProperty("bit_rate").GetString(), CultureInfo.InvariantCulture) / 1000;
                        Bitrate = Math.Round(Bitrate);
                    }
                });
            }
        }

        private bool BracketBalanced(in string text)
        {
            int brackets = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{') brackets++;
                else if (text[i] == '}') brackets--;
            }
            return brackets == 0;
        }

        public async Task<(double before, double after, bool isKeyFrame)> GetNearestBeforeAndAfterKeyFrames(double position)
        {
            Process process = new Process();
            string line;
            double nearestKeyFrameBefore = 0;
            double nearestKeyFrameAfter = Duration.TotalSeconds;
            bool isKeyFrame = false;
            const double MAX_DISTANCE = 30;
            double distance = 1;
            double increment = 1;
            double startSeekPosition, endSeekPosition;
            bool foundBefore = position == 0;
            bool foundAfter = false;

            position = Math.Round(position, 2);
            process.StartInfo.FileName = "ffprobe";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;

            do
            {
                distance += increment++;
                startSeekPosition = position > distance ? position - distance : 0;
                endSeekPosition = position + distance;

                process.StartInfo.Arguments = $"-read_intervals {startSeekPosition.ToString("0.00", CultureInfo.InvariantCulture)}%{endSeekPosition.ToString("0.00", CultureInfo.InvariantCulture)}" +
                                              $" -select_streams v -skip_frame nokey -show_frames -show_entries frame=pkt_pts_time \"{Source}\" -hide_banner";
                process.Start();
                await Task.Run(() =>
                {
                    while ((line = process.StandardOutput.ReadLine()) != null)
                    {
                        if (line.StartsWith("pkt")) //pkt_pts_time=0.000000
                        {
                            double keyFrame = Math.Round(Double.Parse(line.Remove(0, 13), CultureInfo.InvariantCulture), 2);
                            if (keyFrame < position)
                            {
                                nearestKeyFrameBefore = keyFrame;
                                foundBefore = true;
                            }
                            else if (keyFrame > position)
                            {
                                nearestKeyFrameAfter = keyFrame;
                                foundAfter = true;
                                return;
                            }
                            else
                            {
                                isKeyFrame = true;
                            }
                        }
                    }
                }).ConfigureAwait(false);
            } while ((!foundBefore || !foundAfter) && distance < MAX_DISTANCE);

            process.Dispose();
            return (nearestKeyFrameBefore, nearestKeyFrameAfter, isKeyFrame);
        }
    }
}