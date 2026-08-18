using System.Collections.Generic;
using System.Collections.ObjectModel;
using TidyMemo.Common;
using TidyMemo.Models;

namespace TidyMemo.ViewModels;

public class ExifMetadataDialogResult
{
    public ClosingResult ClosingResult { get; set; }
    public List<ExifTokenItemViewModel> ExifTokens { get; set; } = new();
}