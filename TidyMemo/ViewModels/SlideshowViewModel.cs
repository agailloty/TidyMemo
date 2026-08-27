using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyMemo.Models;
using TidyMemo.Services;

namespace TidyMemo.ViewModels;

public partial class SlideshowViewModel : ViewModelBase
{
    private readonly SlideshowService _service;
    private readonly ExifService _exifService;
    private readonly IDialogService _dialogs;
    private readonly SettingsViewModel _settings;
    private CancellationTokenSource? _cancellation;

    public ObservableCollection<SlideshowItemViewModel> Images { get; } = new();
    public IReadOnlyList<SlideshowResolution> Resolutions { get; } = new[]
    {
        new SlideshowResolution("Full HD landscape", 1920, 1080),
        new SlideshowResolution("HD landscape", 1280, 720),
        new SlideshowResolution("Full HD portrait", 1080, 1920),
        new SlideshowResolution("Square", 1080, 1080),
        new SlideshowResolution("4K landscape", 3840, 2160)
    };
    public IReadOnlyList<SlideshowType> SlideshowTypes { get; } = Enum.GetValues<SlideshowType>();
    public IReadOnlyList<SlideshowBackgroundType> BackgroundTypes { get; } = Enum.GetValues<SlideshowBackgroundType>();
    public IReadOnlyList<SlideshowGradientDirection> GradientDirections { get; } = Enum.GetValues<SlideshowGradientDirection>();
    public IReadOnlyList<SlideshowSortMode> SortModes { get; } = Enum.GetValues<SlideshowSortMode>();
    public IReadOnlyList<string> EncoderPresets { get; } = new[] { "ultrafast", "veryfast", "fast", "medium", "slow", "slower" };

