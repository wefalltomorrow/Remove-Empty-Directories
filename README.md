# RED+: Remove Empty Directories Plus

**An independently maintained, performance-focused fork of RED+.**

**Download:** [Latest fork release](https://github.com/wefalltomorrow/Remove-Empty-Directories/releases/latest) · [All fork releases](https://github.com/wefalltomorrow/Remove-Empty-Directories/releases)

**Upstream project:** [BookOfBeasts/Remove-Empty-Directories-Plus](https://github.com/BookOfBeasts/Remove-Empty-Directories-Plus)

RED+ is based on the original [RED by Jonas John](http://www.jonasjohn.de/) and was created by [Robert “NotBob” Bookerby](https://github.com/BookOfBeasts). This fork keeps that project history and adds scan-performance, reliability, and build improvements.

RED+ finds, displays, and deletes empty directories recursively below a chosen start folder. You can create custom rules for keeping or deleting folders, such as treating directories containing only empty files as empty.

![RED+ screenshot](help/img/screen-M02.png)

## Changes in this fork

- Bounded parallel directory scanning to use available CPU resources without creating an unbounded number of workers.
- Lazy filesystem enumeration to reduce intermediate allocations while traversing large directory trees.
- Safer handling of file sizes, directory write probes, and scan logging.
- More reliable and efficient tree sorting and scan-result file counts.
- A Windows GitHub Actions workflow that restores, builds, and packages the portable Release output.

These changes target responsiveness and reliability on large scans. No benchmark results are currently published, so performance gains will depend on the filesystem and directory structure.

## Features

- Simple user interface.
- Shows empty directories before deleting them.
- Multiple delete modes, including sending items to the Recycle Bin.
- Whitelists and blacklists directories using filter lists.
- Can treat directories containing empty files as empty.
- Portable configuration stored beside the executable, or configuration stored under %APPDATA%.

## System requirements

- Windows 7 or later.
- Microsoft .NET Framework 4.8.
- No installer is required: unzip the download and run the application.
- On first launch, RED+ asks where to store RED+.cfg if no configuration file is present:
  - **Portable mode** stores it beside the executable.
  - **%APPDATA% mode** stores it in a subfolder of your Windows roaming profile.
  - If RED+ is in a protected folder such as Program Files, Windows may prevent writing the portable configuration file; choose %APPDATA% mode instead.

## Safety

### ⚠️ Use this software entirely at your own risk.

RED+ deletes directories. Review the results and selected delete mode carefully before confirming a deletion. The original maintainer reports using the original RED for many years and RED+ since July 2024 without significant issues, but provides no guarantee that the software will work on every system.

## Contributing

Contributions of code, translations, documentation, and wording improvements are welcome. You can also review open issues in this fork and in [original RED](https://github.com/hxseven/Remove-Empty-Directories/issues).

## History

The first version of RED was created by Jonas John around 2005. A small team of contributors later fixed bugs and added features to [original RED](https://github.com/hxseven/Remove-Empty-Directories/graphs/contributors).

In July 2024, Robert “NotBob” Bookerby created RED+ using the core code of RED. RED+ became available on GitHub in June 2025. This repository is an independent fork; upstream remains the source for the original RED+ project and releases.

## Changelog

### Fork build 1 — based on RED+ 25.4.0.0

- Improve directory-scan throughput with bounded parallel work and lazy enumeration.
- Improve scan and UI reliability, file-size handling, directory-write checks, and tree sorting.
- Add a Windows GitHub Actions build and portable-package workflow.

### Upstream RED+ releases

#### 25.4.0.0 (*August 2026*)

- Fix startup exception when using UNC network paths (issue #15; thanks to jorensanbar).
- Update help documentation.

#### 25.3.0.0 (*December 2025*)

- Fix the Save Prompt not being actioned on program exit.
- Fix Reset Settings/Filters behavior; Reset Settings no longer resets filters.
- Change how Explorer Integration is handled. See the help file under Settings → Advanced Settings → Windows Explorer Integration.
- Prevent exit while Search or Delete is active.
- Update help documentation.

#### 25.2.0.0 (*November 2025*)

- Improve command-line parsing.
- Update basic help.

#### 25.1.0.0 (*November 2025*)

- Add basic help documentation (help/index.htm; incomplete and a work in progress).
- Show microseconds in the search runtime.
- Disable search-related buttons when not on the Search tab.
- Add the -autosearch command-line switch.

#### 25.0.1.0 (*November 2025*)

- Fix restoration of the Last Used Directory on program load.

#### 25.0.0.0 (*June 2025*)

- First RED+ release, based on [Jonas John's RED 2.3.0 beta](https://github.com/hxseven/Remove-Empty-Directories).
- Rewrite directory and file name matching to support a more sophisticated syntax.
- Add a directory filter that prevents a directory from being treated as empty while still checking its subdirectories.
- Add a dedicated grid for displaying and editing filter rules.
- Update the UI layout and icons.
- Replace settings load/save with a custom routine for portable use. The settings file uses the executable's name with a .cfg extension. Delete the .cfg file to be prompted to store settings under %APPDATA% instead.
- Add the foundations for translations using standard .po files.

For earlier releases of the original RED, see the [original RED project](https://github.com/hxseven/Remove-Empty-Directories).

## Credits

- RED+ is built on the work of Jonas John and the contributors to [original RED](https://github.com/hxseven/Remove-Empty-Directories/graphs/contributors).
- RED+ was created and maintained by [Robert “NotBob” Bookerby](https://github.com/BookOfBeasts).
- Filesystem calls use the [AlphaFS library](https://github.com/alphaleonis/AlphaFS).
- Icons include Nuvola (GNU LGPL 2.1), NuoveXT (GPL), [famfamfam Silk](https://github.com/legacy-icons/famfamfam-silk) (CC BY 2.5), and [FatCow](https://github.com/gammasoft/fatcow) (CC BY 3.0).

## License

RED is free software; you may redistribute it and/or modify it under the terms of the [GNU Lesser General Public License](https://www.gnu.org/licenses/lgpl.html), version 3 or (at your option) any later version.
