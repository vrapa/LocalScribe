# Portable live-transcription release

This unsigned `win-x64` preview is self-contained: Windows does not need a separate .NET install.
Extract the archive, then run `app\LocalScribe.App.exe`. Windows SmartScreen may warn because the
binary is not code-signed; verify the archive against `SHA256SUMS.txt` before running it.

The archive includes multilingual Whisper `base-q8_0` and `small-q8_0`, Silero VAD, and the
LocalScribe MCP stdio server at `app\mcp\LocalScribe.Mcp.exe`.

## Intended use

- live microphone plus system/per-process call capture;
- Czech and English transcription on a Windows x64 CPU;
- transcript browsing and local MCP access.

## Preview limitations

This smaller preview does not include FFmpeg, the diarization helper, assistant weights, or the
component-download helper. Importing media, Split Speakers, Assistant/Summary generation, semantic
search, and in-app component downloads are therefore not release-tested in this archive. Stored
transcripts and the read/search MCP tools work without those optional components.

The archive is portable rather than installed: it does not add Start-menu entries, file
associations, services, or registry keys. Settings remain in `%APPDATA%\LocalScribe` and sessions in
the configured storage root.
