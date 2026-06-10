# Agent template reference

Agent templates are `.md` files with `[tag:value]` directives, `{$variables}`, and LLM tools. Empty lines are ignored.

---

## Variables

Substituted at runtime via `{$name}` or `$name`.

| Variable | Description |
|---|---|
| `$workingPath` | Agent working directory |
| `$datetime` | Current date/time (`yyyy-MM-dd HH:mm:ss`) |
| `$prompt` | Last user input from `do:prompt()` |
| `$completion` | Last assistant text from `do:turn()` |

**Example**

```markdown
[role:USER]
Search for: {$prompt}

[role:ASSISTANT]
Latest result: {$completion}
```

**Choose-only placeholder:** `[$history]` — replaced with serialized chat history inside `[choose:...]` blocks.

---

## Tags

Control flow and message building. Syntax: `[tag:value]`.

### `[model:...]`

Set LLM fallbacks. Tries each model in order on failure.

| Value | Format |
|---|---|
| Models | `provider@model`, separated by `\|` |

Providers: `ollama` (local), `openrouter` (uses `$env:OPENROUTER_API_KEY`).

**Example**

```markdown
[model:openrouter@anthropic/claude-sonnet-4|ollama@llama3]
```

---

### `[role:...]`

Start a new chat message for the given role. Text after this tag appends to that message until the next `[role:...]`.

| Value | `SYSTEM`, `ASSISTANT`, `USER`, `TOOL` |

**Example**

```markdown
[role:SYSTEM]
You are a helpful assistant.

[role:USER]
[do:prompt()]
```

---

### `[label:...]`

Mark a jump target. Does not emit content.

**Example**

```markdown
[label:start]
[role:USER]
[do:prompt()]
[goto:start]
```

---

### `[goto:...]`

Jump to a label.

| Param | Label name |

**Example**

```markdown
[goto:refine]
```

---

### `[do:...]`

Run a built-in function. Output is injected into the current message buffer.

See **Functions** below.

---

### `[tools:...]`

Enable LLM tools from this point onward (comma-separated). Built-in tool names and custom tool names are supported. Use `file('path.json')` to enable every tool defined in a JSON file (reloaded from disk each time the tool list is resolved for a completion).

**Example**

```markdown
[tools:file-read,file-write,shell]
[do:turn()]
```

```markdown
[tools:shell,file-read,file('agents/my-tools.json')]
[do:turn()]
```

---

### `[choose:...]`

Send the following text (until the next label) to the model without history. The engine appends `I should respond only with one word from; opt1, opt2` (using the declared options). Neither the choose prompt nor the model's one-word reply is added to chat history. Jump to the matching label if the response contains exactly one option (case-insensitive, spaces ignored). Retries up to 3 times.

| Value | Options separated by `\|` |

**Example**

```markdown
[choose:loop|done]
Reply with only one word based on: {$completion}
```

---

### `[shell:...]`

Run a shell command at template execution time (not during a turn). Command goes inside ` ``` ` fences. Supports variable substitution.

**Example**

```markdown
[shell:```git status```]
[shell:```si-search index.json search -q "$prompt" -k 5```]
```

---

### `[partial:...]`

Inline another `.md` file at parse time (relative to template or working path).

**Example**

```markdown
[partial:shared-instructions.md]
```

---

## Functions (`do:`)

Built-in commands invoked via `[do:command()]`.

### `prompt()`

Prompt the user and append their input to the current message. Sets `$prompt`.

| Params | none |

**Example**

```markdown
[role:USER]
[do:prompt()]
```

CLI: first call can use `--prompt "..."` instead of prompting.

---

### `turn()`

Run an LLM turn. Loops on tool calls until the model returns text only. Sets `$completion`.

| Params | none |

**Example**

```markdown
[role:ASSISTANT]
Summarize the notes above.
[do:turn()]
```

After a text-only completion, fenced blocks in the assistant response are executed automatically:

| Fence | Behavior |
|---|---|
| ` ```shell ` | Run command(s); print output on console; inject output into chat history as a USER message for the next turn; remove block from stored assistant text |
| ` ```shell-silent ` | Run command(s); print output on console only; remove block from stored assistant text |

Output from `shell` blocks is injected once per turn — the model is **not** called again in the same turn. The next `[do:turn()]` sees the injected output.

This differs from `[shell:...]`, which runs at template execution time (before or between turns), and from the `shell` tool, which the model invokes during a turn.

**Example** (model output during a turn):

````markdown
Checking disk usage:

```shell
df -h
```
````

---

### `prompt_yesno(message, yes|no)`

Show a message and two choices. Jumps to the label matching the user's pick.

| Param | Description |
|---|---|
| `message` | Text inside ` ``` ` fences |
| `yes` | Label name if user picks first option |
| `no` | Label name if user picks second option |

