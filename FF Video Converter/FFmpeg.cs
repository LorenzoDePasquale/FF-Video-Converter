using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;


namespace FFVideoConverter
{
    public class FFmpegEngine
    {
        public delegate void ConversionEventHandler(ProgressData progressData);
        public event ConversionEventHandler ProgressChanged;
        public event ConversionEventHandler ConversionCompleted;

        private readonly Process convertProcess = new Process();
        private ProgressData progressData;
        private int i = 0;

        public FFmpegEngine()
        {
            convertProcess.StartInfo.FileName = "ffmpeg";
            convertProcess.StartInfo.RedirectStandardError = true;
            convertProcess.StartInfo.UseShellExecute = false;
            convertProcess.StartInfo.CreateNoWindow = true;
            convertProcess.ErrorDataReceived += ConvertProcess_ErrorDataReceived;
        }

        public async void Convert(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions)
        {
            progressData = new ProgressData();

            //Duration
            if (conversionOptions.End != TimeSpan.Zero)
            {
                progressData.TotalTime = conversionOptions.End - conversionOptions.Start;
            }
            else
            {
                progressData.TotalTime = sourceInfo.Duration - conversionOptions.Start;
            }

            //Filters
            string filters = "";
            if (conversionOptions.Resolution.HasValue() || conversionOptions.CropData.HasValue() || conversionOptions.Rotation.HasValue())
            {
                filters = " -vf " + ConcatFilters(conversionOptions.Resolution.FilterString, conversionOptions.CropData.FilterString, conversionOptions.Rotation.FilterString);
            }

            //FFMpeg command string
            StringBuilder sb = new StringBuilder("-y");
            sb.Append($" -ss {conversionOptions.Start}");
            //sb.Append($" -hwaccel dxva2 -i \"{sourceInfo.Source}\"");
            sb.Append($" -i \"{sourceInfo.Source}\"");
            if (!String.IsNullOrEmpty(sourceInfo.AudioSource))
            {
                sb.Append($" -ss {conversionOptions.Start}");
                sb.Append($" -i \"{sourceInfo.AudioSource}\"");
            }
            if (conversionOptions.End != TimeSpan.Zero) sb.Append($" -t {conversionOptions.End - conversionOptions.Start}");
            sb.Append(" -c:v " + conversionOptions.Encoder.GetFFMpegCommand());
            if (conversionOptions.Framerate > 0) sb.Append(" -r " + conversionOptions.Framerate);
            sb.Append(filters);
            sb.Append(conversionOptions.SkipAudio ? " -an" : " -c:a copy");
            sb.Append($" -avoid_negative_ts 1 \"{destination}\" -hide_banner");

            convertProcess.StartInfo.Arguments = sb.ToString();
            convertProcess.Start();
            convertProcess.BeginErrorReadLine();
            convertProcess.PriorityClass = ProcessPriorityClass.BelowNormal;

            await Task.Run(() =>
            {
                convertProcess.WaitForExit();
            });
            convertProcess.CancelErrorRead();

            int exitCode = convertProcess.ExitCode;
            if (exitCode == 0) //conversion not aborted
            {
                ConversionCompleted?.Invoke(progressData);
            }
        }

        private string ConcatFilters(params string[] filters)
        {
            if (filters.Length == 0) return "";

            StringBuilder sb = new StringBuilder();

            sb.Append('"');
            foreach (var filter in filters)
            {
                if (filter.Length != 0)
                {
                    sb.Append(filter);
                    sb.Append(',');
                }    
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append('"');
            return sb.ToString();
        }

        private void ConvertProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            string line = e.Data;

            if (line != null)
            {
                if (line.StartsWith("frame")) //frame=   47 fps=0.0 q=0.0 size=       0kB time=00:00:00.00 bitrate=N/A speed=   0x    
                {
                    progressData.CurrentFrame = System.Convert.ToUInt32(line.Remove(line.IndexOf(" fps")).Remove(0, 6));
                    progressData.EncodingSpeedFps = System.Convert.ToInt16(line.Remove(line.IndexOf(" q")).Remove(0, line.IndexOf("fps") + 4).Replace(".", ""));
                    progressData.CurrentByteSize = System.Convert.ToInt32(line.Remove(line.IndexOf(" time") - 2).Remove(0, line.IndexOf("size") + 5)) * 1000;
                    progressData.CurrentTime = TimeSpan.Parse(line.Remove(line.IndexOf(" bit")).Remove(0, line.IndexOf("time") + 5));
                    float currentBitrate = line.Contains("bitrate=N/A") ? 0 : System.Convert.ToSingle(line.Remove(line.IndexOf("kbits")).Remove(0, line.IndexOf("bitrate") + 8), CultureInfo.InvariantCulture);
                    if (progressData.CurrentTime.Seconds > 5) //Skips first 5 seconds to give the encoder time to adjust it's bitrate
                    {
                        progressData.AverageBitrate += (currentBitrate - progressData.AverageBitrate) / ++i;
                    }

                    if (line.EndsWith("N/A    ") || line.EndsWith("x")) progressData.EncodingSpeed = 0;
                    else progressData.EncodingSpeed = System.Convert.ToSingle(line.Remove(line.IndexOf('x')).Remove(0, line.IndexOf("speed") + 6), CultureInfo.InvariantCulture);

                    ProgressChanged?.Invoke(progressData);
                }
                else if (line.StartsWith("Error"))
                {
                    //TODO: raise exception
                }
            }
        }

        public void PauseConversion()
        {
            convertProcess.Suspend();
        }

        public void ResumeConversion()
        {
            convertProcess.Resume();
        }

        public void StopConversion()
        {
            try
            {
                if (convertProcess != null && !convertProcess.HasExited)
                {
                    convertProcess.Kill(); 
                    convertProcess.CancelErrorRead();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}