using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The privacy dots (microphone, camera, screen), the muted microphone and presentation mode.
    public partial class IslandWindow
    {
        void OnPrivacyChanged() { UpdatePrivacy(); UpdateClipboard(true); Recompute(); }

        // Presentation mode: while the screen is shared or recorded, only the timer (and its alert) shows. No dots either.
        bool Presenting { get { return IslandRules.Presenting(settings.PresentationMode, privacy.Screen); } }

        // True on an island that is not on the active monitor ("only on the monitor you are using"): no music or Claude here.
        bool muted;
        internal void SetMuted(bool value)
        {
            if (muted == value) return;
            muted = value;
            Recompute();
        }

        bool ScreenOn { get { return !Presenting && settings.ShowPrivacyDots && settings.ShowScreenShare && privacy.Screen; } }
        bool PrivacyOn { get { return !Presenting && settings.ShowPrivacyDots && (privacy.Microphone || privacy.Camera || ScreenOn); } }

        string privacyLabel = "";
        bool dotHover; // The mouse is over the dots themselves: that is what shows the app name
        DateTime privacyNameUntil;
        DispatcherTimer privacyNameTimer;

        // "Zoom", or "Zoom +1" when several apps are recording.
        string PrivacyLabel()
        {
            List<string> names = new List<string>();
            if (privacy.Microphone) names.AddRange(privacy.MicrophoneApps);
            if (privacy.Camera) names.AddRange(privacy.CameraApps);
            if (ScreenOn) names.AddRange(privacy.ScreenApps);
            names = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0) return "";
            string first = names[0].Length > 20 ? names[0].Substring(0, 19) + "…" : names[0];
            return names.Count > 1 ? first + " +" + (names.Count - 1) : first;
        }

        void UpdatePrivacy()
        {
            UpdateFocusMoon();
            bool mic = privacy.Microphone && settings.ShowPrivacyDots && !Presenting, cam = privacy.Camera && settings.ShowPrivacyDots && !Presenting, screen = ScreenOn;
            // On the thin line the dots sit inside it; otherwise they sit beside the island
            bool thin = view == View.Minimal;
            MicDot.Visibility = mic && !thin ? Visibility.Visible : Visibility.Collapsed;
            CamDot.Visibility = cam && !thin ? Visibility.Visible : Visibility.Collapsed;
            ScreenDot.Visibility = screen && !thin ? Visibility.Visible : Visibility.Collapsed;
            ThinMic.Visibility = mic && thin ? Visibility.Visible : Visibility.Collapsed;
            ThinCam.Visibility = cam && thin ? Visibility.Visible : Visibility.Collapsed;
            ThinScreen.Visibility = screen && thin ? Visibility.Visible : Visibility.Collapsed;
            bool rec = RecordingOn;
            RecDot.Visibility = rec && !thin ? Visibility.Visible : Visibility.Collapsed;
            if (thin) { mic = cam = screen = false; rec = false; } // Nothing of them beside the island, no name either (the line has its own recording dot)
            // Muted or deafened in Discord, while the Discord option is on and connected
            bool deaf = settings.DiscordEnabled && discord != null && discord.Deafened;
            bool muted = settings.DiscordEnabled && discord != null && discord.Muted && !deaf;
            DiscordDeaf.Visibility = deaf ? Visibility.Visible : Visibility.Collapsed;
            DiscordMute.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            bool discordShown = deaf || muted;

            // Hovering over the dots (not the island) shows the name of the app using the microphone or camera; a new app also shows it by itself if the setting asks for it.
            string label = mic || cam || screen ? PrivacyLabel() : "";
            bool anything = mic || cam || screen || discordShown || rec;
            if (label != privacyLabel)
            {
                bool added = label.Length > 0;
                privacyLabel = label;
                if (added && settings.PrivacyNameSeconds > 0)
                {
                    privacyNameUntil = DateTime.Now.AddSeconds(settings.PrivacyNameSeconds);
                    if (privacyNameTimer == null)
                    {
                        privacyNameTimer = new DispatcherTimer();
                        privacyNameTimer.Tick += (s, e) => { privacyNameTimer.Stop(); UpdatePrivacy(); };
                    }
                    privacyNameTimer.Interval = TimeSpan.FromSeconds(settings.PrivacyNameSeconds + 0.1);
                    privacyNameTimer.Stop();
                    privacyNameTimer.Start();
                }
            }
            bool showName = label.Length > 0 && (dotHover || DateTime.Now < privacyNameUntil);
            PrivacyDots.IsHitTestVisible = anything; // Only while a dot is there, so the spot is empty the rest of the time
            double dotsWidth = 10 + 10 * ((mic ? 1 : 0) + (cam ? 1 : 0) + (screen ? 1 : 0) + (rec ? 1 : 0)) + (discordShown ? 17 : 0), nameWidth = 0;
            PrivacyName.Visibility = showName ? Visibility.Visible : Visibility.Collapsed;
            if (showName)
            {
                if (PrivacyName.Text != label) PrivacyName.Text = label;
                PrivacyName.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                nameWidth = PrivacyName.DesiredSize.Width + PrivacyName.Margin.Left + PrivacyName.Margin.Right;
            }
            PrivacyDots.Width = dotsWidth + nameWidth;
            privacyOpacity.Target = anything ? 1 : 0;
        }
    }
}
