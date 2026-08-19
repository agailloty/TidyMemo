using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TidyMemo.Models;

namespace TidyMemo.Services;

public class RenamerService
{
    private readonly ExifService _exifService;

    public RenamerService()
    {
        _exifService = new ExifService();
    }
    public List<RenamerPatternModel> GetBuiltInRenamerPatterns()
    {
        return new List<RenamerPatternModel>
        {
            new() { Name = "Choose pattern", Description = "Choose a renaming pattern" },
            new() { Name = "YY-MM-DD", Description = "Year-Month-Day (2-digit year)" },
            new() { Name = "YYYY-MM-DD", Description = "Year-Month-Day (4-digit year)" },
            new() { Name = "YY-Monthname-DD", Description = "Year-Monthname-Day (2-digit year)" },
            new() { Name = "YYYY-Monthname-DD", Description = "Year-Monthname-Day (4-digit year)" },
            new() { Name = "YY-MM-DD_HHMMSS", Description = "Year-Month-Day HourMinuteSecond (2-digit year)" },
            new() { Name = "YYMMDD_HHMMSS", Description = "YearMonthDay HourMinuteSecond (2-digit year)" },
            new() { Name = "YYYY-MM-DD_HHMMSS", Description = "Year-Month-Day HourMinuteSecond (4-digit year)" },
            new() { Name = "YYYYMMDD_HHMMSS", Description = "YearMonthDay HourMinuteSecond (4-digit year)" },
            new() { Name = "YY-Monthname-DD_HHMMSS", Description = "Year-Monthname-Day HourMinuteSecond (2-digit year)" },
            new() { Name = "YYYY-Monthname-DD_HHMMSS", Description = "Year-Monthname-Day HourMinuteSecond (4-digit year)" },
            new() { Name = "DD-MM-YY", Description = "Day-Month-Year (2-digit year)" },
            new() { Name = "DD-MM-YYYY", Description = "Day-Month-Year (4-digit year)" },
            new() { Name = "DD-Monthname-YY", Description = "Day-Monthname-Year (2-digit year)" },
            new() { Name = "DD-Monthname-YYYY", Description = "Day-Monthname-Year (4-digit year)" },
            new() { Name = "DD-MM-YY_HHMMSS", Description = "Day-Month-Year HourMinuteSecond (2-digit year)" },
            new() { Name = "DD-MM-YYYY_HHMMSS", Description = "Day-Month-Year HourMinuteSecond (4-digit year)" },
            new() { Name = "DD-Monthname-YY_HHMMSS", Description = "Day-Monthname-Year HourMinuteSecond (2-digit year)" },
            new() { Name = "DD-Monthname-YYYY_HHMMSS", Description = "Day-Monthname-Year HourMinuteSecond (4-digit year)" },
            new() { Name = "MM-DD-YY", Description = "Month-Day-Year (2-digit year)" },
            new() { Name = "MM-DD-YYYY", Description = "Month-Day-Year (4-digit year)" },
            new() { Name = "Monthname-DD-YY", Description = "Monthname-Day-Year (2-digit year)" },
            new() { Name = "Monthname-DD-YYYY", Description = "Monthname-Day-Year (4-digit year)" },
            new() { Name = "MM-DD-YY_HHMMSS", Description = "Month-Day-Year HourMinuteSecond (2-digit year)" },
            new() { Name = "MM-DD-YYYY_HHMMSS", Description = "Month-Day-Year HourMinuteSecond (4-digit year)" },
            new() { Name = "Monthname-DD-YY_HHMMSS", Description = "Monthname-Day-Year HourMinuteSecond (2-digit year)" },
            new() { Name = "Monthname-DD-YYYY_HHMMSS", Description = "Monthname-Day-Year HourMinuteSecond (4-digit year)" },
            new() { Name = "Custom Date Time", Description = "Custom Date Time" , IsCustomDateFormat = true},
            new() { Name = "Custom", Description = "Custom" }
        };
    }

    public bool RenameFile(string filename, string newFilename)
    {
        var file = new FileInfo(filename);
        var newFile = new FileInfo(newFilename);
        if (!file.Exists || newFile.Exists) return false;
        file.MoveTo(newFilename);
        return true;
    }

