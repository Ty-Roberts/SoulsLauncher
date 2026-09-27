using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Souls Launcher")]
[assembly: System.Reflection.AssemblyDescription("Community launcher for Souls games")]
[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]

internal sealed class Game
{
    public string Id, Name, Subtitle, Family, Art, LauncherFile, SettingsFile, SteamAppId;
    public int Slice;
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            bool smokeTest = args.Any(a => a.Equals("/smoketest", StringComparison.OrdinalIgnoreCase));
            bool screenshotMode = args.Length > 0 && args[0].Equals("/screenshots", StringComparison.OrdinalIgnoreCase);
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var launcher = new LauncherWindow(smokeTest || screenshotMode);
            if (screenshotMode)
            {
                launcher.CaptureScreenshots(args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots"));
                launcher.Window.Close();
                return 0;
            }
            if (smokeTest)
            {
                launcher.SmokeTest();
                launcher.Window.Close();
                return 0;
            }
            app.Run(launcher.Window);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Souls Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}

internal sealed class LauncherWindow
{
    public Window Window { get; private set; }

    private readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    private readonly string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoulsLauncher");
    private readonly Dictionary<string, string> settings = new Dictionary<string, string>();
    private readonly List<Game> games = new List<Game>
    {
        new Game { Id="ds1", Name="DARK SOULS", Subtitle="REMASTERED", Family="Souls", Art="souls", Slice=0, SteamAppId="570940", LauncherFile="ds1sc_launcher.exe", SettingsFile="ds1sc_settings.ini" },
        new Game { Id="ds2", Name="DARK SOULS II", Subtitle="SCHOLAR OF THE FIRST SIN", Family="Souls", Art="souls", Slice=1, SteamAppId="335300", LauncherFile="ds2sc_launcher.exe", SettingsFile="ds2sc_settings.ini" },
        new Game { Id="ds3", Name="DARK SOULS III", Subtitle="THE FIRE FADES", Family="Souls", Art="souls", Slice=2, SteamAppId="374320", LauncherFile="ds3sc_launcher.exe", SettingsFile="ds3sc_settings.ini" },
        new Game { Id="er", Name="ELDEN RING", Subtitle="THE LANDS BETWEEN", Family="Elden", Art="elden", Slice=0, SteamAppId="1245620", LauncherFile="ersc_launcher.exe", SettingsFile="ersc_settings.ini" },
        new Game { Id="ern", Name="ELDEN RING NIGHTREIGN", Subtitle="", Family="Elden", Art="elden", Slice=1, SteamAppId="2622380", LauncherFile="nrsc_launcher.exe", SettingsFile="nrsc_settings.ini" }
    };

    private Grid cardsHost;
    private Grid homeView;
    private Border settingsView;
    private StackPanel settingsRows;
    private Button soulsTab, eldenTab, maxButton;
    private TextBlock statusText;
    private string selectedFamily = "Souls";

    public LauncherWindow(bool smokeTest = false)
    {
        LoadSettings();
        if (!smokeTest) DetectSteamGames(false);
        Window = (Window)XamlReader.Parse(Xaml);
        FindControls();
        WireEvents();
        RenderCards();
    }

    public void SmokeTest()
    {
        BuildSettingsRows();
        if (cardsHost.Children.Count != 3 || settingsRows.Children.Count != 5)
            throw new InvalidOperationException("UI smoke test failed.");
        foreach (var image in new[] { "souls-panorama.png", "elden-panorama.png" })
            if (!File.Exists(Path.Combine(root, "assets", image))) throw new FileNotFoundException("Missing artwork", image);
    }

    public void CaptureScreenshots(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        Window.Width = 1280; Window.Height = 760;
        Window.WindowStartupLocation = WindowStartupLocation.Manual;
        Window.Left = -20000; Window.Top = -20000;
        Window.Show();
        Window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);

        selectedFamily = "Souls"; RenderCards(); SaveScreenshot(Path.Combine(outputDirectory, "dark-souls.png"));
        selectedFamily = "Elden"; RenderCards(); SaveScreenshot(Path.Combine(outputDirectory, "elden-ring.png"));
        Window.Hide();
    }

