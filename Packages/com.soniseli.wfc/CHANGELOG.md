# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Package skeleton: `WfcStudio.Core` (engine-free solver assembly), `WfcStudio.Runtime` (Unity integration) and the `WfcStudio.Tests` EditMode test assembly.
- Architecture tests that keep `WfcStudio.Core` free of `UnityEngine`/`UnityEditor` and `WfcStudio.Runtime` free of `UnityEditor`.
