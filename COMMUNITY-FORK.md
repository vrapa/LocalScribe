# LocalScribe community fork by vrapa

This branch is a small, Windows-focused hardening fork of
[imnotwallace/LocalScribe](https://github.com/imnotwallace/LocalScribe). It keeps the upstream MIT
licence and authorship. It is not an official upstream release.

## Why this fork exists

The immediate goal is dependable local Czech and English transcription of work calls on Windows,
with microphone and remote audio kept as separate transcript sources. The changes are deliberately
small enough to review and offer upstream later.

## Changes from upstream 0.9.2

- LiveRunner waits for asynchronous finalization and verifies persisted session metadata before it
  reports success.
- LiveRunner and OfflineRunner accept isolated settings, output-root, and language overrides for
  reproducible tests.
- Automatic model selection correctly uses installed multilingual Whisper models for Czech and
  other non-English languages.
- Settings exposes the existing `keep` / `never` audio-retention policy for new sessions.

## What has been tested

On Windows 11 with an Intel Core i5-9400 (CPU transcription, no CUDA):

- the 1,414-test model-free Core suite passes;
- a Czech 26.6-second system-audio smoke produced three live segments and a fully finalized session;
- microphone and remote capture paths create independent audio sources;
- `audioRetention=never` avoids retaining session FLAC files;
- the MCP stdio server initializes, lists its tools, and returns sessions.

Slack and Discord per-process targets can be activated, but a release should not claim end-to-end
call reliability until known audio has been played by each application and the resulting remote
segments have been inspected. Czech recognition with the quantized `base` model is useful but not
perfect; `small-q8_0` is the recommended next accuracy test on this CPU.

## Security and privacy

The fork does not add telemetry or cloud transcription. Release binaries are unsigned. Verify the
published SHA-256 file before running a downloaded archive. MCP is local stdio and exposes stored
transcripts to whichever client launches it, so configure it only in clients you trust.

## Upstreaming

The preferred long-term outcome is to send focused pull requests upstream. This repository remains
useful as a tested Windows/Czech release channel while those changes are reviewed.
