using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TidyMemo.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace TidyMemo.Services;

public class ExifService
{
    public string? GetExifValue(string path, int tag)
    {
        var directories = ImageMetadataReader.ReadMetadata(path);
        foreach (var directory in directories)
        {
            var tagValue = directory.GetDescription(tag);
            if (tagValue != null) return tagValue;
        }

        return null;
    }
    
    public DateTime? GetDateFromExif(string filename)
    {
        var directories = ImageMetadataReader.ReadMetadata(filename);
        var exifSubDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var originalDate = exifSubDirectory?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
        var dateFormat = new DateTimeFormatInfo {DateSeparator = ":", TimeSeparator = ":"};
        return originalDate != null ? DateTime.Parse(originalDate, dateFormat) : null;
    }
    
    public string GetExifValue(Tag exifTag, string filename)
    {   
        var directories = ImageMetadataReader.ReadMetadata(filename);
        var allTags = directories.SelectMany(d => d.Tags).ToList();
        var tagValue = allTags.FirstOrDefault(t => t.Name == exifTag.Name);
        string result = string.Empty;
        if (tagValue != null)
        {
            result = tagValue.Description ?? string.Empty;
        }
        return result;
    }
    
    public List<string> RetrieveExifTags(string[] filenames)
    {
        return filenames.AsParallel()
            .SelectMany(RetrieveExifTagsFromFile)
            .Distinct()
            .ToList();
    }

    private List<string> RetrieveExifTagsFromFile(string filename)
    {
        var directories = ImageMetadataReader.ReadMetadata(filename);
        var allTags = directories.SelectMany(d => d.Tags).ToList();
        
        var tags = new List<string>();
        foreach (var tag in allTags)
        {
            tags.Add(tag.Name);
        }

        return tags;
    }

    string ParseDateFormat(string dateFormat)
    {
        string result = "yyyyMMdd_mmss";
        if (!string.IsNullOrEmpty(dateFormat))
        {
            result = dateFormat.Replace("Y", "y")
                .Replace("Monthname", "MMMM")
                .Replace("DD", "dd")
                .Replace("MMSS", "mmss");
        }
        return result;
    }

    private string InterpolateCustomFormat(string token, string filename)
    {
        string? result = token;
        var args = token.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        string command = args[0];
        string flag = string.Empty;
        if (args.Length > 1)
        {
            flag = args[1];
        }

        switch (command.ToLower())
        {
            case "datetaken":
                var dateTaken = GetDateFromExif(filename);
                result = dateTaken?.ToString(ParseDateFormat(flag));
                break;
            case "datecreated":
                var file = new FileInfo(filename);
                result = file.CreationTime.ToString(ParseDateFormat(flag));
                break;
            case "datemodified":
                file = new FileInfo(filename);
                result = file.LastWriteTime.ToString(ParseDateFormat(flag));
                break;
            default:
                var exifTokens = GetExifTokens(filename);
                var availableTokens = exifTokens.Select(e => e.Key).ToList();
                if (availableTokens.Contains(command.ToLower()))
                {
                    result = exifTokens.FirstOrDefault(e => e.Key == command)?.Tag.Description;
                }

                break;
        }
        
        return result ?? string.Empty;
    }

    private ExifToken[] GetExifTokens(string filename)
    {
        var directories = ImageMetadataReader.ReadMetadata(filename);
        var allTags = directories.SelectMany(d => d.Tags).ToList();
        var tokens = new List<ExifToken>();
        foreach (var tag in allTags)
        {
            string key = tag.Name.Replace("/", "").Replace("(", "")
                .Replace(")", "")
                .Replace(" ", "")
                .ToLower();
            var exifToken = new ExifToken
            {
                Key = key,
                Tag = tag
            };
            tokens.Add(exifToken);
        }
        //tokens.Sort();
        return tokens.ToArray();
    }
    
    public string TokenizeExifName(string exifName)
    {
        string result = exifName;
        if (!string.IsNullOrEmpty(exifName))
        {
            result = exifName.Replace("/", "").Replace("(", "")
                .Replace(")", "")
                .Replace(" ", "")
                .ToLower();
            result = "%" + result + "%";
        }
        return result;
    }
    
    public string GetExifTags(string customFormat, string filename)
    {
        string result = string.Empty;
        if (!string.IsNullOrEmpty(customFormat))
        {
            string pattern = "%([^%]+)%";

            MatchCollection matches = Regex.Matches(customFormat, pattern);
            foreach (Match match in matches)
            {
                var token = match.Value.Replace("%", "");
                result += InterpolateCustomFormat(token, filename);
            }
        }
        
        return result;
    }
}