# Changelog

All notable changes to this package are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-09-27

First public release.

### Added

- Setup tab: GitHub sign-in (Git Credential Manager login or OAuth device flow), private
  repository create/pick, iOS target switch, and one-click install of the build workflow.
- Build tab: one-click cloud build (export, zip, upload, GitHub Actions macOS build, download)
  that resumes after domain reloads and editor restarts, with build history.
- Build-minutes panel with an estimate of free builds left and a confirmation before a build
  that would be billed.
- Install on iPhone: signs with a free Apple ID and installs over USB through the bundled
  sideload helper (built on isideload), with two-factor prompts in the window and a 7-day
  expiry reminder.
- Help tab with device setup, Developer Mode, trust and troubleshooting pages.
