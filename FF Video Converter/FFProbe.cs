using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;


namespace FFVideoConverter
{
    static class FFProbe
    {
        static ProcessStartInfo psi = new ProcessStartInfo();

        static FFProbe()
        {
            psi.FileName = "ffprobe.exe";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
        }

        public static async Task<string> GetMediaInfo(string source)
        {
            string jsonOutput;

            using Process process = new Process();
            process.StartInfo = psi;
            process.StartInfo.Arguments = "-i \"" + source + "\" -v quiet -print_format json -show_format -show_streams -hide_banner";
            process.Start();

            jsonOutput = await Task.Run(() =>
            {
                StringBuilder stdoutBuilder = new StringBuilder();

                while (process.StandardOutput.Peek() > -1)
                {
                    string line = process.StandardOutput.ReadLine();
                    if (line.Contains("probe_score")) // For some reason ffprobe sometimes hangs for 30-60 seconds before closing, so it's necessary to manually stop at the end of the output
                        {
                        break;
                    }
                    stdoutBuilder.Append(line);
                }
                stdoutBuilder.Remove(stdoutBuilder.Length - 1, 1); // Removes last comma
                return stdoutBuilder.ToString();
            }).ConfigureAwait(false);

            if (jsonOutput.Length < 5)
            {
                // Since it's not possible to create a Window from this thread, an Exception is thrown; whoever called this method will have to show the user the error
                throw new Exception("Failed to parse media file:\n\n" + source); 
            }

            // For some reason output brakets sometimes are unbalanced, so it's necessary to manually balance them
            while (!BracketBalanced(jsonOutput)) jsonOutput += "}";

            return jsonOutput;
        }

        public static async Task<string> GetColorInfo(string source)
        {
            using Process process = new Process();
            process.StartInfo = psi;
            process.StartInfo.Arguments = "-i \"" + source + "\" -v quiet -print_format json -select_streams v -show_frames -read_intervals \"%+#1\" -show_entries \"frame=color_space,color_primaries,color_transfer,side_data_list,pix_fmt\" -hide_banner";
            process.Start();

            return await Task.Run(() =>
            {
                StringBuilder stdoutBuilder = new StringBuilder();

                while (process.StandardOutput.Peek() > -1)
                {
                    string line = process.StandardOutput.ReadLine();
                    stdoutBuilder.Append(line);
                    if (line.Contains("probe_score")) // For some reason ffprobe sometimes hangs for 30-60 seconds before closing, so it's necessary to manually stop at the end of the output
                    {
                        break;
                    }
                }

                return stdoutBuilder.ToString();
            }).ConfigureAwait(false);
        }

        public static async Task<(double before, double after, bool isKeyFrame)> GetNearestBeforeAndAfterKeyFrames(MediaInfo mediaInfo, double position)
        {
            using Process process = new Process();
            string line;
            double nearestKeyFrameBefore = 0;
            double nearestKeyFrameAfter = mediaInfo.Duration.TotalSeconds;
            bool isKeyFrame = position == 0;
            const double MAX_DISTANCE = 30;
            double distance = 2;
            double increment = 2;
            double startSeekPosition, endSeekPosition;
            bool foundBefore = position == 0;
            bool foundAfter = false;

            process.StartInfo = psi;
            position = Math.Round(position, 2);

            do
            {
                distance += increment++;
                startSeekPosition = position > distance ? position - distance : 0;
                endSeekPosition = position + distance;
                process.StartInfo.Arguments = $"-read_intervals {startSeekPosition:0.00}%{endSeekPosition:0.00} -select_streams v -skip_frame nokey -show_entries frame=key_frame,pkt_pts_time -print_format csv=p=0 \"{mediaInfo.Source}\" -hide_banner";
                process.Start();

                await Task.Run(() =>
                {
                    while ((line = process.StandardOutput.ReadLine()) != null)
                    {
                        string[] values = line.Split(','); // for vp9 videos -skip_frame nokey doesn't work, so it's necessary to include and check the key_frame flag
                        if (values[0] == "1")
                        {
                            double currentPosition = Ceiling(Double.Parse(values[1]), 2);
                            if (currentPosition < position)
                            {
                                if (currentPosition > nearestKeyFrameBefore)
                                {
                                    nearestKeyFrameBefore = currentPosition;
                                    foundBefore = true;
                                }
                            }
                            else if (currentPosition > position)
                            {
                                if (currentPosition < nearestKeyFrameAfter)
                                {
                                    nearestKeyFrameAfter = currentPosition;
                                    foundAfter = true;
                                }
                            }
                            else
                            {
                                isKeyFrame = true;
                            }
                        }
                    }
                }).ConfigureAwait(false);
            } while ((!foundBefore || !foundAfter) && distance < MAX_DISTANCE);

            return (nearestKeyFrameBefore, nearestKeyFrameAfter, position > 0 ? isKeyFrame : true);
        }

        private static bool BracketBalanced(string text)
        {
            int brackets = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{') brackets++;
                else if (text[i] == '}') brackets--;
            }
            return brackets == 0;
        }

        private static double Ceiling(double value, int digits)
        {
            // With digits = 2:
            // 5.123456 -> 5.13
            // 5.001000 -> 5.01
            // 5.110999 -> 5.12
            int x = (int)(value * Math.Pow(10, digits));
            x += 1;
            return x / Math.Pow(10, digits);
        }
    }
}