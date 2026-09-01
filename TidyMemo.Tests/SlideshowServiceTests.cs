using System.IO;
using TidyMemo.Services;
using Xunit;

namespace TidyMemo.Tests;

public sealed class SlideshowServiceTests
{
    [Fact]
    public void PreparationParallelismIsBoundedAndNeverZero()
    {
        Assert.Equal(1, SlideshowService.RecommendedParallelism(0));
        Assert.Equal(1, SlideshowService.RecommendedParallelism(1));
        Assert.InRange(SlideshowService.RecommendedParallelism(100), 1, 4);
        Assert.InRange(SlideshowService.RecommendedParallelism(100, maximum: 2), 1, 2);
    }

    [Fact]
    public void GetImagesFindsPhotosWhenOnlySubfoldersContainFiles()
    {
        var directory = Directory.CreateTempSubdirectory("slidetune-images-");
        try
        {
            var album = Directory.CreateDirectory(Path.Combine(directory.FullName, "album"));
            var image = Path.Combine(album.FullName, "photo.jpg");
            File.WriteAllBytes(image, []);

            var images = new SlideshowService().GetImages(directory.FullName, includeSubfolders: true);

            Assert.Equal([image], images);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
