using System.IO;
using TidyMemo.Services;
using Xunit;

namespace TidyMemo.Tests;

public sealed class SlideshowServiceTests
{
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
