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
            sliderVolume.Value = audioTrack.Volume;
            sliderVolume.ValueChanged += SliderVolume_ValueChanged;
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            ExportButtonClicked?.Invoke(AudioTrack);
        }

        private void RadioButtonDefaultTrack_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AudioTrack.Default = radioButtonDefaultTrack.IsChecked.Value;
        }

        private void CheckBoxTrackEnabled_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AudioTrack.Enabled = checkBoxTrackEnabled.IsChecked.Value;
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            AudioTrack.Volume = sliderVolume.Value;
        }
    }
}