using System;
using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter
{
    public partial class AudioTrackControl : UserControl
    {
        public delegate void ExportEventHandler(AudioTrack audioTrack);
        public event ExportEventHandler ExportButtonClicked;

        public AudioTrack AudioTrack { get; }

        public AudioTrackControl()
        {
            InitializeComponent();
        }

        public AudioTrackControl(AudioTrack audioTrack)
        {
            InitializeComponent();

            AudioTrack = audioTrack;
            textBlockLanguage.Text = audioTrack.Language;
            textBlockCodec.Text = String.IsNullOrEmpty(audioTrack.Codec) ? "unknown" : audioTrack.Codec;
            textBlockBitrate.Text = audioTrack.Bitrate > 0 ? $"{audioTrack.Bitrate / 1000} Kbps" : "unknown";
            textBlockSampleRate.Text = audioTrack.SampleRate > 0 ? $"{audioTrack.SampleRate} Hz" : "unknown";
            textBlockSize.Text = audioTrack.Size > 0 ? audioTrack.Size.ToBytesString() : "unknown";
            checkBoxTrackEnabled.IsChecked = audioTrack.Enabled;
            radioButtonDefaultTrack.IsChecked = audioTrack.Default;
            //comboBoxEncoder.Items.Add("Native (copy track)");
            //comboBoxEncoder.Items.Add("Flac (lossless)");
            //comboBoxEncoder.Items.Add("Aac (lossy)");
            //comboBoxEncoder.SelectedIndex = 0;
        }

        /*private void ComboBoxEncoder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (comboBoxEncoder.SelectedIndex)
            {
                case 0: //Native
                    comboBoxBitrate.IsEnabled = false;
                    break;
                case 1: //Flac
                    comboBoxBitrate.IsEnabled = false;
                    break;
                case 3: //Aac
                    break;
            }
        }*/

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            ExportButtonClicked?.Invoke(AudioTrack);

            //Flac:
            //ffmpeg -i input.mp4 -vn -c:a flac -compression_level 0 output.flac     lowest compression
            //ffmpeg -i input.mp4 -vn -c:a flac -compression_level 5 output.flac     medium compression
            //ffmpeg -i input.mp4 -vn -c:a flac -compression_level 10 output.flac    highest compression

            //Aac:
            //ffmpeg -i input.mp4 -vn -c:a aac -q:a 0.1 output.aac    lowest quality
            //ffmpeg -i input.mp4 -vn -c:a aac -q:a 2 output.aac      highest quality
        }

        private void RadioButtonDefaultTrack_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AudioTrack.Default = radioButtonDefaultTrack.IsChecked.Value;
        }

        private void CheckBoxTrackEnabled_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AudioTrack.Enabled = checkBoxTrackEnabled.IsChecked.Value;
        }
    }
}