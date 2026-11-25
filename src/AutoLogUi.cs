// Hallow Mixer 3.29 - the "Why?" button by the status line: what Mixer turned down (or back up) by itself, newest first.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HallowMixer
{
    public partial class MixerWindow
    {
        Button BuildWhyButton()
        {
            var b = new Button { Content = "Why?", Style = (Style)FindResource("Btn"), Padding = new Thickness(9, 1, 9, 2), FontSize = 11.5, Margin = new Thickness(8, 6, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "What Mixer turned down by itself, and why (ducking for Chat, Smart Volume, per-game levels, ads...)" };
            b.Click += (s, e) => ShowWhy();
            AutoLog.Added += () => Dispatcher.BeginInvoke(new Action(() => { var l = AutoLog.Recent(1).FirstOrDefault(); if (l != null && l.Kind != "quiet" && l.Kind != "game-levels" && l.Kind != "ad") Status(l.Text); }));
            return b;
        }
        public Window WhyWindow()
        {
            var w = new Window { Title = "Hallow Mixer - why the volume changed", Width = 620, SizeToContent = SizeToContent.Height, MaxHeight = 640, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Look.Panel, Foreground = Look.Text, ResizeMode = ResizeMode.CanResize };
            var sp = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
            sp.Children.Add(new TextBlock { Text = "What Mixer turned down by itself", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(new TextBlock { Text = "Newest first, since Mixer started. Each line says which setting did it, so you can switch that one off.", Foreground = Look.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            var items = AutoLog.Recent(60);
            var list = new StackPanel();
            if (items.Count == 0) list.Children.Add(new TextBlock { Text = "Nothing yet - Mixer hasn't changed any volume by itself since it started.", Foreground = Look.Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            foreach (var it in items)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var t = new TextBlock { Text = it.At.ToString("HH:mm:ss"), Foreground = Look.Muted, FontSize = 12, Width = 70, FontFamily = new FontFamily("Consolas") };
                DockPanel.SetDock(t, Dock.Left); row.Children.Add(t);
                row.Children.Add(new TextBlock { Text = it.Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
                list.Children.Add(row);
            }
            sp.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 480 });
            var close = new Button { Content = "Close", Style = (Style)FindResource("Btn"), HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(14, 3, 14, 4), Margin = new Thickness(0, 12, 0, 0) };
            close.Click += (s, e) => w.Close(); sp.Children.Add(close);
            w.Content = sp;
            return w;
        }
        void ShowWhy() { var w = WhyWindow(); try { w.Owner = this; } catch { } w.Show(); }
    }
}
