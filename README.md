# TidyMemo

## Headless slideshow export

A saved `.slidetune` project can be rendered without starting the graphical interface. The project
contains the media paths, output path, presentation effects, motion, transitions, audio and export
settings. FFmpeg is read from the existing TidyMemo application settings.

```powershell
TidyMemo.exe "C:\projects\holiday.slidetune"
```

The explicit form and automation overrides are also supported:

```powershell
TidyMemo.exe --render "C:\projects\holiday.slidetune" --output "C:\bench\run-01.mp4"
TidyMemo.exe --render "C:\projects\holiday.slidetune" --ffmpeg "C:\ffmpeg\bin\ffmpeg.exe"
```

Progress and elapsed time are written to standard output. Exit code `0` means success, `1` means
the export failed, `2` means the command line or configuration is invalid, and `130` means it was
cancelled with Ctrl+C. Starting TidyMemo without arguments still opens the graphical interface.

**Organize, understand, transform, and revisit your photo and video memories — locally.**

TidyMemo is a free, open-source desktop application for maintaining personal
photo and video collections. It can rename and arrange photos from their dates
and metadata, process videos in batches, and turn a sequence of images into an
MP4 slideshow. Files stay on your computer: there is no account, advertising,
cloud upload, or hosted media library.

TidyMemo runs on Windows, macOS, and Linux. It is built with C#, .NET, and
Avalonia UI and is licensed under the MIT License.

## Features

### Photo renaming and organization

- Add one or several folders and optionally scan their subfolders.
- Rename JPEG, PNG, GIF, BMP, TIFF, HEIC, and HEIF images.
- Base names on the photo-taken EXIF date, file creation date, or modification
  date. When the capture date is unavailable, TidyMemo falls back to the file
  creation date.
- Choose from built-in date and date-time patterns, enter a custom date format,
  or compose a name from any metadata fields found in the selected images.
- Preview the source name, destination name, and destination folder before
  changing anything.
- Automatically disambiguate duplicate names with a numeric suffix and refuse
  to overwrite existing files.
- Optionally arrange images in a generated folder hierarchy such as
  `%year%/%month%`; date tokens and image metadata tokens are supported.

Useful custom tokens include `%datetaken%`, `%datecreated%`, `%datemodified%`,
`%year%`, `%month%`, and `%monthname%`. A date token can include a format after a
comma, for example `%datetaken,YYYY-MM-DD_HHMMSS%`. The EXIF Explorer lists and
inserts the additional tokens available in the current collection.

> Renaming and organization move the original files. Always review the preview
> and keep a backup of important collections.

### Video toolkit

Add individual files or entire folders, optionally including subfolders. TidyMemo
recognizes MP4, MOV, AVI, MKV, WMV, FLV, M4V, and WebM input files and offers five
operations:

- **Compress** with ready-made quality/size presets, per-file preset selection,
  optional H.264 or H.265 encoding, or advanced CRF and encoder-speed controls.
- **Speed up** a video by 1.25×, 1.5×, 2×, 3×, or 4× while keeping audio in sync.
- **Convert** to MP4, MKV, MOV, WebM, or AVI.
- **Export GIF** with a selectable width and frame rate.
- **Speed up and export GIF** in one operation.

Jobs run sequentially and report their state, output size, and space reduction.
Processing can be cancelled. Originals remain untouched: results are written to
a configurable subfolder (named `Final` by default), with an optional filename
suffix, and a completed result can be opened from the application.

### SlideTune slideshow creator

- Add supported images individually or from a folder, including subfolders.
- Reorder images manually or sort them by filename, capture date, creation date,
  or modification date.
- Create an MP4 with a configurable duration per image and frame rate.
- Choose Full HD or HD landscape, Full HD portrait, square, or 4K landscape.
- Use a blurred fill, solid color, gradient, or image behind fitted photos; a
  simple crop-to-fill mode is also available.
- Add an optional border and drop shadow.
- Add looping MP3, WAV, M4A, AAC, OGG, or FLAC background music and set its
  volume.
- Adjust H.264 CRF quality and encoding speed, monitor progress, cancel an
  export, and open the completed file.
- Optionally use ImageMagick for enhanced background preparation.
- Add native FFmpeg transitions between completed slide canvases. Choose one transition for the
  whole slideshow, use Random without immediate repetitions, or keep None for the historical
  cut behavior. Transition time is an overlap and therefore shortens the final duration; see
  [`docs/slideshow-transitions.md`](docs/slideshow-transitions.md).

### Local settings and privacy

Photo renaming and metadata inspection work without FFmpeg. The video toolkit
and SlideTune require FFmpeg; provide an existing executable in **Settings** or
use TidyMemo's platform-aware downloader. The download is stored in the user's
local application-data directory. The configured path, video output-subfolder
name, and video-feature switch are persisted locally.

ImageMagick is optional and is used only when its enhanced SlideTune processing
option is enabled. No media is sent to TidyMemo or to a cloud service.

## Screenshots

The website in [`docs/`](docs/) contains current illustrative views of the main
workspaces. Older application captures are also kept in that folder for project
history.

## Install

Download the latest build from [GitHub Releases](https://github.com/agailloty/TidyMemo/releases/latest):

- Windows x64: per-user NSIS installer or portable ZIP;
- macOS Intel and Apple Silicon: DMG or ZIP containing the application bundle;
- Linux x64: Debian package or portable TAR.GZ.

Current packages do not establish a verified publisher identity. Windows may
show an unknown-publisher warning; macOS bundles have an ad-hoc integrity
signature but are not Developer ID signed or notarized; Linux packages are
unsigned. Release artifacts include SHA-256 checksums.

## Build and run

Requirements:

- .NET 10 SDK;
- FFmpeg for the video toolkit and SlideTune;
- ImageMagick only for SlideTune's optional enhanced background processing.

From the repository root:

```shell
dotnet restore TidyMemo/TidyMemo.csproj
dotnet run --project TidyMemo/TidyMemo.csproj
```

Run a release build with:

```shell
dotnet build TidyMemo/TidyMemo.csproj --configuration Release
```

## Website

The static French-language product site uses plain HTML, CSS, JavaScript, and
SVG assets in `docs/`. Preview it without building the desktop application:

```shell
python -m http.server 8080 --directory docs
```

Then open `http://localhost:8080`. Changes limited to `docs/` deploy through the
`Deploy landing page` workflow. Configure GitHub Pages to use **GitHub Actions**
as its source.

## Publishing a release

Pushes and pull requests targeting `master` build the supported packages. To
publish them in a GitHub Release, create an exact `vMAJOR.MINOR.PATCH` tag:

```shell
git tag v1.0.0
git push origin v1.0.0
```

Signing or notarization credentials must be stored as protected GitHub Actions
environment secrets and must never be committed.

## Scope and direction

TidyMemo is a local maintenance workspace, not a photo editor, cloud gallery, or
catalog that takes ownership of a collection. Its principles are safe previews,
durable filenames and folders, local processing, and equal support for photo and
video workflows. Ideas discussed for future releases are not documented as
current functionality until they are implemented.

## Contributing

Bug reports, feature proposals, design feedback, documentation improvements, and
code contributions are welcome through the
[issue tracker](https://github.com/agailloty/TidyMemo/issues). When proposing a
feature, explain how it helps people organize, preserve, revisit, or reduce the
storage footprint of personal media.

## License

TidyMemo is available under the [MIT License](LICENSE).
