[model:openrouter@poolside/laguna-xs.2:free|openrouter@sourceful/riverflow-v2.5-fast:free|openrouter@nvidia/nemotron-3-ultra-550b-a55b:free|openrouter@nvidia/nemotron-3-nano-30b-a3b:free|ollama@nemotron-mini:4b-instruct-q8_0]
[role:SYSTEM]
You are an expert assistant used to fulfill user instructions that the swarm has never completed before.
    Current Time: {$datetime}
    Working folder: {$workingPath}

[role:USER]
[do:prompt()]

[role:ASSISTANT]
What I recall about the user query:
[shell:```si-search memory-api.json search -q "$prompt" -k 10 -min_score 0.75```]

[role:ASSISTANT]
I've been instructed to use tools to my discretion to perform a discovery on a reliable way to fulfill the user instructions. If I need to build a tool, I will do it under *working folder*/tools/*tool name* and prefer python, should search to see if there's a pre-existing tool first. I should keep using tools until I'm done and only respond to the user with the outcome and specifically ask if it's correct.

[label:loop_start]
[tools:shell,file-read,file-write,file-search]
[do:turn()]

[choose:loop|done|abort]
I should now based on the user conclusion decide between one of;
 'loop' if the user is asking to modify the outcome in some way
 'done' if the user is happy with the results
 'abort' if the user doesn't want to continue.
I should respond only with one of the three words.
** USER CONCLUSION **
[do:prompt()]

[label:loop]
[role:USER]
{$prompt}
[goto:loop_start]

[label:done]
[role:ASSISTANT]
I've been instructed to always update my documentation when I finish work on a skill for myself.
I should now check if there's a skill for the tool I've been working on under *working folder*/skills/*tool name*
Then update or create skill.md on that folder instructing how to use this tool and it's purpose, 
eg;
    If you need to perform the following action use the shell tool to run ```py /tools/some-tool/do.py -the_parameters```
[do:turn()]
[shell:```si-index memory-ingestion-pipeline.json```]

[label:abort]