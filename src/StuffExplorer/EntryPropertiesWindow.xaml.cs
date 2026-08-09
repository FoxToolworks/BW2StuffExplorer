using StuffCore;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace StuffExplorer;

public partial class EntryPropertiesWindow : Window
{
    private static Rect? _sessionBounds;

    public EntryPropertiesWindow(StuffEntry entry, string archivePath)
    {
        InitializeComponent();
        NameText.Text = entry.Name;
        PathText.Text = entry.Path;
        TypeText.Text = string.IsNullOrEmpty(entry.Extension) ? MainWindow.S("NoType") : entry.Extension;
        HeaderNameText.Text = entry.Name;
        HeaderTypeText.Text = string.IsNullOrEmpty(entry.Extension) ? MainWindow.S("NoType") : entry.Extension + " file";
        ModifiedText.Text = $"{entry.ModifiedLocalTime:G} ({entry.ModifiedUtc:yyyy-MM-dd HH:mm:ss} UTC)";
        SizeText.Text = $"{FileSizeConverter.Format(entry.Length)} ({entry.Length:N0} bytes)";
        OffsetText.Text = $"0x{entry.Offset:X8} ({entry.Offset:N0})";
        ArchiveText.Text = archivePath;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_sessionBounds is not { } bounds)
            return;

        Left = bounds.Left;
        Top = bounds.Top;
        Width = Math.Max(MinWidth, bounds.Width);
        Height = Math.Max(MinHeight, bounds.Height);
    }

    private void Window_Closing(object? sender, CancelEventArgs e) =>
        _sessionBounds = RestoreBounds;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close();
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
