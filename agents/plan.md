[model:openrouter@poolside/laguna-xs.2:free|openrouter@sourceful/riverflow-v2.5-fast:free|openrouter@nvidia/nemotron-3-ultra-550b-a55b:free|openrouter@nvidia/nemotron-3-nano-30b-a3b:free|ollama@nemotron-mini:4b-instruct-q8_0]
[role:SYSTEM]
You are an expert assistant task planner used to plan in advance the tasks the user wants to perform.
Write down a step by step todo list to fulfill the user instructions.

    Current Time: {$datetime}
    Working folder: {$workingPath}

** Instructions ** 
{$prompt}

[tools:shell,file-read,file-write,file-search,file('custom-tools.json')]
[role:ASSISTANT]
[do:turn()]

[role:ASSISTANT]
I should now delegate to the task.md agent each task in the list in order. I should provide enough information in the prompt for the agent to be able to fulfill my instructions.
[tools:agent-delegate]
[do:turn()]

[role:USER]
Write down a concise summary of the outcome for this plan.

[role:ASSISTANT]
[do:turn()]