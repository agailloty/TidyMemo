using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyMemo.Models;
using TidyMemo.Services;

namespace TidyMemo.ViewModels;

public partial class VideoCompressorViewModel : ViewModelBase
{
    private readonly VideoCompressorService _compressorService;
    private readonly IDialogService _dialogService;
    private readonly SettingsViewModel _settings;
    private CancellationTokenSource? _cts;

    // ── Observable properties ────────────────────────────────────────────────

    [ObservableProperty]
    private VideoCompressionPreset _selectedPreset = null!;

    [ObservableProperty]
    private VideoCodecOption _selectedVideoCodec = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdvancedCompressionMode))]
    private bool _useAdvancedCompressionSettings;

    [ObservableProperty]
    private int _customCrf = 30;

    [ObservableProperty]
    private string _customFfmpegPreset = "medium";

    [ObservableProperty]
    private bool _usePostfix = true;

    [ObservableProperty]
    private string _postfix = "V";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _processedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _includeSubfolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompressionMode))]
    [NotifyPropertyChangedFor(nameof(IsAdvancedCompressionMode))]
    [NotifyPropertyChangedFor(nameof(IsSpeedMode))]
    [NotifyPropertyChangedFor(nameof(IsConvertMode))]
    [NotifyPropertyChangedFor(nameof(IsGifMode))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    private VideoProcessingOperationOption _selectedOperation = null!;

    [ObservableProperty]
    private double _speedMultiplier = 2;

    [ObservableProperty]
    private string _outputFormat = "mp4";

    [ObservableProperty]
    private int _gifWidth = 640;

    [ObservableProperty]
    private int _gifFps = 12;

    // ── Collections ──────────────────────────────────────────────────────────

    public ObservableCollection<DirectoryInfo> Folders { get; } = new();
    public ObservableCollection<VideoCompressionJobViewModel> Jobs { get; } = new();
    public IReadOnlyList<VideoCompressionPreset> Presets { get; }
    public IReadOnlyList<VideoCodecOption> VideoCodecs { get; }
    public IReadOnlyList<string> FfmpegPresets { get; } = new[]
    {
        "ultrafast", "superfast", "veryfast", "faster", "fast", "medium",
        "slow", "slower", "veryslow", "placebo"
    };
    public IReadOnlyList<VideoProcessingOperationOption> Operations { get; }
    public IReadOnlyList<double> SpeedMultipliers { get; } = new[] { 1.25, 1.5, 2, 3, 4 };
    public IReadOnlyList<string> OutputFormats { get; } = new[] { "mp4", "mkv", "mov", "webm", "avi" };
    public IReadOnlyList<int> GifWidths { get; } = new[] { 320, 480, 640, 800 };
    public IReadOnlyList<int> GifFrameRates { get; } = new[] { 8, 10, 12, 15, 20 };

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand AddFolderCommand { get; }
    public ICommand AddFileCommand { get; }
    public ICommand RemoveFolderCommand { get; }
    public ICommand RemoveJobCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand OpenFfmpegSettingsCommand { get; }

    // ── Computed ─────────────────────────────────────────────────────────────

    public bool CanStart => Jobs.Count > 0 && !IsRunning && IsFfmpegConfigured;
    public bool CanClear => !IsRunning && (Jobs.Count > 0 || Folders.Count > 0 || !string.IsNullOrEmpty(StatusMessage));
    public bool IsFfmpegConfigured => _settings.IsFfmpegConfigured;
    public SettingsViewModel Settings => _settings;
    public bool IsCompressionMode => SelectedOperation?.Operation == VideoProcessingOperation.Compress;
    public bool IsAdvancedCompressionMode => IsCompressionMode && UseAdvancedCompressionSettings;
    public bool IsSpeedMode => SelectedOperation?.Operation is
        VideoProcessingOperation.SpeedUp or VideoProcessingOperation.SpeedUpAndExportGif;
    public bool IsConvertMode => SelectedOperation?.Operation == VideoProcessingOperation.Convert;
    public bool IsGifMode => SelectedOperation?.Operation is
        VideoProcessingOperation.ExportGif or VideoProcessingOperation.SpeedUpAndExportGif;
    public string ActionButtonText => SelectedOperation?.Operation switch
    {
        VideoProcessingOperation.SpeedUp => "▶  SPEED UP",
        VideoProcessingOperation.Convert => "▶  CONVERT",
        VideoProcessingOperation.ExportGif => "▶  EXPORT GIF",
        VideoProcessingOperation.SpeedUpAndExportGif => "▶  SPEED UP + GIF",
        _ => "▶  COMPRESS"
    };

    // ── Constructor ──────────────────────────────────────────────────────────

    public VideoCompressorViewModel(
        VideoCompressorService compressorService,
        IDialogService dialogService,
        SettingsViewModel settings,
        Action openFfmpegSettings)
    {
        _compressorService = compressorService;
        _dialogService = dialogService;
        _settings = settings;

        Presets = BuildPresets();
        _selectedPreset = Presets[0]; // FFmpeg encoder defaults
        VideoCodecs = BuildVideoCodecs();
        _selectedVideoCodec = VideoCodecs[0]; // Preserve every existing preset's behavior
        Operations = BuildOperations();
        _selectedOperation = Operations[0];

        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync);
        AddFileCommand = new AsyncRelayCommand(AddFileAsync);
        RemoveFolderCommand = new RelayCommand<DirectoryInfo>(RemoveFolder);
        RemoveJobCommand = new RelayCommand<VideoCompressionJobViewModel>(job => { if (job is not null) Jobs.Remove(job); });
        StartCommand = new AsyncRelayCommand(StartCompressionAsync, () => CanStart);
        CancelCommand = new RelayCommand(CancelCompression, () => IsRunning);
        ClearCommand = new RelayCommand(Clear, () => CanClear);
        OpenFfmpegSettingsCommand = new RelayCommand(openFfmpegSettings);

        Folders.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanClear));
            ((AsyncRelayCommand)StartCommand).NotifyCanExecuteChanged();
            ((RelayCommand)ClearCommand).NotifyCanExecuteChanged();
        };

        Jobs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanClear));
            ((AsyncRelayCommand)StartCommand).NotifyCanExecuteChanged();
            ((RelayCommand)ClearCommand).NotifyCanExecuteChanged();
        };

        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    // ── Command implementations ───────────────────────────────────────────────

    private async Task AddFolderAsync()
    {
        var path = await _dialogService.ShowFolderBrowserDialogAsync();
        if (!string.IsNullOrEmpty(path))
        {
            var dir = new DirectoryInfo(path);
            if (Folders.All(f => f.FullName != dir.FullName))
            {
                Folders.Add(dir);
                ScanAndAddJobs(dir);
            }
        }
    }

    private async Task AddFileAsync()
    {
        var path = await _dialogService.ShowFilePickerAsync(
            "Select a video",
            new[] { "*.mp4", "*.mov", "*.avi", "*.mkv", "*.wmv", "*.flv", "*.m4v", "*.webm" });

        if (!string.IsNullOrWhiteSpace(path) && _compressorService.IsSupportedVideo(path))
            AddJob(path);
    }

    private void RemoveFolder(DirectoryInfo? folder)
    {
        if (folder is null) return;
        Folders.Remove(folder);
        var prefix = folder.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        var toRemove = Jobs
            .Where(j => j.InputPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var job in toRemove)
            Jobs.Remove(job);
    }

    private void ScanAndAddJobs(DirectoryInfo folder)
    {
        var files = _compressorService.GetVideoFiles(new[] { folder.FullName }, IncludeSubfolders);
        foreach (var file in files)
            AddJob(file);
    }

    private void AddJob(string file)
    {
        if (Jobs.Any(j => j.InputPath.Equals(file, StringComparison.OrdinalIgnoreCase)))
            return;

        var info = new FileInfo(file);
        Jobs.Add(new VideoCompressionJobViewModel
        {
            InputFilename = info.Name,
            InputPath = file,
            Presets = Presets,
            SelectedPreset = SelectedPreset
        });
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not nameof(SettingsViewModel.FfmpegPath)) return;
        OnPropertyChanged(nameof(IsFfmpegConfigured));
        OnPropertyChanged(nameof(CanStart));
        ((AsyncRelayCommand)StartCommand).NotifyCanExecuteChanged();
    }

    private async Task StartCompressionAsync()
    {
        if (string.IsNullOrEmpty(_settings.FfmpegPath) || Jobs.Count == 0) return;

        _cts = new CancellationTokenSource();
        IsRunning = true;

        var outputSubfolder = string.IsNullOrWhiteSpace(_settings.OutputSubfolderName)
            ? "Final"
            : _settings.OutputSubfolderName;

        // Compute output paths and reset statuses
        foreach (var job in Jobs)
        {
            var baseName = Path.GetFileNameWithoutExtension(job.InputPath);
            var ext = GetOutputExtension(job.InputPath);
            var suffix = UsePostfix ? GetOutputSuffix() : string.Empty;
            var outName = $"{baseName}{suffix}{ext}";
            if (string.Equals(outName, job.InputFilename, StringComparison.OrdinalIgnoreCase))
                outName = $"{baseName}_output{ext}";
            var inputDir = new FileInfo(job.InputPath).DirectoryName ?? string.Empty;
            job.OutputFilename = outName;
            job.OutputPath = Path.Combine(inputDir, outputSubfolder, outName);
            job.InputSize = 0;
            job.OutputSize = 0;
            job.ErrorMessage = null;
            job.Status = VideoCompressionJobStatus.Queued;
        }

        TotalCount = Jobs.Count;
        ProcessedCount = 0;
        StatusMessage = $"0 / {TotalCount} files processed";

        // Process each job sequentially
        for (var i = 0; i < Jobs.Count; i++)
        {
            if (_cts.IsCancellationRequested) break;

            var job = Jobs[i];
            job.Status = VideoCompressionJobStatus.Processing;
            StatusMessage = $"Processing {job.InputFilename}...";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);

                var progressReporter = new Progress<string>(line =>
                    StatusMessage = $"[{job.InputFilename}] {line}");

                var result = await _compressorService.ProcessAsync(
                    job.InputPath,
                    job.OutputPath,
                    new VideoProcessingOptions
                    {
                        Operation = SelectedOperation.Operation,
                        CompressionPreset = GetCompressionPreset(job),
                        SpeedMultiplier = Math.Clamp(SpeedMultiplier, 1, 100),
                        OutputFormat = OutputFormat,
                        GifWidth = GifWidth,
                        GifFps = GifFps
                    },
                    _settings.FfmpegPath,
                    progressReporter,
                    _cts.Token);

                if (result.Success)
                {
                    job.InputSize = result.InputSize;
                    job.OutputSize = result.OutputSize;
                    job.Status = VideoCompressionJobStatus.Done;
                }
                else
                {
                    job.ErrorMessage = result.ErrorMessage;
                    job.Status = VideoCompressionJobStatus.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = VideoCompressionJobStatus.Cancelled;
                for (var j = i + 1; j < Jobs.Count; j++)
                    Jobs[j].Status = VideoCompressionJobStatus.Cancelled;
                break;
            }

            ProcessedCount++;
            StatusMessage = $"{ProcessedCount} / {TotalCount} files processed";
        }

        var doneCount = Jobs.Count(j => j.Status == VideoCompressionJobStatus.Done);
        var failedCount = Jobs.Count(j => j.Status == VideoCompressionJobStatus.Failed);
        StatusMessage = failedCount > 0
            ? $"Done — {doneCount} processed, {failedCount} error(s) out of {TotalCount}"
            : $"Done — {doneCount} file(s) processed out of {TotalCount}";

        IsRunning = false;
        _cts.Dispose();
        _cts = null;
    }

    private void CancelCompression() => _cts?.Cancel();

    private void Clear()
    {
        Folders.Clear();
        Jobs.Clear();
        SelectedOperation = Operations[0];
        SelectedPreset = Presets[0];
        SelectedVideoCodec = VideoCodecs[0];
        UseAdvancedCompressionSettings = false;
        CustomCrf = 30;
        CustomFfmpegPreset = "medium";
        UsePostfix = true;
        Postfix = "V";
        IncludeSubfolders = false;
        SpeedMultiplier = 2;
        OutputFormat = "mp4";
        GifWidth = 640;
        GifFps = 12;
        ProcessedCount = 0;
        TotalCount = 0;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(CanClear));
        ((RelayCommand)ClearCommand).NotifyCanExecuteChanged();
    }

    private string GetOutputExtension(string inputPath) => SelectedOperation.Operation switch
    {
        VideoProcessingOperation.ExportGif or VideoProcessingOperation.SpeedUpAndExportGif => ".gif",
        VideoProcessingOperation.SpeedUp => ".mp4",
        VideoProcessingOperation.Convert => $".{OutputFormat.ToLowerInvariant()}",
        _ => Path.GetExtension(inputPath)
    };

    private string GetOutputSuffix()
    {
        return Postfix;
    }

    // ── Partial hooks ─────────────────────────────────────────────────────────

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        ((AsyncRelayCommand)StartCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CancelCommand).NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanClear));
        ((RelayCommand)ClearCommand).NotifyCanExecuteChanged();
        foreach (var job in Jobs)
            job.IsPresetEditable = !value;
    }

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        var standaloneJobs = Jobs
            .Where(job => Folders.All(folder => !IsInsideFolder(job.InputPath, folder.FullName)))
            .ToList();
        Jobs.Clear();
        foreach (var job in standaloneJobs)
            Jobs.Add(job);
        foreach (var folder in Folders)
            ScanAndAddJobs(folder);
    }

    partial void OnSelectedOperationChanged(VideoProcessingOperationOption value)
    {
        Postfix = value.Operation switch
        {
            VideoProcessingOperation.Compress => "V",
            VideoProcessingOperation.SpeedUp => $"_x{SpeedMultiplier:0.##}",
            VideoProcessingOperation.Convert => "_converted",
            VideoProcessingOperation.ExportGif => "_gif",
            VideoProcessingOperation.SpeedUpAndExportGif => $"_x{SpeedMultiplier:0.##}_gif",
            _ => "_output"
        };
    }

    partial void OnSpeedMultiplierChanged(double value)
    {
        if (SelectedOperation?.Operation == VideoProcessingOperation.SpeedUp)
            Postfix = $"_x{value:0.##}";
        else if (SelectedOperation?.Operation == VideoProcessingOperation.SpeedUpAndExportGif)
            Postfix = $"_x{value:0.##}_gif";
    }

    private static bool IsInsideFolder(string filePath, string folderPath)
    {
        var prefix = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    // ── Preset definitions ────────────────────────────────────────────────────

    private static IReadOnlyList<VideoCompressionPreset> BuildPresets() =>
        new List<VideoCompressionPreset>
        {
            new() { Name = "Auto (default)",      Description = "FFmpeg defaults — keeps source format, resolution, audio and metadata", Crf = 0, FfmpegPreset = "medium", UseEncoderDefaults = true },
            new() { Name = "Compact",             Description = "CRF 30 · medium — efficient compression and reduced size",             Crf = 30, FfmpegPreset = "medium"    },
            new() { Name = "Very high quality", Description = "CRF 18 · slow — near-original quality, large files",                Crf = 18, FfmpegPreset = "slow"      },
            new() { Name = "High quality",         Description = "CRF 22 · medium — excellent quality, good compression",               Crf = 22, FfmpegPreset = "medium"    },
            new() { Name = "Balanced",             Description = "CRF 27 · veryfast — good quality/size trade-off (recommended)",       Crf = 27, FfmpegPreset = "veryfast"  },
            new() { Name = "Reduced size",         Description = "CRF 30 · veryfast — lighter files, slight quality loss",              Crf = 30, FfmpegPreset = "veryfast"  },
            new() { Name = "Minimum size",         Description = "CRF 36 · ultrafast — maximum compression, reduced quality",           Crf = 36, FfmpegPreset = "ultrafast" },
            new() { Name = "Full HD (1080p)",      Description = "CRF 23 · veryfast — scales to 1920×… (source ≥ 1080p)",              Crf = 23, FfmpegPreset = "veryfast",  ScaleFilter = "1920:-2" },
            new() { Name = "HD (720p)",            Description = "CRF 23 · veryfast — scales to 1280×… (source ≥ 720p)",               Crf = 23, FfmpegPreset = "veryfast",  ScaleFilter = "1280:-2" },
            new() { Name = "Social media",         Description = "CRF 28 · veryfast — 720p optimised for online sharing",               Crf = 28, FfmpegPreset = "veryfast",  ScaleFilter = "1280:-2" },
        }.AsReadOnly();

    private static IReadOnlyList<VideoCodecOption> BuildVideoCodecs() =>
        new List<VideoCodecOption>
        {
            new() { Name = "According to preset (default)", Description = "Preserves the current behavior of existing presets", FfmpegEncoder = null },
            new() { Name = "H.264 (AVC)", Description = "Broad compatibility", FfmpegEncoder = "libx264" },
            new() { Name = "H.265 (HEVC)", Description = "Smaller files, slower encoding and newer-device compatibility", FfmpegEncoder = "libx265" }
        }.AsReadOnly();

    private VideoCompressionPreset GetCompressionPreset(VideoCompressionJobViewModel job)
    {
        if (!UseAdvancedCompressionSettings)
        {
            return new VideoCompressionPreset
            {
                Name = job.SelectedPreset.Name,
                Description = job.SelectedPreset.Description,
                Crf = job.SelectedPreset.Crf,
                FfmpegPreset = job.SelectedPreset.FfmpegPreset,
                UseEncoderDefaults = job.SelectedPreset.UseEncoderDefaults,
                ScaleFilter = job.SelectedPreset.ScaleFilter,
                VideoEncoder = SelectedVideoCodec.FfmpegEncoder
            };
        }

        return new VideoCompressionPreset
        {
            Name = "Custom",
            Description = $"CRF {CustomCrf} · {CustomFfmpegPreset}",
            Crf = Math.Clamp(CustomCrf, 0, 51),
            FfmpegPreset = FfmpegPresets.Contains(CustomFfmpegPreset)
                ? CustomFfmpegPreset
                : "medium",
            VideoEncoder = SelectedVideoCodec.FfmpegEncoder
        };
    }

    private static IReadOnlyList<VideoProcessingOperationOption> BuildOperations() =>
        new List<VideoProcessingOperationOption>
        {
            new() { Operation = VideoProcessingOperation.Compress, Name = "Compress", Description = "Reduce file size while controlling quality." },
            new() { Operation = VideoProcessingOperation.SpeedUp, Name = "Speed up", Description = "Create a faster MP4 video and preserve audio when present." },
            new() { Operation = VideoProcessingOperation.Convert, Name = "Convert format", Description = "Convert to MP4, MKV, MOV, WebM or AVI." },
            new() { Operation = VideoProcessingOperation.ExportGif, Name = "Export as GIF", Description = "Create an optimized animated GIF from an existing video." },
            new() { Operation = VideoProcessingOperation.SpeedUpAndExportGif, Name = "Speed up and export GIF", Description = "Accelerate the source while generating the GIF." }
        }.AsReadOnly();
}
