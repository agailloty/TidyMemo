using System;
using System.IO;
using System.Threading.Tasks;
using TidyMemo.Models;
using TidyMemo.Services;
using Xunit;

namespace TidyMemo.Tests;

public sealed class SlideshowProjectStoreTests
{
    [Fact]
    public void NewProjectsIncludeImageSubfoldersByDefault()
    {
        var project = new SlideshowProject();

        Assert.True(project.Presentation.IncludeSubfolders);
    }

    [Fact]
    public async Task ProjectRoundTripsWithStableIdentityAndSettings()
    {
        var directory = Directory.CreateTempSubdirectory("slidetune-tests-");
        try
        {
            var path = Path.Combine(directory.FullName, "holiday.slidetune");
            var id = Guid.NewGuid();
            var slideId = Guid.NewGuid();
            var project = new SlideshowProject
            {
                Id = id,
                Name = "Holiday",
                Slides = [new SlideshowSlide { Id = slideId, Path = "media/photo.jpg" }],
                Sources = [new SlideshowSource { Path = "media", IncludeSubfolders = true }],
                Presentation = new SlideshowPresentationSettings
                {
                    Width = 3840, Height = 2160, TransitionMode = TransitionMode.Random,
                    TransitionId = "dissolve", TransitionDuration = 1.2,
                    MotionMode = PhotoMotionMode.RandomSoft, MotionId = "slow-zoom-in",
                    MotionIntensity = MotionIntensity.Subtle, MotionEasing = MotionEasing.EaseOut,
                    RandomSeed = 1234
                },
                Audio = new SlideshowAudioSettings { Path = "audio/music.mp3", Volume = 0.4 },
                Export = new SlideshowExportSettings { OutputFile = "exports/movie.mp4", Quality = 20 }
            };
            var store = new JsonSlideshowProjectStore();

            await store.SaveAsync(path, project);
            var loaded = await store.LoadAsync(path);

            Assert.Equal(id, loaded.Id);
            Assert.Equal(slideId, loaded.Slides[0].Id);
            Assert.Equal("Holiday", loaded.Name);
            Assert.True(loaded.Sources[0].IncludeSubfolders);
            Assert.Equal(TransitionMode.Random, loaded.Presentation.TransitionMode);
            Assert.Equal(PhotoMotionMode.RandomSoft, loaded.Presentation.MotionMode);
            Assert.Equal(MotionIntensity.Subtle, loaded.Presentation.MotionIntensity);
            Assert.Equal(MotionEasing.EaseOut, loaded.Presentation.MotionEasing);
            Assert.Equal(1234, loaded.Presentation.RandomSeed);
            Assert.Equal("audio/music.mp3", loaded.Audio.Path);
            Assert.Equal(20, loaded.Export.Quality);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task ReplacingProjectKeepsBackupOfPreviousVersion()
    {
        var directory = Directory.CreateTempSubdirectory("slidetune-tests-");
        try
        {
            var path = Path.Combine(directory.FullName, "project.slidetune");
            var store = new JsonSlideshowProjectStore();
            await store.SaveAsync(path, new SlideshowProject { Name = "First" });
            await store.SaveAsync(path, new SlideshowProject { Name = "Second" });

            var backup = await store.LoadAsync(path + ".bak");
            var current = await store.LoadAsync(path);

            Assert.Equal("First", backup.Name);
            Assert.Equal("Second", current.Name);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void RelativePathsRemainPortableWithProjectDirectory()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "portable", "project.slidetune");
        var mediaPath = Path.Combine(Path.GetTempPath(), "portable", "media", "photo.jpg");

        var stored = SlideshowProjectPaths.ToStoredPath(mediaPath, projectPath);
        var restored = SlideshowProjectPaths.ToAbsolutePath(stored, projectPath);

        Assert.Equal(Path.Combine("media", "photo.jpg"), stored);
        Assert.Equal(Path.GetFullPath(mediaPath), restored);
    }
}
