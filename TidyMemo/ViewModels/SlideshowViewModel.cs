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
    private readonly ISlideshowProjectStore _projectStore;
    private readonly List<SlideshowSource> _sources = [];
    private CancellationTokenSource? _cancellation;
    private bool _isApplyingProject;
    private Guid _projectId = Guid.NewGuid();

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
    public IReadOnlyList<TransitionMode> TransitionModes { get; } = Enum.GetValues<TransitionMode>();
    public IReadOnlyList<TransitionDefinition> Transitions { get; } = TransitionCatalog.All;
    public IReadOnlyList<PhotoMotionMode> MotionModes { get; } = Enum.GetValues<PhotoMotionMode>();
    public IReadOnlyList<PhotoMotionDefinition> Motions { get; } = PhotoMotionCatalog.All;
    public IReadOnlyList<MotionIntensity> MotionIntensities { get; } = Enum.GetValues<MotionIntensity>();
    public IReadOnlyList<MotionEasing> MotionEasings { get; } = Enum.GetValues<MotionEasing>();

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
    [ObservableProperty] private bool _includeSubfolders = true;
    [ObservableProperty] private bool _useEnhancedBackgroundProcessing = true;
    [ObservableProperty] private bool _preferImageMagick;
    [ObservableProperty] private string _imageMagickPath = "magick";
    [ObservableProperty] private TransitionMode _selectedTransitionMode;
    [ObservableProperty] private TransitionDefinition _selectedTransition;
    [ObservableProperty] private double _transitionDuration;
    [ObservableProperty] private PhotoMotionMode _selectedMotionMode;
    [ObservableProperty] private PhotoMotionDefinition _selectedMotion;
    [ObservableProperty] private MotionIntensity _selectedMotionIntensity;
    [ObservableProperty] private MotionEasing _selectedMotionEasing;
    [ObservableProperty] private int _motionRandomSeed = 1;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanCreate))] private bool _isRunning;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusMessage = "Add images to begin.";
    [ObservableProperty] private string? _projectPath;
    [ObservableProperty] private string _projectName = "Untitled slideshow";
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _isProjectOpen;

    public bool HasImages => Images.Count > 0;
    public bool IsBackgroundMode => SelectedType == SlideshowType.Background;
    public bool IsImageBackground => IsBackgroundMode && SelectedBackgroundType == SlideshowBackgroundType.Image;
    public bool IsGradientBackground => IsBackgroundMode && SelectedBackgroundType == SlideshowBackgroundType.Gradient;
    public bool CanCreate => HasImages && !IsRunning && _settings.IsFfmpegConfigured && !string.IsNullOrWhiteSpace(OutputFile);
    public bool IsTransitionEnabled => SelectedTransitionMode != TransitionMode.None;
    public bool IsMotionEnabled => SelectedMotionMode != PhotoMotionMode.None;
    public bool IsFixedMotion => SelectedMotionMode == PhotoMotionMode.Preset;
    public string ImageCountText
    {
        get
        {
            var seconds = Images.Count * ImageDuration;
            if (IsTransitionEnabled && Images.Count > 1 && TransitionDuration < ImageDuration)
                seconds -= (Images.Count - 1) * TransitionDuration;
            return $"{Images.Count} image(s) - approximately {TimeSpan.FromSeconds(Math.Max(0, seconds)):hh\\:mm\\:ss}";
        }
    }
    public bool IsFfmpegConfigured => _settings.IsFfmpegConfigured;
    public string ProjectTitle => $"{ProjectName}{(IsDirty ? " *" : string.Empty)}";

    public SlideshowViewModel(SlideshowService service, ExifService exifService, IDialogService dialogs,
        SettingsViewModel settings, Action openSettings, ISlideshowProjectStore? projectStore = null)
    {
        _service = service; _exifService = exifService; _dialogs = dialogs; _settings = settings;
        _projectStore = projectStore ?? new JsonSlideshowProjectStore();
        _selectedResolution = Resolutions[0];
        _selectedTransitionMode = settings.SlideshowTransitionMode;
        _selectedTransition = TransitionCatalog.Find(settings.SlideshowTransitionId) ?? TransitionCatalog.Fade;
        _transitionDuration = settings.SlideshowTransitionDuration is >= 0.1 and <= 3
            ? settings.SlideshowTransitionDuration : 0.8;
        _selectedMotionMode = settings.SlideshowMotionMode;
        _selectedMotion = PhotoMotionCatalog.Find(settings.SlideshowMotionId) ?? PhotoMotionCatalog.None;
        _selectedMotionIntensity = settings.SlideshowMotionIntensity;
        _selectedMotionEasing = settings.SlideshowMotionEasing;
        _motionRandomSeed = settings.SlideshowMotionRandomSeed;
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
        NewProjectCommand = new AsyncRelayCommand(NewProjectAsync, () => !IsRunning);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync, () => !IsRunning);
        SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync, () => !IsRunning);
        SaveProjectAsCommand = new AsyncRelayCommand(SaveProjectAsAsync, () => !IsRunning);
        Images.CollectionChanged += (_, _) => { RefreshCollectionState(); MarkDirty(); };
        _settings.PropertyChanged += SettingsChanged;
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not (nameof(IsDirty) or nameof(ProjectTitle) or nameof(ProjectPath)
                or nameof(ProjectName) or nameof(IsProjectOpen) or nameof(IsRunning) or nameof(ProgressValue) or nameof(StatusMessage)
                or nameof(CanCreate) or nameof(HasImages) or nameof(ImageCountText) or nameof(IsFfmpegConfigured)
                or nameof(IsBackgroundMode) or nameof(IsImageBackground) or nameof(IsGradientBackground)
                or nameof(IsTransitionEnabled) or nameof(IsMotionEnabled) or nameof(IsFixedMotion))) MarkDirty();
        };
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
    public IAsyncRelayCommand NewProjectCommand { get; }
    public IAsyncRelayCommand OpenProjectCommand { get; }
    public IAsyncRelayCommand SaveProjectCommand { get; }
    public IAsyncRelayCommand SaveProjectAsCommand { get; }

    private async Task AddImagesAsync()
    {
        var paths = await _dialogs.ShowFilePickerMultipleAsync("Select slideshow images",
            new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.webp" });
        AddPaths(paths);
    }
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.ShowFolderBrowserDialogAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            var includeSubfolders = IncludeSubfolders;
            var images = _service.GetImages(folder, includeSubfolders);
            if (!includeSubfolders && images.Count == 0)
            {
                includeSubfolders = true;
                IncludeSubfolders = true;
                images = _service.GetImages(folder, includeSubfolders);
            }

            if (_sources.All(source => !source.Path.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                _sources.Add(new SlideshowSource { Path = Path.GetFullPath(folder), IncludeSubfolders = includeSubfolders });
            AddPaths(images);
        }
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
            PreferImageMagick = PreferImageMagick, ImageMagickPath = ImageMagickPath,
            TransitionMode = SelectedTransitionMode, TransitionId = SelectedTransition.Id,
            TransitionDuration = TransitionDuration, MotionMode = SelectedMotionMode,
            MotionId = SelectedMotion.Id, MotionIntensity = SelectedMotionIntensity,
            MotionEasing = SelectedMotionEasing, RandomSeed = MotionRandomSeed
        }, progress, _cancellation.Token);
        StatusMessage = result.Success ? $"Slideshow created: {result.OutputFile}" :
            $"Export failed: {result.ErrorMessage ?? "Slideshow creation failed."}";
        IsRunning = false; _cancellation.Dispose(); _cancellation = null; OpenOutputCommand.NotifyCanExecuteChanged();
    }
    private void Clear()
    {
        ApplyProject(new SlideshowProject(), null);
        IsProjectOpen = false;
        StatusMessage = "Add images to begin.";
    }

    private async Task NewProjectAsync()
    {
        if (IsDirty)
        {
            StatusMessage = "Save the current project before creating a new one.";
            return;
        }

        var path = await _dialogs.ShowSaveFilePickerAsync(
            "Create SlideTune project", "slideshow.slidetune", ".slidetune");
        if (string.IsNullOrWhiteSpace(path)) return;

        var project = new SlideshowProject { Name = Path.GetFileNameWithoutExtension(path) };
        try
        {
            await _projectStore.SaveAsync(path, project);
            ApplyProject(project, path);
            IsProjectOpen = true;
            StatusMessage = $"Project created: {ProjectName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not create project: {ex.Message}";
        }
    }

    private async Task OpenProjectAsync()
    {
        if (IsDirty)
        {
            StatusMessage = "Save the current project before opening another one.";
            return;
        }
        var path = await _dialogs.ShowFilePickerAsync("Open SlideTune project", ["*.slidetune"]);
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var project = await _projectStore.LoadAsync(path);
            ApplyProject(project, path);
            IsProjectOpen = true;
            var missing = Images.Count(item => !File.Exists(item.Path));
            StatusMessage = missing == 0
                ? $"Project opened: {ProjectName}"
                : $"Project opened with {missing} missing image(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open project: {ex.Message}";
        }
    }

    private async Task SaveProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
        {
            await SaveProjectAsAsync();
            return;
        }
        await SaveToAsync(ProjectPath);
    }

    private async Task SaveProjectAsAsync()
    {
        var suggestedName = SanitizeFileName(ProjectName) + ".slidetune";
        var path = await _dialogs.ShowSaveFilePickerAsync("Save SlideTune project", suggestedName, ".slidetune");
        if (!string.IsNullOrWhiteSpace(path)) await SaveToAsync(path);
    }

    private async Task SaveToAsync(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ProjectPath) && ProjectName == "Untitled slideshow")
                ProjectName = Path.GetFileNameWithoutExtension(path);
            await _projectStore.SaveAsync(path, BuildProject(path));
            ProjectPath = Path.GetFullPath(path);
            ProjectName = Path.GetFileNameWithoutExtension(path);
            IsDirty = false;
            StatusMessage = $"Project saved: {ProjectName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save project: {ex.Message}";
        }
    }

    private SlideshowProject BuildProject(string path) => new()
    {
        Id = _projectId,
        Name = string.IsNullOrWhiteSpace(ProjectName) ? Path.GetFileNameWithoutExtension(path) : ProjectName,
        Sources = _sources.Select(source => new SlideshowSource
        {
            Path = SlideshowProjectPaths.ToStoredPath(source.Path, path),
            IncludeSubfolders = source.IncludeSubfolders
        }).ToList(),
        Slides = Images.Select(item => new SlideshowSlide
        {
            Id = item.Id, Path = SlideshowProjectPaths.ToStoredPath(item.Path, path)
        }).ToList(),
        Presentation = new SlideshowPresentationSettings
        {
            Width = SelectedResolution.Width, Height = SelectedResolution.Height, ImageDuration = ImageDuration,
            SortMode = SelectedSortMode, IncludeSubfolders = IncludeSubfolders,
            Type = SelectedType, BackgroundType = SelectedBackgroundType, BackgroundColor = BackgroundColor,
            GradientEndColor = GradientEndColor, GradientDirection = SelectedGradientDirection,
            BackgroundImage = SlideshowProjectPaths.ToStoredPath(BackgroundImage, path), ImageScaling = ImageScaling,
            EnableBorder = EnableBorder, BorderWidth = BorderWidth, BorderColor = BorderColor,
            EnableShadow = EnableShadow, ShadowOffsetX = ShadowOffsetX, ShadowOffsetY = ShadowOffsetY,
            ShadowBlur = ShadowBlur, ShadowOpacity = ShadowOpacity,
            UseEnhancedBackgroundProcessing = UseEnhancedBackgroundProcessing,
            PreferImageMagick = PreferImageMagick, ImageMagickPath = ImageMagickPath,
            TransitionMode = SelectedTransitionMode, TransitionId = SelectedTransition.Id,
            TransitionDuration = TransitionDuration, MotionMode = SelectedMotionMode,
            MotionId = SelectedMotion.Id, MotionIntensity = SelectedMotionIntensity,
            MotionEasing = SelectedMotionEasing, RandomSeed = MotionRandomSeed
        },
        Audio = new SlideshowAudioSettings
        {
            Path = SlideshowProjectPaths.ToStoredPath(AudioFile, path), Volume = Volume
        },
        Export = new SlideshowExportSettings
        {
            OutputFile = SlideshowProjectPaths.ToStoredPath(OutputFile, path), FrameRate = FrameRate,
            Quality = Quality, EncoderPreset = EncoderPreset
        }
    };

    private void ApplyProject(SlideshowProject project, string? path)
    {
        _isApplyingProject = true;
        try
        {
            _projectId = project.Id;
            Images.Clear();
            _sources.Clear();
            SelectedSortMode = project.Presentation.SortMode;
            IncludeSubfolders = project.Presentation.IncludeSubfolders;
            foreach (var source in project.Sources)
            {
                var absolute = path is null ? source.Path : SlideshowProjectPaths.ToAbsolutePath(source.Path, path);
                if (!string.IsNullOrWhiteSpace(absolute))
                    _sources.Add(new SlideshowSource { Path = absolute, IncludeSubfolders = source.IncludeSubfolders });
            }
            foreach (var slide in project.Slides)
            {
                var absolute = path is null ? slide.Path : SlideshowProjectPaths.ToAbsolutePath(slide.Path, path);
                if (!string.IsNullOrWhiteSpace(absolute)) Images.Add(new SlideshowItemViewModel(absolute, slide.Id));
            }

            var presentation = project.Presentation;
            SelectedResolution = Resolutions.FirstOrDefault(r => r.Width == presentation.Width && r.Height == presentation.Height) ?? Resolutions[0];
            ImageDuration = presentation.ImageDuration; SelectedType = presentation.Type;
            SelectedBackgroundType = presentation.BackgroundType; BackgroundColor = presentation.BackgroundColor;
            GradientEndColor = presentation.GradientEndColor; SelectedGradientDirection = presentation.GradientDirection;
            BackgroundImage = path is null ? presentation.BackgroundImage : SlideshowProjectPaths.ToAbsolutePath(presentation.BackgroundImage, path);
            ImageScaling = presentation.ImageScaling; EnableBorder = presentation.EnableBorder;
            BorderWidth = presentation.BorderWidth; BorderColor = presentation.BorderColor;
            EnableShadow = presentation.EnableShadow; ShadowOffsetX = presentation.ShadowOffsetX;
            ShadowOffsetY = presentation.ShadowOffsetY; ShadowBlur = presentation.ShadowBlur;
            ShadowOpacity = presentation.ShadowOpacity; UseEnhancedBackgroundProcessing = presentation.UseEnhancedBackgroundProcessing;
            PreferImageMagick = presentation.PreferImageMagick; ImageMagickPath = presentation.ImageMagickPath;
            SelectedTransitionMode = presentation.TransitionMode;
            SelectedTransition = TransitionCatalog.Find(presentation.TransitionId) ?? TransitionCatalog.Fade;
            TransitionDuration = presentation.TransitionDuration;
            SelectedMotionMode = presentation.MotionMode;
            SelectedMotion = PhotoMotionCatalog.Find(presentation.MotionId) ?? PhotoMotionCatalog.None;
            SelectedMotionIntensity = presentation.MotionIntensity;
            SelectedMotionEasing = presentation.MotionEasing;
            MotionRandomSeed = presentation.RandomSeed;
            AudioFile = path is null ? project.Audio.Path : SlideshowProjectPaths.ToAbsolutePath(project.Audio.Path, path);
            Volume = project.Audio.Volume; FrameRate = project.Export.FrameRate; Quality = project.Export.Quality;
            EncoderPreset = project.Export.EncoderPreset;
            OutputFile = path is null ? project.Export.OutputFile ?? string.Empty : SlideshowProjectPaths.ToAbsolutePath(project.Export.OutputFile, path) ?? string.Empty;
            ProjectPath = path is null ? null : Path.GetFullPath(path);
            ProjectName = string.IsNullOrWhiteSpace(project.Name) ? "Untitled slideshow" : project.Name;
            ProgressValue = 0;
        }
        finally
        {
            _isApplyingProject = false;
            IsDirty = false;
            RefreshCollectionState();
        }
    }

    private void MarkDirty()
    {
        if (!_isApplyingProject) IsDirty = true;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars()) value = value.Replace(character, '_');
        return string.IsNullOrWhiteSpace(value) ? "slideshow" : value;
    }
    private void OpenOutput() { if (File.Exists(OutputFile)) Process.Start(new ProcessStartInfo { FileName = OutputFile, UseShellExecute = true }); }
    private void Reindex() { for (var i = 0; i < Images.Count; i++) Images[i].Position = i + 1; OnPropertyChanged(nameof(ImageCountText)); }
    private void RefreshCollectionState() { Reindex(); OnPropertyChanged(nameof(HasImages)); OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); ClearCommand.NotifyCanExecuteChanged(); }
    private void SettingsChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName != nameof(SettingsViewModel.FfmpegPath)) return; OnPropertyChanged(nameof(IsFfmpegConfigured)); OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedTypeChanged(SlideshowType value) { OnPropertyChanged(nameof(IsBackgroundMode)); OnPropertyChanged(nameof(IsImageBackground)); OnPropertyChanged(nameof(IsGradientBackground)); }
    partial void OnSelectedBackgroundTypeChanged(SlideshowBackgroundType value) { OnPropertyChanged(nameof(IsImageBackground)); OnPropertyChanged(nameof(IsGradientBackground)); }
    partial void OnSelectedSortModeChanged(SlideshowSortMode value) => SortImages();
    partial void OnImageDurationChanged(double value) => OnPropertyChanged(nameof(ImageCountText));
    partial void OnSelectedTransitionModeChanged(TransitionMode value)
    {
        OnPropertyChanged(nameof(IsTransitionEnabled));
        OnPropertyChanged(nameof(ImageCountText));
        PersistTransitionSettings();
    }
    partial void OnSelectedTransitionChanged(TransitionDefinition value) => PersistTransitionSettings();
    partial void OnTransitionDurationChanged(double value)
    {
        OnPropertyChanged(nameof(ImageCountText));
        PersistTransitionSettings();
    }
    private void PersistTransitionSettings()
    {
        if (SelectedTransition is not null)
            _settings.SaveSlideshowTransition(SelectedTransitionMode, SelectedTransition.Id, TransitionDuration);
    }
    partial void OnSelectedMotionModeChanged(PhotoMotionMode value)
    {
        OnPropertyChanged(nameof(IsMotionEnabled));
        OnPropertyChanged(nameof(IsFixedMotion));
        PersistMotionSettings();
    }
    partial void OnSelectedMotionChanged(PhotoMotionDefinition value) => PersistMotionSettings();
    partial void OnSelectedMotionIntensityChanged(MotionIntensity value) => PersistMotionSettings();
    partial void OnSelectedMotionEasingChanged(MotionEasing value) => PersistMotionSettings();
    partial void OnMotionRandomSeedChanged(int value) => PersistMotionSettings();
    private void PersistMotionSettings()
    {
        if (SelectedMotion is not null)
            _settings.SaveSlideshowMotion(SelectedMotionMode, SelectedMotion.Id, SelectedMotionIntensity,
                SelectedMotionEasing, MotionRandomSeed);
    }
    partial void OnOutputFileChanged(string value) { OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); OpenOutputCommand.NotifyCanExecuteChanged(); }
    partial void OnIsRunningChanged(bool value) { OnPropertyChanged(nameof(CanCreate)); CreateCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); ClearCommand.NotifyCanExecuteChanged(); NewProjectCommand.NotifyCanExecuteChanged(); OpenProjectCommand.NotifyCanExecuteChanged(); SaveProjectCommand.NotifyCanExecuteChanged(); SaveProjectAsCommand.NotifyCanExecuteChanged(); }
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(ProjectTitle));
    partial void OnProjectNameChanged(string value) => OnPropertyChanged(nameof(ProjectTitle));
}
