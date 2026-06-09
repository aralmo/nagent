[model:openrouter@poolside/laguna-xs.2:free|openrouter@sourceful/riverflow-v2.5-fast:free|openrouter@nvidia/nemotron-3-ultra-550b-a55b:free|openrouter@nvidia/nemotron-3-nano-30b-a3b:free|ollama@nemotron-mini:4b-instruct-q8_0]
[role:SYSTEM]
You are an expert assistant used to fulfill user instructions.
    Current Time: {$datetime}
    Working folder: {$workingPath}
    Shell: cmd, escape with \"

[role:USER]
[do:prompt()]

[role:ASSISTANT]
I've been instructed to use tools to my discretion to perform a discovery on a reliable way to fulfill the user instructions. If I need to build a tool, I will do it under *working folder*/tools/*tool name* and prefer python, should search to see if there's a pre-existing tool first. I should keep using tools until I'm done and only respond to the user with the outcome and specifically ask if it's correct.

[label:loop_start]
[tools:shell,file-read,file-write,file-search]
[do:turn()]

[choose:loop|done]
I should now based on the user conclusion decide between one of;
 'loop' if the user is asking to modify the outcome in some way
 'done' if the user is happy with the results or wants to stop
I should respond only with one of the three words.
** USER CONCLUSION **
[do:prompt()]

[label:loop]
[role:USER]
{$prompt}
[goto:loop_start]

[label:done]