# Third-party notices

LocalScribe itself is distributed under the [MIT licence](LICENSE), with the upstream copyright
notice preserved. The portable preview also redistributes third-party binaries and model weights.
This file is a practical attribution and licence index; the authoritative licence text is the one
published by each named project and included in its source/package.

## Bundled model weights

| Component | Licence | Source |
|---|---|---|
| OpenAI Whisper `base` and `small` model weights, converted to ggml/q8_0 | MIT | [openai/whisper](https://github.com/openai/whisper) |
| Silero VAD ONNX model | MIT | [snakers4/silero-vad](https://github.com/snakers4/silero-vad) |

OpenAI states that both Whisper code and model weights are released under MIT. Silero VAD publishes
its code and models under MIT. The preview does not contain Qwen, EmbeddingGemma, pyannote,
3D-Speaker, or FFmpeg assets.

## Principal redistributed libraries

| Component | Version | Licence |
|---|---:|---|
| .NET runtime / Windows Desktop runtime | 10.0.x | MIT and bundled Microsoft third-party notices |
| WPF-UI | 4.0.3 | MIT |
| Velopack | 0.0.1298 | MIT |
| H.NotifyIcon.Wpf | 2.3.0 | MIT |
| CommunityToolkit.Mvvm | 8.4.0 | MIT |
| Whisper.net and native runtimes | 1.9.1 | MIT |
| NAudio | 2.2.1 | MIT |
| Microsoft.ML.OnnxRuntime | 1.22.0 | MIT, plus its third-party notices |
| DocumentFormat.OpenXml | 3.5.1 | MIT |
| CUETools.Codecs.FLAKE | 1.0.5 | LGPL-3.0 |
| ModelContextProtocol C# SDK | 2.0.0-rc.1 | Apache-2.0 |

CUETools.Codecs.FLAKE is shipped as a separate dynamically linked DLL. Recipients may replace that
DLL with a compatible modified build; this fork does not statically link it or prevent relinking.
The LGPL-3.0 text and the larger runtime third-party notice files are included in the binary archive's
`licenses` directory.

## Fork attribution

Upstream project: [imnotwallace/LocalScribe](https://github.com/imnotwallace/LocalScribe).
Community changes: [COMMUNITY-FORK.md](COMMUNITY-FORK.md).

This notice is not legal advice. If the distribution grows to include the optional assistant,
semantic-search, diarization, or FFmpeg components, their corresponding licence texts and notices
must be added to the release package before publication.
