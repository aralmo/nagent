# Custom Agents

Prompt-and-control-flow templates for LLM agents. See **[reference.md](reference.md)** for variables, tags, functions, and tools.

## Quick start

```bash
customagent agents/bot.md
customagent agents/bot.md --prompt "initial user message"
customagent agents/bot.md --tools agents/example-tools.json
```

## How it works

Templates are processed top-to-bottom. Messages accumulate in chat history. `do:prompt()` and `do:turn()` drive the conversation; tags like `[choose:...]`, `[goto:...]`, and `[tools:...]` control flow and capabilities.

## Project layout

- `agents/` — agent templates (`.md`)
- `src/` — .NET library and CLI
- `publish/` — published `customagent` executable

## CLI

```
customagent <template.md> [--prompt <initial prompt>] [--tools <tools.json>]...
```

Completions are streamed. If the template ends without a loop, the session exits (no extra prompts).

## Logging

Each conversation writes a JSON Lines log under `<workingPath>/logs/` with requests, tool calls, and responses. Delegation and handover rotate to separate log sessions.
