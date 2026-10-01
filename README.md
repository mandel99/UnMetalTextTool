# UnMetal Text Tool

A small Windows utility for converting UnMetal dictionary files between `.bin` and editable `.txt` files.

## Features

- Drag and drop one or multiple `.bin` / `.txt` files into the window.
- Select one or multiple files with the **Select Files...** button.
- `.bin` files are converted to `.txt`.
- `.txt` files are converted back to `.bin`.
- Supports separate `[base]` and `[extra]` sections so repeated IDs in different sections are preserved correctly.
- No dependency on the original game executable or `font_default.txt`.
- Targets **.NET Framework 4.8** for broad Windows 10/11 compatibility.

## Text format

```text
# base_count: 3
[base]
0:"START NEW GAME"
1:"LOAD GAME"
2:"OPTIONS"

[extra]
1:"Alternative text stored in the extra section"
```

The same numeric ID can appear once in `[base]` and once in `[extra]` without conflict.

Supported escape sequences inside text are `\\`, `\\"`, `\\n`, `\\r`, and `\\t`.

## Usage

1. Run `UnMetalTextTool.exe`.
2. Drag files into the window or click **Select Files...**.
3. Converted files are written next to the source file with the opposite extension.

## Build

Open the project in Visual Studio with the .NET Framework 4.8 Developer Pack installed, or build it from a Developer Command Prompt:

```bat
msbuild UnMetalTextTool.csproj /restore /p:Configuration=Release
```

## Release

The `v1.0.0` release contains the ready-to-run Windows build.
