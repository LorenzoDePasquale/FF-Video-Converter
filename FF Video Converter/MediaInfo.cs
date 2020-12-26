using System;
using System.Collections.Generic;
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
        public TimeSpan Duration { get; private set; }
        public long Size { get; private set; }
        public string Codec { get; private set; }
        public string ExternalAudioCodec { get; private set; }
        public double Framerate { get; private set; }
        /// <summary>
        /// Total bitrate (in bits)
        /// </summary>
        public double Bitrate { get; private set; }
        public string Source { get; private set; }
        public string ExternalAudioSource { get; private set; }
        public IReadOnlyList<AudioTrack> AudioTracks => audioTracks;
        public Resolution Resolution { get; private set; }
        public int Width => Resolution.Width;
        public int Height => Resolution.Height;
        public bool IsLocal => !Source.StartsWith("http");
        public bool HasExternalAudio => !String.IsNullOrEmpty(ExternalAudioSource);

        private AudioTrack[] audioTracks;

        private MediaInfo()
        {
        }

        public static async Task<MediaInfo> Open(string source)
        {
            MediaInfo mediaInfo = new MediaInfo();
            mediaInfo.Source = source;
            await mediaInfo.FF_Open(source).ConfigureAwait(false);
            if (!source.StartsWith("http")) mediaInfo.Title = Path.GetFileName(source);
            return mediaInfo;
        }

        public static async Task<MediaInfo> Open(string source, string externalAudioSource)
        {
            MediaInfo mediaInfo = new MediaInfo();
            mediaInfo.Source = source;
            mediaInfo.ExternalAudioSource = externalAudioSource;
            await mediaInfo.FF_Open(source).ConfigureAwait(false);
            if (!source.StartsWith("http")) mediaInfo.Title = Path.GetFileName(source);
            await mediaInfo.FF_ExternalAudioOpen(externalAudioSource).ConfigureAwait(false);
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
                        throw new Exception("Failed to parse media file:\n\n" + source); //Since it's not possible to create a Window from this thread, an Exception is thrown; whoever called this method will have to show the user the error
                    }
                    while (!BracketBalanced(stdout)) stdout += "}"; //For some reason output brakets sometimes are unbalanced, so it's necessary to manually balance them

                    using (JsonDocument jsonOutput = JsonDocument.Parse(stdout))
                    {
                        JsonElement streamsElement = jsonOutput.RootElement.GetProperty("streams");
                        JsonElement videoStreamElement = new JsonElement();
                        List<JsonElement> audioStreamElements = new List<JsonElement>();

                        //Find first video stream and all audio streams
                        for (int i = 0; i < streamsElement.GetArrayLength(); i++)
                        {
                            if (streamsElement[i].GetProperty("codec_type").GetString() == "video")
                            {
                                if (videoStreamElement.ValueKind == JsonValueKind.Undefined) videoStreamElement = streamsElement[i];
                            }
                            else if (streamsElement[i].GetProperty("codec_type").GetString() == "audio")
                            {
                                audioStreamElements.Add(streamsElement[i]);
                            }
                        }

                        //Get resolution
                        short width = 0, height = 0;
                        if (videoStreamElement.TryGetProperty("codec_name", out JsonElement e))
                            Codec = e.GetString();
                        if (videoStreamElement.TryGetProperty("width", out e))
                            width = e.GetInt16();
                        if (videoStreamElement.TryGetProperty("height", out e))
                            height = e.GetInt16();
                        if (width > 0 && height > 0)
                            Resolution = new Resolution(width, height);

                        //Get framerate
                        string fps = videoStreamElement.GetProperty("r_frame_rate").GetString();
                        if (fps != "N/A")
                        {
                            int a = Convert.ToInt32(fps.Remove(fps.IndexOf('/')));
                            int b = Convert.ToInt32(fps.Remove(0, fps.IndexOf('/') + 1));
                            Framerate = Math.Round(a / (double)b, 2);
                        }

                        //Get remaining properties
                        JsonElement formatElement = jsonOutput.RootElement.GetProperty("format");
                        double totalSeconds = Double.Parse(formatElement.GetProperty("duration").GetString(), CultureInfo.InvariantCulture);
                        Duration = TimeSpan.FromSeconds(totalSeconds);
                        Size = Int64.Parse(formatElement.GetProperty("size").GetString());
                        if (Bitrate == 0) //Sometimes the bitrate is missing from the video stream, so it's necessary to get it from here, and subtract the bitrate of all audio streams
                        {
                            Bitrate = Double.Parse(formatElement.GetProperty("bit_rate").GetString(), CultureInfo.InvariantCulture) / 1000;
                            Bitrate = Math.Round(Bitrate);
                            
                        }

                        //Get audio properties
                        audioTracks = new AudioTrack[audioStreamElements.Count];
                        for (int i = 0; i < audioStreamElements.Count; i++)
                        {
                            string codec = "";
                            int bitrate = 0, sampleRate = 0, index = 0;
                            string language = "";
                            bool defaultTrack = false;
                            if (audioStreamElements[i].TryGetProperty("codec_name", out e))
                                codec = e.GetString();
                            if (audioStreamElements[i].TryGetProperty("bit_rate", out e))
                                bitrate = Convert.ToInt32(e.GetString());
                            if (audioStreamElements[i].TryGetProperty("sample_rate", out e))
                                sampleRate = Convert.ToInt32(e.GetString());
                            index = audioStreamElements[i].GetProperty("index").GetInt32();
                            JsonElement e2 = audioStreamElements[i].GetProperty("tags");
                            if (e2.TryGetProperty("language", out e))
                                language = e.GetString();
                            e2 = audioStreamElements[i].GetProperty("disposition");
                            if (e2.TryGetProperty("default", out e))
                                defaultTrack = Convert.ToBoolean(e.GetByte());
                            audioTracks[i] = new AudioTrack(codec, bitrate, sampleRate, totalSeconds, index, language, defaultTrack);
                            Bitrate -= bitrate / 1000;
                        }

                    }
                });
            }
        }

        private async Task FF_ExternalAudioOpen(string audioSource)
        {
            StringBuilder stdoutBuilder = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "ffprobe";
                process.StartInfo.Arguments = "-i \"" + audioSource + "\" -v quiet -print_format json -show_format -show_streams -hide_banner";
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
                        throw new Exception("Failed to parse media file:\n\n" + audioSource); //Since it's not possible to create a Window from this thread, an Exception is thrown; whoever called this method will have to show the user the error
                    }
                    while (!BracketBalanced(stdout)) stdout += "}"; //For some reason output brakets sometimes are unbalanced, so it's necessary to manually balance them

                    using (JsonDocument jsonOutput = JsonDocument.Parse(stdout))
                    {
                        JsonElement streamsElement = jsonOutput.RootElement.GetProperty("streams");
                        JsonElement audioStreamElement = new JsonElement();

                        //Find first audio stream
                        for (int i = 0; i < streamsElement.GetArrayLength(); i++)
                        {
                            if (streamsElement[i].GetProperty("codec_type").GetString() == "audio")
                            {
                                audioStreamElement = streamsElement[i];
                                break;
                            }
                        }

                        //Get audio properties
                        string codec = "";
                        int bitrate = 0, sampleRate = 0, index = 0;
                        string language = "";
                        bool defaultTrack = false;
                        if (audioStreamElement.TryGetProperty("codec_name", out JsonElement e))
                            codec = e.GetString();
                        if (audioStreamElement.TryGetProperty("bit_rate", out e))
                            bitrate = Convert.ToInt32(e.GetString());
                        if (audioStreamElement.TryGetProperty("sample_rate", out e))
                            sampleRate = Convert.ToInt32(e.GetString());
                        index = audioStreamElement.GetProperty("index").GetInt32();
                        JsonElement e2 = audioStreamElement.GetProperty("tags");
                        if (e2.TryGetProperty("language", out e))
                            language = e.GetString();
                        e2 = audioStreamElement.GetProperty("disposition");
                        if (e2.TryGetProperty("default", out e))
                            defaultTrack = Convert.ToBoolean(e.GetByte());
                        ExternalAudioCodec = codec;

                        JsonElement formatElement = jsonOutput.RootElement.GetProperty("format");
                        Size += Convert.ToInt64(formatElement.GetProperty("size").GetString());
                        bitrate = Convert.ToInt32(formatElement.GetProperty("bit_rate").GetString());

                        Array.Resize(ref audioTracks, audioTracks.Length + 1);
                        audioTracks[audioTracks.Length - 1] = new AudioTrack(codec, bitrate, sampleRate, Duration.TotalSeconds, index, language, defaultTrack);
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
                process.StartInfo.Arguments = $"-read_intervals {startSeekPosition.ToString("0.00", CultureInfo.InvariantCulture)}%{endSeekPosition.ToString("0.00", CultureInfo.InvariantCulture)} -select_streams v -skip_frame nokey -show_frames -show_entries frame=pkt_pts_time -print_format csv=p=0 \"{Source}\" -hide_banner";
                process.Start();

                await Task.Run(() =>
                {
                    while ((line = process.StandardOutput.ReadLine()) != null)
                    {
                        double keyFrame = Ceiling(Double.Parse(line, CultureInfo.InvariantCulture), 2);
                        if (keyFrame < position)
                        {
                            if (keyFrame > nearestKeyFrameBefore)
                            {
                                nearestKeyFrameBefore = keyFrame;
                                foundBefore = true;
                            }
                        }
                        else if (keyFrame > position)
                        {
                            if (keyFrame < nearestKeyFrameAfter)
                            {
                                nearestKeyFrameAfter = keyFrame;
                                foundAfter = true;
                            }
                        }
                        else
                        {
                            isKeyFrame = true;
                        }
                    }
                }).ConfigureAwait(false);
            } while ((!foundBefore || !foundAfter) && distance < MAX_DISTANCE);

            process.Dispose();
            return (nearestKeyFrameBefore, nearestKeyFrameAfter, position > 0 ? isKeyFrame : true);
        }

        private double Ceiling(double value, int digits)
        {
            //With digits = 2:
            //5.123456 -> 5.13
            //5.001000 -> 5.01
            //5.110999 -> 5.12
            int x = (int)(value * Math.Pow(10, digits));
            x += 1;
            return x / Math.Pow(10, digits);
        }
    }
}