**Example**

```markdown
[do:prompt_yesno(```Save changes?```save|discard)]
[label:save]
[role:ASSISTANT]
Saved.
[label:discard]
[role:ASSISTANT]
Discarded.
```

---

## Tools (LLM)

Called by the model during `do:turn()`. Enable with `[tools:...]`.

### `file-read`

Read a file.

| Param | Required | Description |
|---|---|---|
| `path` | yes | File path (`~` and relative paths supported) |
| `max_length` | no | Max characters to return |

```json
{ "path": "notes.md", "max_length": 4000 }
```

---

### `file-write`

Write a file (creates parent folders).

| Param | Required | Description |
|---|---|---|
| `path` | yes | File path |
| `content` | yes | Text to write |

```json
{ "path": "output/result.md", "content": "# Hello" }
```

---

### `file-search`

List files in a folder.

| Param | Required | Description |
|---|---|---|
| `folder` | yes | Directory to search |
| `filter` | no | Glob filter (default `*`) |
| `recurse` | no | Search subfolders (default `false`) |

```json
{ "folder": "src", "filter": "*.cs", "recurse": true }
```

---

### `shell`

Run a shell command and return output.

| Param | Required | Description |
|---|---|---|
| `command` | yes | Shell command string |

```json
{ "command": "git diff --stat" }
```

---

### `agent-handover`

End the current agent, clear history, and run another template.

| Param | Required | Description |
|---|---|---|
| `agent` | yes | Path to agent `.md` |
| `prompt` | yes | Sets child's `$prompt` (does not skip `do:prompt()`) |

```json
{ "agent": "task-bot.md", "prompt": "Fix the login bug" }
```

---

### `agent-delegate`

Run another template in an isolated session. Parent continues after the child finishes. Returns the child's last `$completion` as the tool result.

| Param | Required | Description |
|---|---|---|
| `agent` | yes | Path to agent `.md` |
| `prompt` | yes | Sets child's `$prompt` (does not skip `do:prompt()`) |

```json
{ "agent": "rpg-character-creator-skills.md", "prompt": "./player-characters/Kevin.md" }
```

---

### Custom tools (JSON)

Define shell-based tools in a JSON file and load them via `--tools` or `file('...')` in `[tools:...]`.

**CLI**

```bash
nagent agents/bot.md --tools agents/custom-tools.json
```

**JSON shape**

```json
[
  {
    "name": "echo-text",
    "description": "Echo text to stdout",
    "command": "echo $text",
    "parameters": [
      { "name": "text", "description": "the text to show" }
    ]
  }
]
```

| Field | Description |
|---|---|
| `name` | Tool id exposed to the LLM (must not match a built-in tool name) |
| `description` | Tool description sent to the model |
| `command` | Shell command template; `$name` placeholders are substituted at invoke time |
| `parameters` | List of `{ name, description }`; all are required string parameters |

**Substitution**

When a custom tool runs, `$name` tokens in `command` are replaced with shell-quoted values:

1. Tool parameters from the LLM call (e.g. `{ "text": "hello" }`)
2. Agent variables: `$workingPath`, `$datetime`, `$prompt`, `$completion`

If a parameter and an agent variable share a name, the tool parameter wins. Missing required parameters produce an error.

**Example file** — see [agents/custom-tools.json](agents/custom-tools.json).

---

## Minimal template

```markdown
[model:ollama@llama3]
[role:SYSTEM]
You help the user with tasks.

[role:USER]
[do:prompt()]

[role:ASSISTANT]
[tools:file-read,shell]
[do:turn()]
```
