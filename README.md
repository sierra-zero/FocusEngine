![Focus Engine](https://i.imgur.com/OjANvN9.png)
=======

Welcome to the Focus Engine source code repository!

Focus is an open-source C# game engine for realistic rendering and VR based off of Xenko. You'll still see "Xenko" in many places.
The engine is highly modular and aims at giving game makers more flexibility in their development.
Xenko comes with an editor that allows you create and manage the content of your games or applications in a visual and intuitive way.

![Focus Game Studio](https://xenko.com/images/external/script-editor.png)

To learn more about Xenko, visit [xenko.com](https://xenko.com/).

## License

Focus is covered by [MIT](LICENSE.md), unless stated otherwise (i.e. for some files that are copied from other projects).

You can find the list of third party projects [here](THIRD%20PARTY.md).

Contributors need to sign the following [Contribution License Agreement](docs/ContributorLicenseAgreement.md).

## Documentation

Find explanations and information about Xenko:
* [Xenko Manual](https://doc.xenko.com/latest/manual/index.html)
* [API Reference](https://doc.xenko.com/latest/api/index.html)
* [Release Notes](https://doc.xenko.com/latest/ReleaseNotes/index.html)

## Community

Ask for help or report issues:
* [Chat with the community on Discord](https://discord.gg/f6aerfE) [![Join the chat at https://discord.gg/f6aerfE](https://img.shields.io/discord/500285081265635328.svg?style=flat&logo=discord&label=discord)](https://discord.gg/f6aerfE)
* [Discuss topics on our forums](http://forums.xenko.com/)
* [Report engine issues](https://github.com/xenko3d/xenko/issues)
* [Donate to support the project](https://www.patreon.com/phr00tssoftware)

## Building from source

### Prerequisites

1. [Git](https://git-scm.com/downloads) (recent version that includes LFS, or install [Git LFS](https://git-lfs.github.com/) separately).
2. [Visual Studio 2019](https://www.visualstudio.com/downloads/) with the following workloads:
  * `.NET desktop development` with `.NET Framework 4.8 targeting pack`
  * `Desktop development with C++` with `Windows 10 SDK (10.0.17763.0)` or later, and `VC++ 2019 version 15.9 v14.16 latest v141 tools` or later (both should be enabled by default)
  * `.NET Core cross-platform development`
  * Optional (to target UWP): `Universal Windows Platform development` with `Windows 10 SDK (10.0.17763.0)`
  * Optional (to target iOS/Android): `Mobile development with .NET` and `Android NDK R13B+` individual component
3. [FBX SDK 2019.0 VS2015](https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2019-0)

### Build Focus

1. Clone Focus: `git clone https://github.com/phr00t/FocusEngine.git`
2. Run `<FocusDir>\build\Xenko.PCPlatforms.bat`, which starts Visual Studio 2019, and build.

### Contribution Guidelines

Please check our [Contributing Guidelines](docs/CONTRIBUTING.md).