    private void SaveScreenshot(string path)
    {
        Window.UpdateLayout();
        Window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
        int width = Math.Max(1, (int)Math.Round(Window.ActualWidth));
        int height = Math.Max(1, (int)Math.Round(Window.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }

    private T Find<T>(string name) where T : class { return Window.FindName(name) as T; }

    private void FindControls()
    {
        cardsHost = Find<Grid>("CardsHost"); homeView = Find<Grid>("HomeView");
        settingsView = Find<Border>("SettingsView"); settingsRows = Find<StackPanel>("SettingsRows");
        soulsTab = Find<Button>("SoulsTab"); eldenTab = Find<Button>("EldenTab"); maxButton = Find<Button>("MaxButton");
        statusText = Find<TextBlock>("StatusText");
    }

    private void WireEvents()
    {
        Find<Button>("CloseButton").Click += (s, e) => Window.Close();
        Find<Button>("MinButton").Click += (s, e) => Window.WindowState = WindowState.Minimized;
        maxButton.Click += (s, e) => ToggleMaximize();
        Find<Button>("SettingsButton").Click += (s, e) => ShowSettings();
        Find<Button>("ScanSteamButton").Click += (s, e) => { DetectSteamGames(true); BuildSettingsRows(); RenderCards(); };
        Find<Button>("BackButton").Click += (s, e) => ShowHome();
        soulsTab.Click += (s, e) => { selectedFamily = "Souls"; RenderCards(); };
        eldenTab.Click += (s, e) => { selectedFamily = "Elden"; RenderCards(); };
        Window.StateChanged += (s, e) => maxButton.Content = Window.WindowState == WindowState.Maximized ? "❐" : "□";
        Window.KeyDown += (s, e) => { if (e.Key == Key.Escape && settingsView.Visibility == Visibility.Visible) ShowHome(); };
        var titleBar = Find<Border>("TitleBar");
        titleBar.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else if (!(e.Source is Button)) try { Window.DragMove(); } catch { }
        };
    }

    private void ToggleMaximize()
    {
        Window.WindowState = Window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void RenderCards()
    {
        cardsHost.Children.Clear(); cardsHost.ColumnDefinitions.Clear();
        var visible = games.Where(g => g.Family == selectedFamily).ToList();
        for (int i = 0; i < visible.Count; i++)
        {
            cardsHost.ColumnDefinitions.Add(new ColumnDefinition());
            var card = CreateCard(visible[i], visible.Count);
            Grid.SetColumn(card, i); cardsHost.Children.Add(card);
        }
        bool souls = selectedFamily == "Souls";
        SetSegment(soulsTab, souls); SetSegment(eldenTab, !souls);
    }

    private static Brush Brush(string color) { return (Brush)new BrushConverter().ConvertFromString(color); }

    private void SetSegment(Button button, bool active)
    {
        button.Background = active ? Brush("#594728") : Brushes.Transparent;
        button.Foreground = active ? Brush("#F4D99F") : Brush("#77736B");
    }

    private Border CreateCard(Game game, int count)
    {
        var card = new Border
        {
            Margin = new Thickness(1, 0, 1, 0), BorderBrush = Brush("#494135"), BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand, Tag = game.Id
        };
        string art = game.Art == "souls" ? "souls-panorama.png" : "elden-panorama.png";
        var image = new BitmapImage(new Uri(Path.Combine(root, "assets", art), UriKind.Absolute));
        card.Background = new ImageBrush(image)
        {
            Stretch = Stretch.UniformToFill, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = new Rect((double)game.Slice / count, 0, 1.0 / count, 1)
        };

        var grid = new Grid();
        grid.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Fill = new LinearGradientBrush(Color.FromArgb(18, 0, 0, 0), Color.FromArgb(245, 4, 5, 6), 90)
        });
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 0, 24, 25) };
        stack.Children.Add(new TextBlock
        {
            Text = game.Name, Foreground = Brushes.White, FontFamily = new FontFamily("Georgia"), FontSize = 25,
            FontWeight = FontWeights.Light, Margin = new Thickness(0, 0, 0, 13)
        });
        stack.Children.Add(new Border { Height = 1, Background = Brush("#6B6049"), Margin = new Thickness(0, 0, 0, 12) });
        stack.Children.Add(new TextBlock
        {
            Text = IsExecutable(GetLauncherPath(game, settings[game.Id])) ? "LAUNCH GAME  >" : "SET GAME FOLDER  >",
            Foreground = Brush("#D8D2C6"), FontSize = 11
        });
        grid.Children.Add(stack); card.Child = grid;
        card.MouseEnter += (s, e) => { card.BorderBrush = Brush("#C7A568"); card.BorderThickness = new Thickness(2); };
        card.MouseLeave += (s, e) => { card.BorderBrush = Brush("#494135"); card.BorderThickness = new Thickness(1); };
        card.MouseLeftButtonUp += (s, e) => LaunchGame(game);
        return card;
    }

    private void LaunchGame(Game game)
    {
        string path = GetLauncherPath(game, settings[game.Id]);
        if (!IsExecutable(path)) { statusText.Text = "PATH REQUIRED"; ShowSettings(); return; }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, WorkingDirectory = Path.GetDirectoryName(path), UseShellExecute = true });
            statusText.Text = "LAUNCHED  /  " + game.Name;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not launch the executable.\n\n" + ex.Message, "Souls Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            statusText.Text = "LAUNCH FAILED";
        }
    }

    private void ShowSettings() { BuildSettingsRows(); homeView.Visibility = Visibility.Collapsed; settingsView.Visibility = Visibility.Visible; }
    private void ShowHome() { SaveSettings(); settingsView.Visibility = Visibility.Collapsed; homeView.Visibility = Visibility.Visible; RenderCards(); }

    private void BuildSettingsRows()
    {
        settingsRows.Children.Clear();
        foreach (var game in games)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 11) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            var label = new TextBlock
            {
                Text = String.IsNullOrWhiteSpace(game.Subtitle) ? game.Name : game.Name + "\n" + game.Subtitle, Foreground = Brush("#D3CEC2"), FontFamily = new FontFamily("Georgia"),
                FontSize = 13, VerticalAlignment = VerticalAlignment.Center
            };
            var box = new TextBox
            {
                Text = settings[game.Id], Style = (Style)Window.FindResource("Field"), Margin = new Thickness(0, 0, 10, 0)
            };
            var browse = new Button
            {
                Content = "BROWSE", Style = (Style)Window.FindResource("TopButton"), Background = Brush("#1C1A16"), Tag = game.Id
            };
            var editIni = new Button
            {
                Content = "OPEN SEAMLESS SETTINGS", Style = (Style)Window.FindResource("TopButton"), Background = Brush("#1C1A16"),
                Margin = new Thickness(8, 0, 0, 0)
            };
            Action refreshIni = () => editIni.Visibility = File.Exists(GetSettingsPath(game, box.Text)) ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (s, e) => { settings[game.Id] = box.Text; refreshIni(); };
            browse.Click += (s, e) =>
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select the game's Steam folder or its Game folder",
                    ShowNewFolderButton = false
                })
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string resolved = ResolveGameFolder(game, dialog.SelectedPath);
                        box.Text = resolved;
                        if (!IsExecutable(GetLauncherPath(game, resolved)))
                            MessageBox.Show("The expected launcher was not found in this folder or its Game subfolder:\n\n" + game.LauncherFile,
                                "Souls Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
            };
            editIni.Click += (s, e) => OpenSettingsFile(game, box.Text);
            refreshIni();
            Grid.SetColumn(box, 1); Grid.SetColumn(browse, 2); Grid.SetColumn(editIni, 3);
            row.Children.Add(label); row.Children.Add(box); row.Children.Add(browse); row.Children.Add(editIni); settingsRows.Children.Add(row);
        }
    }

    private string ResolveGameFolder(Game game, string selected)
    {
        if (String.IsNullOrWhiteSpace(selected)) return "";
        string direct = Path.GetFullPath(selected);
        if (File.Exists(Path.Combine(direct, game.LauncherFile))) return direct;
        string child = Path.Combine(direct, "Game");
        if (File.Exists(Path.Combine(child, game.LauncherFile))) return child;
        return Directory.Exists(child) ? child : direct;
    }

    private string GetLauncherPath(Game game, string folder)
    {
        if (String.IsNullOrWhiteSpace(folder)) return "";
        if (File.Exists(folder)) return folder;
        string resolved = ResolveGameFolder(game, folder);
        return Path.Combine(resolved, game.LauncherFile);
    }

    private string GetSettingsPath(Game game, string folder)
    {
        if (String.IsNullOrWhiteSpace(folder)) return "";
        string resolved = ResolveGameFolder(game, File.Exists(folder) ? Path.GetDirectoryName(folder) : folder);
        return Path.Combine(resolved, "SeamlessCoop", game.SettingsFile);
    }

    private void OpenSettingsFile(Game game, string folder)
    {
        string path = GetSettingsPath(game, folder);
        if (!File.Exists(path))
        {
            MessageBox.Show("The settings file was not found:\n\n" + path, "Souls Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Could not open the settings file.\n\n" + ex.Message, "Souls Launcher"); }
    }

    private void DetectSteamGames(bool notify)
    {
        int found = 0;
        foreach (string library in GetSteamLibraries())
        {
            foreach (var game in games)
            {
                string manifest = Path.Combine(library, "steamapps", "appmanifest_" + game.SteamAppId + ".acf");
                if (!File.Exists(manifest)) continue;
                try
                {
                    var match = Regex.Match(File.ReadAllText(manifest), "\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;
                    string installed = Path.Combine(library, "steamapps", "common", match.Groups[1].Value.Replace("\\\\", "\\"));
                    string resolved = ResolveGameFolder(game, installed);
                    if (Directory.Exists(resolved) && (!Directory.Exists(settings[game.Id]) || !IsExecutable(GetLauncherPath(game, settings[game.Id]))))
                    {
                        settings[game.Id] = resolved;
                        found++;
                    }
                }
                catch { }
            }
        }
        if (found > 0) SaveSettings();
        if (notify)
            MessageBox.Show(found > 0 ? "Steam locations updated for " + found + " game" + (found == 1 ? "." : "s.") : "No new game locations were found in your Steam libraries.",
                "Souls Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private IEnumerable<string> GetSteamLibraries()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string steam = null;
        try { steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string; } catch { }
        if (String.IsNullOrWhiteSpace(steam))
            try { steam = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string; } catch { }
        if (String.IsNullOrWhiteSpace(steam)) return libraries;
        steam = steam.Replace('/', '\\'); libraries.Add(steam);
        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        try
        {
            if (File.Exists(vdf))
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase))
                    libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        }
        catch { }
        return libraries;
    }

    private bool IsExecutable(string path) { return !String.IsNullOrWhiteSpace(path) && File.Exists(path); }
    private string ConfigFile { get { return Path.Combine(configDir, "settings.json"); } }

    private void LoadSettings()
    {
        foreach (var game in games) settings[game.Id] = "";
        try
        {
            if (!File.Exists(ConfigFile)) return;
            var saved = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(ConfigFile));
            foreach (var pair in saved)
                if (settings.ContainsKey(pair.Key)) settings[pair.Key] = File.Exists(pair.Value ?? "") ? Path.GetDirectoryName(pair.Value) : (pair.Value ?? "");
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(configDir);
            File.WriteAllText(ConfigFile, new JavaScriptSerializer().Serialize(settings));
        }
        catch (Exception ex) { MessageBox.Show("Settings could not be saved.\n\n" + ex.Message, "Souls Launcher"); }
    }

    private const string Xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
 Title='Souls Launcher' Width='1280' Height='760' MinWidth='980' MinHeight='620' WindowStartupLocation='CenterScreen'
 WindowStyle='None' AllowsTransparency='True' Background='Transparent' ResizeMode='CanResizeWithGrip'>
 <Window.Resources>
  <Style x:Key='TopButton' TargetType='Button'><Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#BDB9AF'/><Setter Property='BorderThickness' Value='0'/><Setter Property='FontFamily' Value='Segoe UI Semibold'/><Setter Property='FontSize' Value='13'/><Setter Property='Padding' Value='17,10'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='b' Background='{TemplateBinding Background}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#1CFFFFFF'/><Setter Property='Foreground' Value='#F4E4BE'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style x:Key='SegmentButton' TargetType='Button'><Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#8E8A82'/><Setter Property='BorderThickness' Value='0'/><Setter Property='FontFamily' Value='Segoe UI Semibold'/><Setter Property='FontSize' Value='12'/><Setter Property='Height' Value='30'/><Setter Property='MinWidth' Value='150'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='15'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
  <Style x:Key='WindowButton' TargetType='Button'><Setter Property='Width' Value='46'/><Setter Property='Height' Value='34'/><Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#BDB9AF'/><Setter Property='BorderThickness' Value='0'/><Setter Property='FontFamily' Value='Segoe UI'/><Setter Property='FontSize' Value='15'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='2'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#24FFFFFF'/><Setter Property='Foreground' Value='White'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style x:Key='CloseButton' TargetType='Button' BasedOn='{StaticResource WindowButton}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='b' Background='{TemplateBinding Background}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#C42B1C'/><Setter Property='Foreground' Value='White'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style x:Key='Field' TargetType='TextBox'><Setter Property='Background' Value='#B30A0B0D'/><Setter Property='Foreground' Value='#EAE5D8'/><Setter Property='BorderBrush' Value='#4A453B'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='12,10'/><Setter Property='FontSize' Value='13'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
 </Window.Resources>
 <Border CornerRadius='10' Background='#FF090A0C' BorderBrush='#443C2E' BorderThickness='1'><Grid>
  <Grid.RowDefinitions><RowDefinition Height='58'/><RowDefinition Height='*'/><RowDefinition Height='36'/></Grid.RowDefinitions>
  <Border x:Name='TitleBar' Grid.Row='0' Background='#F20C0D0F' BorderBrush='#332E27' BorderThickness='0,0,0,1' CornerRadius='10,10,0,0'><Grid Margin='20,0,8,0'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
   <StackPanel Orientation='Horizontal' VerticalAlignment='Center'><Border Width='24' Height='24' CornerRadius='12' BorderBrush='#C7A568' BorderThickness='1' Margin='0,0,12,0'><TextBlock Text='&#x2726;' Foreground='#C7A568' FontSize='12' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><TextBlock Text='SOULS' Foreground='#E9E4D7' FontFamily='Georgia' FontSize='16' FontWeight='Bold' VerticalAlignment='Center'/><TextBlock Text='  LAUNCHER' Foreground='#77736B' FontFamily='Georgia' FontSize='16' VerticalAlignment='Center'/></StackPanel>
   <Button x:Name='SettingsButton' Grid.Column='1' Style='{StaticResource TopButton}' Content='&#x2699;  SETTINGS'/>
   <StackPanel Grid.Column='2' Orientation='Horizontal'><Button x:Name='MinButton' Style='{StaticResource WindowButton}' Content='&#x2500;'/><Button x:Name='MaxButton' Style='{StaticResource WindowButton}' Content='&#x25A1;'/><Button x:Name='CloseButton' Style='{StaticResource CloseButton}' Content='&#x00D7;'/></StackPanel>
  </Grid></Border>
  <Grid Grid.Row='1'><Grid x:Name='HomeView'><Grid x:Name='CardsHost'/><StackPanel HorizontalAlignment='Center' VerticalAlignment='Top' Margin='0,24,0,0' Panel.ZIndex='20'><TextBlock Text='CHOOSE YOUR JOURNEY' Foreground='#F4EFE4' FontFamily='Georgia' FontSize='28' HorizontalAlignment='Center'><TextBlock.Effect><DropShadowEffect BlurRadius='12' ShadowDepth='1' Opacity='.95' Color='Black'/></TextBlock.Effect></TextBlock><Border Background='#E60B0C0E' BorderBrush='#665B45' BorderThickness='1' CornerRadius='19' Padding='3' Margin='0,12,0,0' HorizontalAlignment='Center'><StackPanel Orientation='Horizontal'><Button x:Name='SoulsTab' Content='DARK SOULS' Style='{StaticResource SegmentButton}'/><Button x:Name='EldenTab' Content='ELDEN RING' Style='{StaticResource SegmentButton}'/></StackPanel></Border></StackPanel></Grid>
   <Border x:Name='SettingsView' Visibility='Collapsed' Background='#FF0B0C0E' Padding='54,32'><Grid MaxWidth='1040'><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='*'/><RowDefinition Height='Auto'/></Grid.RowDefinitions><Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions><StackPanel><TextBlock Text='GAME FOLDERS' Foreground='#EEE8DB' FontFamily='Georgia' FontSize='29'/><TextBlock Text='Steam libraries are scanned automatically. Manual folder selection remains available.' Foreground='#817E77' Margin='0,8,0,22'/></StackPanel><Button x:Name='ScanSteamButton' Grid.Column='1' Content='SCAN STEAM' Style='{StaticResource TopButton}' Height='38' Margin='18,0,0,0' Background='#2B2419' Foreground='#E5CEA1'/></Grid><ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto'><StackPanel x:Name='SettingsRows'/></ScrollViewer><Button x:Name='BackButton' Grid.Row='2' Content='DONE' Style='{StaticResource TopButton}' Width='120' HorizontalAlignment='Right' Margin='0,20,0,0' Background='#2B2419' Foreground='#E5CEA1'/></Grid></Border>
  </Grid>
  <Border Grid.Row='2' BorderBrush='#292621' BorderThickness='0,1,0,0' Background='#FF0B0C0E' CornerRadius='0,0,10,10'><Grid Margin='18,0'><TextBlock Text='SOULS SERIES  &#x2022;  COMMUNITY LAUNCHER' Foreground='#56534D' FontSize='10' VerticalAlignment='Center'/><TextBlock x:Name='StatusText' Text='READY' Foreground='#71674F' FontSize='10' VerticalAlignment='Center' HorizontalAlignment='Right'/></Grid></Border>
 </Grid></Border>
</Window>";
}
