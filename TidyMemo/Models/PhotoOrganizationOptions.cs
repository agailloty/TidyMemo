namespace TidyMemo.Models;

/// <summary>
/// Controls the optional destination hierarchy. FolderPattern accepts the same
/// metadata tokens as file names, plus %year%, %month% and %monthname%.
/// A slash separates hierarchy levels (for example: %year%/%month%).
/// </summary>
public sealed class PhotoOrganizationOptions
{
    public bool Enabled { get; init; }
    public string RootFolder { get; init; } = string.Empty;
    public string FolderPattern { get; init; } = "%year%/%month%";
}
