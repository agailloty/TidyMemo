using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TidyMemo.Common;
using TidyMemo.Models;

namespace TidyMemo.ViewModels;

public class ExifMetadataExplorerDialogViewModel
{
    public event EventHandler<ExifMetadataDialogResult?>? RequestClose;
    ExifInput _parameter;
    
    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }
    
    public ExifMetadataDialogResult? Result { get; private set; }
    public ExifMetadataExplorerDialogViewModel(ExifInput parameter)
    {
        _parameter = parameter;

        OkCommand = new RelayCommand(OnOkCommand);
        CancelCommand = new RelayCommand(OnCancelCommand);
        
        ExifTags = parameter.ExifTags;
    }
    
    public ObservableCollection<ExifTokenItemViewModel> ExifTags { get; set; }
    
    private void OnOkCommand()
    {
        Result = new ExifMetadataDialogResult
        {
            ClosingResult = ClosingResult.Ok,
            ExifTokens = ExifTags.Where(e => e.IsSelected).ToList()
        };
        
        RequestClose?.Invoke(this, Result);
    }
    private void OnCancelCommand()
    {
        Result = new ExifMetadataDialogResult
        {
            ClosingResult = ClosingResult.Cancel
        };
        
        RequestClose?.Invoke(this, Result);
    }
}