    [ObservableProperty] private SlideshowResolution _selectedResolution;
    [ObservableProperty] private SlideshowType _selectedType = SlideshowType.Basic;
    [ObservableProperty] private SlideshowBackgroundType _selectedBackgroundType = SlideshowBackgroundType.SolidColor;
    [ObservableProperty] private SlideshowGradientDirection _selectedGradientDirection;
    [ObservableProperty] private SlideshowSortMode _selectedSortMode = SlideshowSortMode.NaturalFileName;
    [ObservableProperty] private double _imageDuration = 3;
    [ObservableProperty] private int _frameRate = 30;
    [ObservableProperty] private int _quality = 18;
    [ObservableProperty] private string _encoderPreset = "medium";
    [ObservableProperty] private double _volume = 0.6;
    [ObservableProperty] private double _imageScaling = 0.8;
    [ObservableProperty] private string _backgroundColor = "#000000";
    [ObservableProperty] private string _gradientEndColor = "#303060";
    [ObservableProperty] private bool _enableBorder;
    [ObservableProperty] private int _borderWidth = 6;
    [ObservableProperty] private string _borderColor = "#FFFFFF";
    [ObservableProperty] private bool _enableShadow;
    [ObservableProperty] private int _shadowOffsetX = 12;
    [ObservableProperty] private int _shadowOffsetY = 12;
    [ObservableProperty] private int _shadowBlur = 18;
    [ObservableProperty] private double _shadowOpacity = 0.45;
    [ObservableProperty] private string? _audioFile;
    [ObservableProperty] private string? _backgroundImage;
    [ObservableProperty] private string _outputFile = string.Empty;
    [ObservableProperty] private bool _includeSubfolders;
    [ObservableProperty] private bool _useEnhancedBackgroundProcessing = true;
    [ObservableProperty] private bool _preferImageMagick;
    [ObservableProperty] private string _imageMagickPath = "magick";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanCreate))] private bool _isRunning;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusMessage = "Add images to begin.";

    public bool HasImages => Images.Count > 0;
    public bool IsBackgroundMode => SelectedType == SlideshowType.Background;
    public bool IsImageBackground => IsBackgroundMode && SelectedBackgroundType == SlideshowBackgroundType.Image;
    public bool IsGradientBackground => IsBackgroundMode && SelectedBackgroundType == SlideshowBackgroundType.Gradient;
    public bool CanCreate => HasImages && !IsRunning && _settings.IsFfmpegConfigured && !string.IsNullOrWhiteSpace(OutputFile);
    public string ImageCountText => $"{Images.Count} image(s) - approximately {TimeSpan.FromSeconds(Images.Count * ImageDuration):hh\\:mm\\:ss}";
    public bool IsFfmpegConfigured => _settings.IsFfmpegConfigured;

    public SlideshowViewModel(SlideshowService service, ExifService exifService, IDialogService dialogs,
        SettingsViewModel settings, Action openSettings)
    {
        _service = service; _exifService = exifService; _dialogs = dialogs; _settings = settings;
        _selectedResolution = Resolutions[0];
        AddImagesCommand = new AsyncRelayCommand(AddImagesAsync);
        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync);
        RemoveImageCommand = new RelayCommand<SlideshowItemViewModel>(RemoveImage);
        MoveUpCommand = new RelayCommand<SlideshowItemViewModel>(MoveUp);
        MoveDownCommand = new RelayCommand<SlideshowItemViewModel>(MoveDown);
        SortCommand = new RelayCommand(SortImages);
        ChooseAudioCommand = new AsyncRelayCommand(ChooseAudioAsync);
        ClearAudioCommand = new RelayCommand(() => AudioFile = null);
        ChooseBackgroundImageCommand = new AsyncRelayCommand(ChooseBackgroundAsync);
        ChooseOutputCommand = new AsyncRelayCommand(ChooseOutputAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => CanCreate);
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsRunning);
        ClearCommand = new RelayCommand(Clear, () => !IsRunning && HasImages);
        OpenOutputCommand = new RelayCommand(OpenOutput, () => File.Exists(OutputFile));
        OpenSettingsCommand = new RelayCommand(openSettings);
        Images.CollectionChanged += (_, _) => RefreshCollectionState();
        _settings.PropertyChanged += SettingsChanged;
    }

    public IAsyncRelayCommand AddImagesCommand { get; }
    public IAsyncRelayCommand AddFolderCommand { get; }
    public IRelayCommand<SlideshowItemViewModel> RemoveImageCommand { get; }
    public IRelayCommand<SlideshowItemViewModel> MoveUpCommand { get; }
    public IRelayCommand<SlideshowItemViewModel> MoveDownCommand { get; }
    public IRelayCommand SortCommand { get; }
    public IAsyncRelayCommand ChooseAudioCommand { get; }
    public IRelayCommand ClearAudioCommand { get; }
    public IAsyncRelayCommand ChooseBackgroundImageCommand { get; }
    public IAsyncRelayCommand ChooseOutputCommand { get; }
    public IAsyncRelayCommand CreateCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand OpenOutputCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }

    private async Task AddImagesAsync()
    {
        var paths = await _dialogs.ShowFilePickerMultipleAsync("Select slideshow images",
            new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.webp" });
        AddPaths(paths);
    }
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.ShowFolderBrowserDialogAsync();
        if (!string.IsNullOrWhiteSpace(folder)) AddPaths(_service.GetImages(folder, IncludeSubfolders));
    }
    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(_service.IsSupportedImage))
            if (Images.All(item => !item.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) Images.Add(new(path));
        SortImages();
    }
    private void RemoveImage(SlideshowItemViewModel? item) { if (item is not null) Images.Remove(item); }
    private void MoveUp(SlideshowItemViewModel? item) { if (item is null) return; var i = Images.IndexOf(item); if (i > 0) Images.Move(i, i - 1); Reindex(); }
    private void MoveDown(SlideshowItemViewModel? item) { if (item is null) return; var i = Images.IndexOf(item); if (i >= 0 && i < Images.Count - 1) Images.Move(i, i + 1); Reindex(); }
    private void SortImages()
    {
        IEnumerable<SlideshowItemViewModel> sorted = SelectedSortMode switch
        {
            SlideshowSortMode.ExifDate => Images.OrderBy(item => SafeExifDate(item.Path) ?? File.GetCreationTime(item.Path)),
            SlideshowSortMode.FileDate => Images.OrderBy(item => File.GetCreationTime(item.Path)),
            SlideshowSortMode.FileName => Images.OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase),
            _ => Images.OrderBy(item => NaturalKey(item.FileName), StringComparer.OrdinalIgnoreCase)
        };
        var snapshot = sorted.ToArray(); Images.Clear(); foreach (var item in snapshot) Images.Add(item); Reindex();
    }
    private DateTime? SafeExifDate(string path) { try { return _exifService.GetDateFromExif(path); } catch { return null; } }
    private static string NaturalKey(string value) => Regex.Replace(value, "[0-9]+", m => m.Value.PadLeft(20, '0'));
    private async Task ChooseAudioAsync() => AudioFile = await _dialogs.ShowFilePickerAsync("Select background music", new[] { "*.mp3", "*.wav", "*.m4a", "*.aac", "*.ogg", "*.flac" });
    private async Task ChooseBackgroundAsync() => BackgroundImage = await _dialogs.ShowFilePickerAsync("Select a background image", new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp" });
    private async Task ChooseOutputAsync()
    {
        var value = await _dialogs.ShowSaveFilePickerAsync("Save slideshow", "slideshow.mp4", ".mp4");
        if (!string.IsNullOrWhiteSpace(value)) OutputFile = value;
    }
    private async Task CreateAsync()
    {
        _cancellation = new CancellationTokenSource(); IsRunning = true; ProgressValue = 0; StatusMessage = "Preparing slideshow...";
        var progress = new Progress<SlideshowProgress>(p => { ProgressValue = p.Percentage; StatusMessage = p.Message; });
        var result = await _service.CreateAsync(new SlideshowOptions
        {
            Images = Images.Select(item => item.Path).ToArray(), OutputFile = OutputFile, FfmpegPath = _settings.FfmpegPath,
            AudioFile = AudioFile, ImageDuration = ImageDuration, FrameRate = FrameRate,
            Width = SelectedResolution.Width, Height = SelectedResolution.Height, Quality = Quality, EncoderPreset = EncoderPreset,
            Volume = Volume, Type = SelectedType, BackgroundType = SelectedBackgroundType, BackgroundColor = BackgroundColor,
            GradientEndColor = GradientEndColor, GradientDirection = SelectedGradientDirection, BackgroundImage = BackgroundImage,
            ImageScaling = ImageScaling, UseEnhancedBackgroundProcessing = UseEnhancedBackgroundProcessing,
            EnableBorder = EnableBorder, BorderWidth = BorderWidth, BorderColor = BorderColor,
            EnableShadow = EnableShadow, ShadowOffsetX = ShadowOffsetX, ShadowOffsetY = ShadowOffsetY,
            ShadowBlur = ShadowBlur, ShadowOpacity = ShadowOpacity,
            PreferImageMagick = PreferImageMagick, ImageMagickPath = ImageMagickPath
        }, progress, _cancellation.Token);
        StatusMessage = result.Success ? $"Slideshow created: {result.OutputFile}" : result.ErrorMessage ?? "Slideshow creation failed.";
        IsRunning = false; _cancellation.Dispose(); _cancellation = null; OpenOutputCommand.NotifyCanExecuteChanged();
    }
    private void Clear() { Images.Clear(); AudioFile = null; BackgroundImage = null; OutputFile = string.Empty; ProgressValue = 0; StatusMessage = "Add images to begin."; }
    private void OpenOutput() { if (File.Exists(OutputFile)) Process.Start(new ProcessStartInfo { FileName = OutputFile, UseShellExecute = true }); }
    private void Reindex() { for (var i = 0; i < Images.Count; i++) Images[i].Position = i + 1; OnPropertyChanged(nameof(ImageCountText)); }
    private void RefreshCollectionState() { Reindex(); OnPropertyChanged(nameof(HasImages)); OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); ClearCommand.NotifyCanExecuteChanged(); }
    private void SettingsChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName != nameof(SettingsViewModel.FfmpegPath)) return; OnPropertyChanged(nameof(IsFfmpegConfigured)); OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedTypeChanged(SlideshowType value) { OnPropertyChanged(nameof(IsBackgroundMode)); OnPropertyChanged(nameof(IsImageBackground)); OnPropertyChanged(nameof(IsGradientBackground)); }
    partial void OnSelectedBackgroundTypeChanged(SlideshowBackgroundType value) { OnPropertyChanged(nameof(IsImageBackground)); OnPropertyChanged(nameof(IsGradientBackground)); }
    partial void OnSelectedSortModeChanged(SlideshowSortMode value) => SortImages();
    partial void OnImageDurationChanged(double value) => OnPropertyChanged(nameof(ImageCountText));
    partial void OnOutputFileChanged(string value) { OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); OpenOutputCommand.NotifyCanExecuteChanged(); }
    partial void OnIsRunningChanged(bool value) { OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); ClearCommand.NotifyCanExecuteChanged(); }
}
