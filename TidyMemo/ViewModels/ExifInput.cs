using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TidyMemo.ViewModels;

public class ExifInput
{
    public ObservableCollection<ExifTokenItemViewModel> ExifTags { get; set; } = new();
}