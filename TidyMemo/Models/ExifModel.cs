using MetadataExtractor;

namespace TidyMemo.Models;

public class ExifToken
{
    public required string Key { get; set; }
    public required Tag Tag { get; set; }
}