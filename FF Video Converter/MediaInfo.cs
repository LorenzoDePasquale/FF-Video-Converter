using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
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
        public string AudioCodec { get; set; }
        public double Framerate { get; set; }
        public double Bitrate { get; set; }
        public string Source { get; set; }
        public string AudioSource { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string AspectRatio { get; set; }
        public bool IsLocal { get { return !Source.StartsWith("http"); } }


        private MediaInfo()
        {
        }

        public static async Task<MediaInfo> Open(string source)
        {
            MediaInfo mediaInfo = new MediaInfo();
            mediaInfo.Source = source;
            await mediaInfo.FF_Open(source);
            if (!source.StartsWith("http")) mediaInfo.Title = Path.GetFileName(source);
            return mediaInfo;
        }

        private async Task FF_Open(string source)
        {
            StringBuilder stdoutBuilder = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "ffprobe";
                process.StartInfo.Arguments = "-i \"" + source + "\" -v quiet -print_format json -show_format -show_streams -hide_banner";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                await Task.Run(() =>
                {
                    while (process.StandardOutput.Peek() > -1)
                    {
                        string line = process.StandardOutput.ReadLine();
                        stdoutBuilder.Append(line);
                        if (line.Contains("probe_score")) //For some reason ffprobe sometimes hangs for 30-60 seconds before closing, so it's necessary to manually stop at the end of the output
                        {
                            break;
                        }
                    }

                    stdoutBuilder.Remove(stdoutBuilder.Length - 1, 1);
                    string stdout = stdoutBuilder.ToString();
                    if (stdout.Length < 5)
                    {
                        new MessageBoxWindow("The selected media could not per parsed:\n\n" + source, "Error opening media");
                        return;
                    }
                    while (!BracketBalanced(stdout)) stdout += "}";

                    using (JsonDocument jsonOutput = JsonDocument.Parse(stdout))
                    {
                        JsonElement streamsElement = jsonOutput.RootElement.GetProperty("streams");
                        JsonElement videoStreamElement = streamsElement[0];
                        JsonElement audioStreamElement = new JsonElement();
                        for (int i = 0; i < streamsElement.GetArrayLength(); i++)
                        {
                            if (streamsElement[i].GetProperty("codec_type").GetString() == "video")
                            {
                                videoStreamElement = streamsElement[i];
                                break;
                            }
                        }
                        for (int i = 0; i < streamsElement.GetArrayLength(); i++)
                        {
                            if (streamsElement[i].GetProperty("codec_type").GetString() == "audio")
                            {
                                audioStreamElement = streamsElement[i];
                                break;
                            }
                        }
                        if (videoStreamElement.TryGetProperty("codec_name", out JsonElement e))
                            Codec = e.GetString();
                        if (videoStreamElement.TryGetProperty("width", out e))
                            Width = e.GetInt32();
                        if (videoStreamElement.TryGetProperty("height", out e))
                            Height = e.GetInt32();
                        if (videoStreamElement.TryGetProperty("display_aspect_ratio", out JsonElement _))
                            AspectRatio = videoStreamElement.GetProperty("display_aspect_ratio").GetString();
                        string fps = videoStreamElement.GetProperty("r_frame_rate").GetString();
                        if (fps != "N/A")
                        {
                            int a = Convert.ToInt32(fps.Remove(fps.IndexOf('/')));
                            int b = Convert.ToInt32(fps.Remove(0, fps.IndexOf('/') + 1));
                            Framerate = Math.Round(a / (double)b, 2);
                        }
                        if (audioStreamElement.ValueKind != JsonValueKind.Undefined && audioStreamElement.TryGetProperty("codec_name", out e))
                            AudioCodec = e.GetString();
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

        private bool BracketBalanced(string text)
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
            bool isKeyFrame = position == 0;
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