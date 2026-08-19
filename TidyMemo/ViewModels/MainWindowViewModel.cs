using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TidyMemo.Common;
using TidyMemo.Models;
using TidyMemo.Services;

namespace TidyMemo.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly FolderService _folderService;
    private readonly RenamerService _renamerService;
    private bool _isSelectExifVisible;
    private ObservableCollection<PreviewModel> _renamePreviews = new();
    private RenamerPatternModel _selectedDateRenamerPattern = null!;
    private int _totalImagesCount;
    private bool _isRenameEnabled;
    private bool _hasImages;
    private RenamerDateType _selectedRenamerDateType = null!;
    private string _customFormat = string.Empty;
    private bool _isCustomSelected;
    private ExifService _exifService;
    private bool _isCustomDateFormat;
    private string _customDateFormat = string.Empty;
    private bool _includeSubfolders;
    private int _selectedModuleIndex;
    private bool _organizeIntoFolders;
    private string _folderPattern = "%year%/%month%";

    public MainWindowViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        _folderService = new FolderService();
        AddFolderCommand = new AsyncRelayCommand(AddFolder);
        PathFolders = new ObservableCollection<DirectoryInfo>();
        RemoveFolderCommand = new AsyncRelayCommand<DirectoryInfo>(RemoveFolder);
        SelectExifMetadataCommand = new AsyncRelayCommand(OpenExifMetadataDialog);
        ValidateCustomFormatCommand = new AsyncRelayCommand(UpdateImageCount);
        _renamerService = new RenamerService();
        BuiltInRenamerPatterns = _renamerService.GetBuiltInRenamerPatterns().AsReadOnly();
        SelectedDateRenamerPattern = BuiltInRenamerPatterns.First();
        RenameCommand = new AsyncRelayCommand(RenameImages);
        ClearImagesCommand = new RelayCommand(ClearImages);
        ShowExifExplorerCommand = new AsyncRelayCommand(OpenExifMetadataDialog);
        ShowHomeCommand = new RelayCommand(() => SelectedModuleIndex = 0);
        ShowImagesCommand = new RelayCommand(() => SelectedModuleIndex = 1);
        ShowVideosCommand = new RelayCommand(() => SelectedModuleIndex = 2);
        ShowSettingsCommand = new RelayCommand(() => SelectedModuleIndex = 3);
        _exifService = new ExifService();
        RenamerDateTypes = new ObservableCollection<RenamerDateType>
        {
            new("Creation date", DateType.Creation),
            new("Photo taken date", DateType.PhotoTaken),
            new("Modification date", DateType.Modification),
        };
        SelectedRenamerDateType = RenamerDateTypes[1];

        var settingsService = new SettingsService();
        Settings = new SettingsViewModel(settingsService, dialogService, new FfmpegDownloadService());
        VideoCompressor = new VideoCompressorViewModel(
            new VideoCompressorService(), dialogService, Settings,
            () => SelectedModuleIndex = 3);
    }

    #region Commands
    public ICommand RemoveFolderCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand ValidateCustomFormatCommand { get; }
    
    public ICommand SelectExifMetadataCommand { get; }
    public ICommand? OKCommand { get; }
    
    public ICommand ShowExifExplorerCommand { get; }

    public ICommand RenameCommand { get; }
    public ICommand ClearImagesCommand { get; }
    public ICommand ShowHomeCommand { get; }
    public ICommand ShowImagesCommand { get; }
    public ICommand ShowVideosCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    #endregion

    #region Properties
    public SettingsViewModel Settings { get; }
    public VideoCompressorViewModel VideoCompressor { get; }

    public int SelectedModuleIndex
    {
        get => _selectedModuleIndex;
        set => SetProperty(ref _selectedModuleIndex, value);
    }

    public ObservableCollection<DirectoryInfo> PathFolders { get; set; }

    public int TotalImagesCount
    {
        get => _totalImagesCount;
        set 
        {
            if (SetProperty(ref _totalImagesCount, value))
            {
                HasImages = value > 0;
            } 
        }
    }

    public bool HasImages
    {
        get => _hasImages;
        set => SetProperty(ref _hasImages, value);
    }
    public ReadOnlyCollection<RenamerPatternModel> BuiltInRenamerPatterns { get; }

    public RenamerPatternModel SelectedDateRenamerPattern
    {
        get => _selectedDateRenamerPattern;
        set
        {
            if (SetProperty(ref _selectedDateRenamerPattern, value))
            {
                IsSelectExifVisible = value?.Name == "Custom";
                IsCustomSelected = IsSelectExifVisible;
                IsCustomDateFormat = value?.Name == "Custom Date Time";
            }
            Task.Run(UpdateImageCount); 
        }
    }

    public bool IsCustomSelected
    {
        get => _isCustomSelected;
        set => SetProperty(ref _isCustomSelected, value);
    }

    public bool IsSelectExifVisible
    {
        get => _isSelectExifVisible;
        set => SetProperty(ref _isSelectExifVisible, value);
    }

    public ObservableCollection<PreviewModel> RenamePreviews
    {
        get => _renamePreviews;
        set => SetProperty(ref _renamePreviews, value);
    }
    
    public bool IsRenameEnabled
    {
        get => _isRenameEnabled;
        set => SetProperty(ref _isRenameEnabled, value);
    }
    
    public ObservableCollection<RenamerDateType> RenamerDateTypes { get; set; }

    public RenamerDateType SelectedRenamerDateType
    {
        get => _selectedRenamerDateType;
        set
        {
            if (SetProperty(ref _selectedRenamerDateType, value))
            {
                SelectedDateRenamerPattern = _selectedDateRenamerPattern;
            }
        }
    }

    public string CustomFormat
    {
        get => _customFormat;
        set => SetProperty(ref _customFormat, value);
    }
    
    public string CustomDateFormat
    {
        get => _customDateFormat;
        set => SetProperty(ref _customDateFormat, value);
    }

    public bool IsCustomDateFormat
    {
        get => _isCustomDateFormat;
        set => SetProperty(ref _isCustomDateFormat, value);
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set
        {
            if (SetProperty(ref _includeSubfolders, value))
                Task.Run(UpdateImageCount);
        }
    }

    #endregion
    
    #region Private methods
    private async Task RemoveFolder(DirectoryInfo? folder)
    {
        if (folder == null || !PathFolders.Contains(folder)) return;
        PathFolders.Remove(folder);
        await UpdateImageCount();
    }


    private async Task AddFolder()
    {
        var selectedPath = await _dialogService.ShowFolderBrowserDialogAsync();
        if (selectedPath != null)
        {
            var directory = new DirectoryInfo(selectedPath);
            if (PathFolders.All(folder => folder.FullName != directory.FullName))
            {
                PathFolders.Add(new DirectoryInfo(selectedPath));
                await UpdateImageCount();
            }
        }
    }

    private async Task<PreviewModel[]> GetImagePreviews()
    {
        var dateRenamerPattern = SelectedDateRenamerPattern;
        if (SelectedDateRenamerPattern.Name == "Custom" && !string.IsNullOrEmpty(CustomFormat))
        {
            dateRenamerPattern = new RenamerPatternModel
            {
                Name = CustomFormat,
                Description = "Custom format",
            };
        }
        
        if (SelectedDateRenamerPattern.Name == "Custom Date Time" && !string.IsNullOrEmpty(CustomDateFormat))
        {
            dateRenamerPattern = new RenamerPatternModel
            {
                Name = CustomDateFormat,
                Description = "Custom format",
                IsCustomDateFormat = true,  
            };
        }
        
        var results = new List<PreviewModel>();
        foreach (var folder in PathFolders)
        {
            var files = _folderService.GetImageFiles(folder.FullName, IncludeSubfolders);
            var organization = new PhotoOrganizationOptions
            {
                Enabled = OrganizeIntoFolders,
                RootFolder = folder.FullName,
                FolderPattern = FolderPattern
            };
            results.AddRange(await _renamerService.GetRenamePreviews(files, dateRenamerPattern,
                SelectedRenamerDateType.DateType, IsCustomSelected, organization));
        }
        return results.ToArray();
    }

    private async Task OpenExifMetadataDialog()
    {
        if (!PathFolders.Any()) return;
        var path = PathFolders.First().FullName;
        var files = _folderService.GetImageFiles(path, IncludeSubfolders);
        if (files.Any())
        { 
              var exifTags = _exifService.RetrieveExifTags(files);
              var tagItems = new ObservableCollection<ExifTokenItemViewModel>();
                foreach (var tag in exifTags)
                {
                    var tagItem = new ExifTokenItemViewModel
                    {
                        TagName = tag,
                        TagKey = _exifService.TokenizeExifName(tag),
                        IsSelected = false,
                        IsEnabled = true,
                    };
                    tagItems.Add(tagItem);
                }
            var data = new ExifInput { ExifTags = tagItems };
           var res = await _dialogService.ShowExifMetadataDialogAsync(data);
            if (res != null && res.ClosingResult == ClosingResult.Ok)
            {
                var selectedTags = res.ExifTokens.Select(e => e.TagKey).ToList();
                CustomFormat = string.Join('_', selectedTags);
            }
        }
    }

    private async Task UpdateImageCount()
    {
        IsBusy = true;
        var imagePreviews = await GetImagePreviews();
        RenamePreviews = new ObservableCollection<PreviewModel>(imagePreviews);
        TotalImagesCount = RenamePreviews.Count;
        IsRenameEnabled = TotalImagesCount > 0;
        IsBusy = false;
    }
    
    private async Task RenameImages()
    {
        var previews = RenamePreviews;
        foreach (var preview in previews)
        {
            var oldPath = Path.Join(preview.FolderPath, preview.OldFilename);
            var destinationFolder = preview.DestinationFolderPath ?? preview.FolderPath ?? string.Empty;
            Directory.CreateDirectory(destinationFolder);
            var newPath = Path.Join(destinationFolder, preview.NewNameWithExtension);
            if (string.Equals(oldPath, newPath, System.StringComparison.OrdinalIgnoreCase)) continue;
            File.Move(oldPath, newPath, overwrite:false);
        }
        await UpdateImageCount();
    }

    public bool OrganizeIntoFolders
    {
        get => _organizeIntoFolders;
        set
        {
            if (SetProperty(ref _organizeIntoFolders, value)) Task.Run(UpdateImageCount);
        }
    }

    public string FolderPattern
    {
        get => _folderPattern;
        set => SetProperty(ref _folderPattern, value);
    }

    private void ClearImages()
    {
        PathFolders.Clear();
        RenamePreviews.Clear();
        TotalImagesCount = 0;
        IsRenameEnabled = false;
        IncludeSubfolders = false;
        OrganizeIntoFolders = false;
        FolderPattern = "%year%/%month%";
        CustomFormat = string.Empty;
        CustomDateFormat = string.Empty;
        SelectedRenamerDateType = RenamerDateTypes[1];
        SelectedDateRenamerPattern = BuiltInRenamerPatterns.First();
    }
    
    #endregion
}
