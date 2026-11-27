using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsIsland
{
    // The shortcuts page: microphone, speaker, volume, a screenshot and the sound settings.
    public partial class IslandWindow
    {
        bool quickSample;                       // README images: made-up states, the audio is not touched
        bool quickMicMuted, quickSpeakerMuted;
        int quickVolume = -1;
        DateTime quickRead = DateTime.MinValue;
        static readonly Brush QuickGray = Freeze(new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)));

        void SetQuickVolume(double x)
        {
            double width = Math.Max(1, QuickVolumeTrack.ActualWidth);
            int percent = (int)Math.Round(Math.Clamp(x / width, 0, 1) * 100);
            quickVolume = percent;
            if (!quickSample) AudioControl.SetVolume(percent);
            UpdateQuick(true);
        }

        // Reads the switches again (a few times a second while the page is open) and draws them
        void UpdateQuick(bool force)
        {
            if (!force && (DateTime.Now - quickRead).TotalMilliseconds < 250) return;
            quickRead = DateTime.Now;
            if (!quickSample)
            {
                quickMicMuted = AudioControl.IsMuted(AudioControl.Capture);
                quickSpeakerMuted = AudioControl.IsMuted(AudioControl.Render);
                int volume = AudioControl.Volume();
                if (!QuickVolumeArea.IsMouseCaptured && volume >= 0) quickVolume = volume;
            }
            QuickTitleText.Text = Strings.QuickTitle;
            QuickMicLabel.Text = quickMicMuted ? Strings.QuickMicMuted : Strings.QuickMic;
            QuickSpeakerLabel.Text = quickSpeakerMuted ? Strings.QuickSpeakerMuted : Strings.QuickSpeaker;
            QuickShotLabel.Text = Strings.QuickShot;
            QuickSoundLabel.Text = Strings.QuickSoundSettings;
            QuickMicButton.Background = quickMicMuted ? Red : QuickGray;
            QuickMicSlash.Visibility = quickMicMuted ? Visibility.Visible : Visibility.Collapsed;
            QuickSpeakerButton.Background = quickSpeakerMuted ? Brushes.White : QuickGray;
            QuickSpeakerGlyph.Foreground = quickSpeakerMuted ? Brushes.Black : Brushes.White;
            QuickSpeakerGlyph.Text = quickSpeakerMuted ? "\uE74F" : "\uE767";
            int shown = Math.Max(0, quickVolume);
            QuickVolumeFill.Width = Math.Max(0, QuickVolumeTrack.ActualWidth > 0 ? QuickVolumeTrack.ActualWidth : 280) * shown / 100;
            QuickVolumeText.Text = quickVolume < 0 ? "" : shown + "%";
        }
    }
}