    public async Task<PreviewModel[]> GetRenamePreviews(string[] filenames, RenamerPatternModel pattern, DateType selectedDateType, bool isCustomMode,
        PhotoOrganizationOptions? organization = null)
    {
        var previews = new PreviewModel[filenames.Length];
        if (pattern.Name == "Choose pattern")
        {
            previews = filenames.Select(f => new PreviewModel
            {
                OldFilename = Path.GetFileName(f),
                NewFilename = Path.GetFileNameWithoutExtension(f),
                Extension = Path.GetExtension(f),
                FolderPath = Path.GetDirectoryName(f),
                DestinationFolderPath = GetDestinationFolder(f, organization)
            }).ToArray();
            return MakeUniqueFilenames(previews);
        }
        await Task.Run(() =>
        {
            for (var i = 0; i < filenames.Length; i++)
            {
                if (isCustomMode)
                {
                    previews[i] = GetCustomRenamePreview(filenames[i], pattern);
                }
                else
                {
                    previews[i] = GetDateRenamePreview(filenames[i], pattern, selectedDateType);
                }

                previews[i].DestinationFolderPath = GetDestinationFolder(filenames[i], organization);
                
            }
        });
        
        previews = MakeUniqueFilenames(previews);
        return previews;
    }

    private string GetDestinationFolder(string filename, PhotoOrganizationOptions? options)
    {
        var sourceFolder = Path.GetDirectoryName(filename) ?? string.Empty;
        if (options is not { Enabled: true }) return sourceFolder;

        var root = string.IsNullOrWhiteSpace(options.RootFolder) ? sourceFolder : options.RootFolder;
        var rendered = _exifService.GetExifTags(options.FolderPattern, filename);
        var parts = rendered.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePathSegment)
            .Where(part => part.Length > 0);
        return parts.Aggregate(root, Path.Combine);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Trim().Select(c => invalid.Contains(c) ? '_' : c));
    }
    
    private PreviewModel GetDateRenamePreview(string filename, RenamerPatternModel pattern, DateType selectedDateType)
    {
        var file = new FileInfo(filename);
        var extension = file.Extension;
        DateTime renameDate = DateTime.MinValue;
        switch (selectedDateType)
        {
            case DateType.Creation : renameDate = file.CreationTime; break;
            case DateType.Modification : renameDate = file.LastWriteTime; break;
            case DateType.PhotoTaken : 
                var exifDate = _exifService.GetDateFromExif(filename);
                renameDate = exifDate ?? file.CreationTime;
                break;
        }
        
        var newFilename = $"{GetFormattedDate(renameDate, pattern)}";
        var folderPath = file.Directory?.FullName ?? string.Empty;
        return new PreviewModel { OldFilename = file.Name, NewFilename = newFilename, FolderPath = folderPath, Extension = extension };
    }

    private PreviewModel GetCustomRenamePreview(string filename, RenamerPatternModel pattern)
    {
        var file = new FileInfo(filename);
        var extension = file.Extension;
        
        var rendered = _exifService.GetExifTags(pattern.Name, filename);
        var newFilename = SanitizePathSegment(rendered);
        if (string.IsNullOrWhiteSpace(newFilename)) newFilename = Path.GetFileNameWithoutExtension(filename);
        var folderPath = file.Directory?.FullName ?? string.Empty;
        return new PreviewModel { OldFilename = file.Name, NewFilename = newFilename, FolderPath = folderPath, Extension = extension };
    }
    

    private string GetFormattedDate(DateTime date, RenamerPatternModel pattern)
    {
        string formattedDate = pattern.Name
            .Replace("Y", "y")
            .Replace("Monthname", "MMMM")
            .Replace("DD", "dd")
            .Replace("MMSS", "mmss");
        
        return date.ToString(formattedDate);
    }
    
    private PreviewModel[] MakeUniqueFilenames(PreviewModel[] previews)
    {
        var uniqueFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preview in previews)
        {
            var newName = preview.NewFilename ?? string.Empty;
            var i = 1;
            var destination = preview.DestinationFolderPath ?? preview.FolderPath ?? string.Empty;
            var candidate = Path.Combine(destination, newName + preview.Extension);
            var source = Path.Combine(preview.FolderPath ?? string.Empty, preview.OldFilename ?? string.Empty);
            while (!uniqueFilenames.Add(candidate) ||
                   (File.Exists(candidate) && !string.Equals(candidate, source, StringComparison.OrdinalIgnoreCase)))
            {
                newName = $"{preview.NewFilename}_{i}";
                i++;
                candidate = Path.Combine(destination, newName + preview.Extension);
            }
            preview.NewFilename = newName;
        }
        return previews;
    }
}
