# GitHub Copilot Telemetry

GitHub Copilot telemetry support is disabled by default.

When enabled, costats reads local Copilot-related JSON, JSONL, and log files from configured roots and extracts only model IDs, timestamps, and token counters. It does not store prompt text, completion text, source files, chat messages, or raw telemetry lines.

Telemetry-derived usage stays on the local machine. costats does not send telemetry records to GitHub, OpenAI, OpenRouter, LiteLLM, or any other third party.

Leave `CopilotTelemetryEnabled` set to `false` to disable this feature.
