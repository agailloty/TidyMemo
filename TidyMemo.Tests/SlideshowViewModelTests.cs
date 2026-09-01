using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TidyMemo.Models;
using TidyMemo.Services;
using TidyMemo.ViewModels;
using Xunit;

namespace TidyMemo.Tests;

public sealed class SlideshowViewModelTests
{
    [Fact]
    public async Task ClearReturnsToAValidProjectSelectionState()
    {
        var dialogs = new StubDialogService
        {
            SavePath = Path.Combine(Path.GetTempPath(), "current.slidetune"),
            ImagePaths = [Path.Combine(Path.GetTempPath(), "photo.jpg")]
        };
        var settings = new SettingsViewModel(new SettingsService(), dialogs, new FfmpegDownloadService());
        var viewModel = new SlideshowViewModel(
            new SlideshowService(), new ExifService(), dialogs, settings, () => { }, new InMemoryProjectStore());

        await viewModel.NewProjectCommand.ExecuteAsync(null);
        await viewModel.AddImagesCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsProjectOpen);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.HasImages);

        viewModel.ClearCommand.Execute(null);

        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.HasImages);
        Assert.Null(viewModel.ProjectPath);
        Assert.Equal("Untitled slideshow", viewModel.ProjectName);
        Assert.True(viewModel.OpenProjectCommand.CanExecute(null));
    }

    private sealed class InMemoryProjectStore : ISlideshowProjectStore
    {
        public Task<SlideshowProject> LoadAsync(string path) => Task.FromResult(new SlideshowProject());
        public Task SaveAsync(string path, SlideshowProject project) => Task.CompletedTask;
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? SavePath { get; init; }
        public IReadOnlyList<string> ImagePaths { get; init; } = [];

        public Task<string?> ShowFolderBrowserDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowFilePickerAsync(string title, string[] patterns) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<string>> ShowFilePickerMultipleAsync(string title, string[] patterns) =>
            Task.FromResult(ImagePaths);
        public Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string extension) =>
            Task.FromResult(SavePath);
        public Task<ExifMetadataDialogResult> ShowExifMetadataDialogAsync(ExifInput exifInput) =>
            Task.FromResult(new ExifMetadataDialogResult());
    }
}
