# TidyMemo

**Organize, understand, and lighten your photo and video memories — locally.**

TidyMemo is a free, open-source desktop application for taking control of personal
photo and video collections. It helps turn folders full of camera-generated file
names into a clear, durable archive while keeping every file on your computer.

The project started as a focused EXIF-based image renamer. Its scope now includes
video processing and is growing into a broader media-management tool.

## What TidyMemo does today

- Rename image collections from capture dates and EXIF metadata.
- Build reusable filename patterns and preview every change before applying it.
- Explore the metadata embedded in image files.
- Compress one video or a complete folder using FFmpeg and configurable presets.
- Process files locally, without an account, advertising, or cloud upload.
- Run on Windows, macOS, and Linux.

## Product direction

TidyMemo is intended to become a simple workspace for maintaining personal media
libraries rather than a conventional photo editor or cloud gallery. Its guiding
principles are:

1. **Memories first** — features should make personal collections easier to
   understand, preserve, and revisit.
2. **Safe by design** — preview potentially destructive operations and keep the
   user in control of every change.
3. **Local by default** — personal media stays on the user's device.
4. **Useful organization** — filenames and folders should remain meaningful even
   outside TidyMemo.
5. **Photo and video together** — common workflows should not require separate
   tools for each media type.

Possible future work includes unified photo/video renaming, richer organization,
duplicate detection, format conversion, and additional storage-saving tools.
These items describe the direction of the project and are not commitments to a
specific release.

## Screenshots

The screenshots below show TidyMemo's main workspaces.

![Rename workspace](docs/img.png)
![Metadata explorer](docs/img_1.png)
![Video workspace](docs/img_2.png)

## Technology

TidyMemo is built with C#, .NET, and Avalonia UI. Video operations rely on
[FFmpeg](https://ffmpeg.org/). The application is licensed under the MIT License.

## Development

Requirements:

- .NET 10 SDK
- FFmpeg for video features

Run the desktop application from the repository root:

```shell
dotnet run --project TidyMemo/TidyMemo.csproj
```

## Product website

The static website lives in `docs/` and uses plain HTML, CSS, and JavaScript.
Preview it without building the desktop application:

```shell
python -m http.server 8080 --directory docs
```

Then open `http://localhost:8080`. The illustrative screenshots are stored in
`docs/assets/screenshot-*.svg`.

Changes limited to `docs/` deploy through the `Deploy landing page` workflow and
are ignored by the desktop build. In the repository settings, configure GitHub
Pages to use **GitHub Actions** as its source.

## Distribution

GitHub Actions builds installable and portable packages for pushes and pull
requests targeting `master`:

- Windows x64: per-user NSIS installer and portable ZIP;
- macOS Intel and Apple Silicon: `.app` bundle distributed as DMG and ZIP;
- Linux x64: Debian package and portable TAR.GZ.

Create and push a semantic version tag to publish these packages in a GitHub
Release:

```shell
git tag v1.0.0
git push origin v1.0.0
```

Release tags must use the exact `vMAJOR.MINOR.PATCH` format. SHA-256 checksums are
attached to each release.

### Signing status

The current distribution pipeline does not establish a verified publisher
identity:

- the Windows executable and NSIS installer are unsigned, so Microsoft Defender
  SmartScreen can show an unknown-publisher warning;
- macOS bundles use an ad-hoc signature for bundle integrity, but they are not
  signed with an Apple Developer ID or notarized;
- Linux packages are unsigned and are distributed with their checksums.

Certificates and notarization credentials must never be committed. Store them as
protected GitHub Actions environment secrets when signing is introduced.

## Contributing

TidyMemo is under active development. Bug reports, feature proposals, design
feedback, documentation improvements, and code contributions are welcome through
the repository's issue tracker.

When proposing a feature, explain how it helps people organize, preserve, or
reduce the storage footprint of their personal media.

## License

TidyMemo is available under the [MIT License](LICENSE).
