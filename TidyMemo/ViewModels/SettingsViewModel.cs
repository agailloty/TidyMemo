using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyMemo.Models;
using TidyMemo.Services;

namespace TidyMemo.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly AppSettings _appSettings;
    private readonly FfmpegDownloadService _ffmpegDownloadService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFfmpegConfigured))]
    [NotifyPropertyChangedFor(nameof(IsVideoTabVisible))]
    [NotifyPropertyChangedFor(nameof(FfmpegConfigurationMessage))]
    private string _ffmpegPath = string.Empty;

    [ObservableProperty]
    private string _outputSubfolderName = "Final";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoTabVisible))]
    private bool _isVideoCompressionEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadFfmpegCommand))]
    private bool _isDownloadingFfmpeg;

    [ObservableProperty]
    private double _ffmpegDownloadProgress;

    [ObservableProperty]
    private string _ffmpegDownloadStatus = string.Empty;

    public bool IsFfmpegConfigured =>
        !string.IsNullOrWhiteSpace(FfmpegPath) && File.Exists(FfmpegPath);

    public bool IsVideoTabVisible => IsVideoCompressionEnabled && IsFfmpegConfigured;

    public string FfmpegConfigurationMessage => string.IsNullOrWhiteSpace(FfmpegPath)
        ? "No FFmpeg executable has been configured yet."
        : "The configured FFmpeg executable could not be found. Select another file or download it again.";

    public string DetectedPlatform => _ffmpegDownloadService.PlatformDescription;
    public TransitionMode SlideshowTransitionMode => _appSettings.SlideshowTransitionMode;
    public string SlideshowTransitionId => _appSettings.SlideshowTransitionId;
    public double SlideshowTransitionDuration => _appSettings.SlideshowTransitionDuration;
    public PhotoMotionMode SlideshowMotionMode => _appSettings.SlideshowMotionMode;
    public string SlideshowMotionId => _appSettings.SlideshowMotionId;
    public MotionIntensity SlideshowMotionIntensity => _appSettings.SlideshowMotionIntensity;
    public MotionEasing SlideshowMotionEasing => _appSettings.SlideshowMotionEasing;
    public int SlideshowMotionRandomSeed => _appSettings.SlideshowMotionRandomSeed;

    public SettingsViewModel(
        SettingsService settingsService,
        IDialogService dialogService,
        FfmpegDownloadService ffmpegDownloadService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _appSettings = settingsService.Load();
        _ffmpegDownloadService = ffmpegDownloadService;
        // Assign backing fields directly to avoid triggering Save() during init
        _ffmpegPath = _appSettings.FfmpegPath;
        _outputSubfolderName = _appSettings.OutputSubfolderName;
        _isVideoCompressionEnabled = _appSettings.IsVideoCompressionEnabled;
    }

    private bool CanDownloadFfmpeg() => !IsDownloadingFfmpeg;

    [RelayCommand(CanExecute = nameof(CanDownloadFfmpeg))]
    private async Task DownloadFfmpegAsync()
    {
        IsDownloadingFfmpeg = true;
        FfmpegDownloadProgress = 0;
        FfmpegDownloadStatus = $"Downloading ffmpeg for {DetectedPlatform}...";

        try
        {
            var progress = new Progress<double>(value => FfmpegDownloadProgress = value * 100);
            FfmpegPath = await _ffmpegDownloadService.DownloadAsync(progress);
            FfmpegDownloadStatus = "ffmpeg downloaded and configured successfully.";
        }
        catch (Exception exception)
        {
            FfmpegDownloadStatus = $"Download failed: {exception.Message}";
        }
        finally
        {
            IsDownloadingFfmpeg = false;
        }
    }

    partial void OnFfmpegPathChanged(string value) => PersistSettings();

    partial void OnOutputSubfolderNameChanged(string value) => PersistSettings();

    partial void OnIsVideoCompressionEnabledChanged(bool value) => PersistSettings();

    [RelayCommand]
    private async Task BrowseFfmpegAsync()
    {
        var path = await _dialogService.ShowFilePickerAsync(
            "Sélectionner ffmpeg",
            new[] { "*" });
        if (!string.IsNullOrEmpty(path))
            FfmpegPath = path;
    }

    private void PersistSettings()
    {
        _appSettings.FfmpegPath = FfmpegPath;
        _appSettings.OutputSubfolderName = OutputSubfolderName;
        _appSettings.IsVideoCompressionEnabled = IsVideoCompressionEnabled;
        _settingsService.Save(_appSettings);
    }

    public void SaveSlideshowTransition(TransitionMode mode, string transitionId, double duration)
    {
        _appSettings.SlideshowTransitionMode = mode;
        _appSettings.SlideshowTransitionId = transitionId;
        _appSettings.SlideshowTransitionDuration = duration;
        PersistSettings();
    }

    public void SaveSlideshowMotion(PhotoMotionMode mode, string motionId, MotionIntensity intensity,
        MotionEasing easing, int randomSeed)
    {
        _appSettings.SlideshowMotionMode = mode;
        _appSettings.SlideshowMotionId = motionId;
        _appSettings.SlideshowMotionIntensity = intensity;
        _appSettings.SlideshowMotionEasing = easing;
        _appSettings.SlideshowMotionRandomSeed = randomSeed;
        PersistSettings();
    }
}
