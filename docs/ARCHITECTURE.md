# Architecture

## App Structure

- `Hnxs.FontManager` contains the WPF application.
- `Models` contains UI/domain models such as `FontAsset`.
- `Services` contains scanning, caching, filename templating, and app state persistence.
- `packaging` contains install/uninstall scripts.
- `seeds` contains the original product seed specification.

## Scan Pipeline

Folder scanning is incremental:

1. Enumerate supported font files: `.ttf`, `.otf`, `.ttc`
2. Compare file path, size, and last-write timestamp against cached metadata
3. Reuse cached metadata for unchanged files
4. Parse only new or changed font files
5. Refresh installed-state and duplicate-state in the UI

The cache is stored under:

```text
%LOCALAPPDATA%\HNXS Font Manager\state.json
```

## Font Preview

The preview uses WPF `GlyphTypeface` metadata to infer:

- font family
- face/style
- OpenType weight
- italic/normal style
- stretch
- Korean glyph availability

If a selected font does not include Korean glyphs, WPF can fall back to another installed Korean font for those characters.

## Installation

Current-user installs copy files to:

```text
%LOCALAPPDATA%\Microsoft\Windows\Fonts
```

System-wide installs copy files to:

```text
%WINDIR%\Fonts
```

System-wide installation requires administrator privileges.
