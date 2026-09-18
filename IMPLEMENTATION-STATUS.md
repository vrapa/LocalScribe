# LocalScribe fork implementation status

Updated: 2026-09-18

Branch: `localscribe-cs-live`, based on upstream `a6eba76fda7e80145014457ba974998a05d5038a`.

## Current priority

Produce a usable and honestly tested Windows build for Czech/English Slack and Discord capture, then publish a clearly labelled community fork and unsigned release under the user's GitHub account.

## Completed

- Created an isolated Git worktree at `C:\Public\DotNet\RKVoxScribe\LocalScribe-fork`.
- Identified that the previous LiveRunner smoke exited before `SessionController.PendingFinalize`, so its incomplete metadata did not prove a GUI pipeline failure.
- Updated LiveRunner to wait with a bounded timeout, verify persisted `EndedAtUtc`, return a nonzero exit on finalization/error, and avoid printing `finalized` before persistence completes.
- Added isolated `--settings`, `--out`, and `--language` overrides to both runners so repeatable tests do not mutate the user's GUI configuration.
- Fixed automatic model selection for non-English sessions: Czech now selects installed multilingual `tiny`, `base`, or `small` weights instead of looking only for `.en` files.
- Exposed the existing `keep` / `never` audio-retention setting in the GUI, with an explicit warning that it affects new sessions only.
- Passed the complete model-free Core suite: 1,414 tests, 0 failures.
- Passed 45 focused Settings view-model tests and 51 selector/finalization tests.
- Ran the complete App suite: 1,169/1,171 passed. One failure is a pre-existing locale-sensitive assertion (`3.3 MB` versus Czech `3,3 MB`); the other timing-sensitive cancellation test passed immediately when repeated in isolation.
- Completed a 26.6-second Czech live system-mix smoke test on CPU with `base-q8_0`: three live transcript segments, separate local/remote FLAC files, persisted `endedAtUtc`, and exit code 0 after finalization.
- Verified `audioRetention=never` produces a transcript without retained FLAC audio.
- Verified the MCP server over stdio: initialization, six advertised tools, and `list_sessions` response all succeeded.
- Published runnable local outputs at `C:\Public\DotNet\RKVoxScribe\LocalScribe-fork-portable\app` and `C:\Public\DotNet\RKVoxScribe\LocalScribe-fork-portable\mcp`.

## Next

- Commit the validated changes and produce a versioned ZIP plus checksums.
- Add community-fork/release documentation without obscuring upstream authorship.
- Test known Slack and Discord playback through per-process capture when each app emits audio.
- Re-authenticate GitHub CLI, create the public fork under `vrapa`, push the branch, and publish the unsigned release.

## External blockers

- A real Slack and Discord audio/call test requires those applications to emit known audio while the smoke is running.
- GitHub CLI currently reports an invalid credential for account `vrapa`; re-authentication is needed only immediately before publishing.
