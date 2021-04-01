using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter.Controls
{
    public partial class AudioTrackControl : UserControl
    {
        public AudioTrack AudioTrack { get; }
        public AudioConversionOptions ConversionOptions 
        { 
            get 
            {
                if (comboBoxEncoders.SelectedIndex == 0) return null;

                AudioConversionOptions conversionOptions = new AudioConversionOptions();
                float bitrate = Convert.ToSingle(comboBoxBitrate.SelectedItem.ToString().Replace(" Kbps", ""));
                conversionOptions.Encoder = comboBoxEncoders.SelectedIndex == 1 ? new AACEncoder() : new OpusEncoder();
                conversionOptions.Encoder.Bitrate = new Bitrate(bitrate);
                conversionOptions.Title = textBoxTitle.Text;
                conversionOptions.Channels = channels[comboBoxChannels.Text];
                return conversionOptions;
            }
            set
            {
                comboBoxEncoders.SelectedIndex = value.Encoder is AACEncoder ? 1 : 2;
                for (int i = 0; i < comboBoxBitrate.Items.Count; i++)
                {
                    if (comboBoxBitrate.Items[i].ToString().Contains(value.Encoder.Bitrate.Kbps.ToString()))
                    {
                        comboBoxBitrate.SelectedIndex = i;
                        break;
                    }
                }
                textBoxTitle.Text = value.Title;
            }
        }

        public delegate void AudioTrackControlEventHandler(AudioTrack audioTrack);
        public event AudioTrackControlEventHandler ExportButtonClicked;

        Dictionary<string, byte> channels;

        public AudioTrackControl(AudioTrack audioTrack)
        {
            InitializeComponent();

            channels = new Dictionary<string, byte>
            {
                { "mono", 1 },
                { "stereo", 2 },
                { "5.1", 6 },
                { "7.1", 8 }
            };

            AudioTrack = audioTrack;
            textBlockTitle.Text = audioTrack.Title == "" ? "-" : audioTrack.Title;
            textBoxTitle.Text = audioTrack.Title;
            textBoxTitle.Width = textBlockTitle.Width + 6;
            textBlockLanguage.Text = audioTrack.Language == "" ? "-" : audioTrack.Language;
            textBlockCodec.Text = String.IsNullOrEmpty(audioTrack.Codec) ? "unknown" : audioTrack.Codec;
            textBlockBitrate.Text = audioTrack.Bitrate.Bps > 0 ? $"{audioTrack.Bitrate.Kbps:F0} Kbps" : "unknown";
            textBlockChannelLayout.Text = audioTrack.ChannelLayout;
            textBlockSize.Text = audioTrack.Size > 0 ? audioTrack.Size.ToBytesString() : "unknown";
            checkBoxTrackEnabled.IsChecked = audioTrack.Enabled;
            radioButtonDefaultTrack.IsChecked = audioTrack.Default;

            if (audioTrack.Channels > 7)
                comboBoxChannels.Items.Add("7.1");
            if (audioTrack.Channels > 5)
                comboBoxChannels.Items.Add("5.1");
            if (audioTrack.Channels > 1)
                comboBoxChannels.Items.Add("stereo");
                comboBoxChannels.Items.Add("mono");
            comboBoxChannels.SelectedIndex = 0;

            comboBoxEncoders.Items.Add("Copy");
            comboBoxEncoders.Items.Add("AAC");
            comboBoxEncoders.Items.Add("Opus");
            comboBoxEncoders.SelectedIndex = 0;
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

        private void ComboBoxEncoders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBoxEncoders.SelectedIndex == 0)
            {
                stackPanelEncodeControls.Visibility = Visibility.Hidden;
                textBlockTitle.Visibility = Visibility.Visible;
                textBoxTitle.Visibility = Visibility.Collapsed;
            }
            else
            {
                stackPanelEncodeControls.Visibility = Visibility.Visible;
                textBlockTitle.Visibility = Visibility.Collapsed;
                textBoxTitle.Visibility = Visibility.Visible;
                textBoxTitle.Text = comboBoxEncoders.SelectedItem.ToString();
                if (AudioTrack.Channels > 2)
                    textBoxTitle.Text += $" {comboBoxChannels.Text}";
            }

            SetupComboBoxBitrate();
        }

        private void SetupComboBoxBitrate()
        {
            comboBoxBitrate.Items.Clear();

            int baseBitrate = comboBoxEncoders.SelectedIndex == 1 ? 96 : 64;
            int outputChannels = channels[comboBoxChannels.SelectedItem.ToString()];

            baseBitrate *= outputChannels / 2;
            for (int i = 0; i < 6; i++)
            {
                int bitrate = baseBitrate + (int)(32f * i * outputChannels / 2f);
                comboBoxBitrate.Items.Add($"{bitrate} Kbps");
            }

            comboBoxBitrate.SelectedIndex = 2;
        }

        private void ComboBoxChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetupComboBoxBitrate();
        }
    }
}