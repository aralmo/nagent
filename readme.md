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
customagent --resume <sessionId>
```

Completions are streamed. If the template ends without a loop, the session exits (no extra prompts).

## Session persistence

Each run is assigned a session GUID. State is checkpointed after each prompt, turn, choose, and handover. On exit, the CLI prints:

```
Session: <guid>
Resume with: customagent --resume <guid>
```

Session snapshots are stored at `<workingPath>/sessions/<guid>.json`. A global index under `%LOCALAPPDATA%/customagent/session-index/` maps session IDs to snapshot paths so `--resume` needs no other arguments — template, tools, working directory, and conversation state are restored from the snapshot.

## Logging

Each conversation writes a JSON Lines log under `<workingPath>/logs/` with requests, tool calls, and responses. Resumed sessions append to the same log file. Delegation and handover rotate to separate log sessions.
