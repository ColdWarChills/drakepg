---
name: advised
description: Run one task with the advisor model (/advisor) checking the approach, any failure that comes back, and the finished result, ending with how often it was asked. Use when the user types /advised <task>, or when a brief or agent definition asks for an advised run.
argument-hint: <task>
---

# Advised task

The task: $ARGUMENTS
(If that is blank, the task is the job in your prompt or brief.)

"The advisor" is Claude Code's advisor tool (`/advisor`): a stronger model that reads this whole conversation, every command and result included, each time you call it. It is not the game-design-advisor skill.

You answer to the user if they typed `/advised`, or to the orchestrator if a Fable session started you (as a subagent or a session). Every "say" below goes to that one.

## 0. Before starting (one line each, only when it applies)
- **No advisor tool in your tools?** Stop and say so. If the user typed `/advised`, tell them: run `/advisor fable`, then `/clear`, then `/advised` again. Claude Code drops a refused advisor without any error, so check the tool list, don't assume.
- **Too small?** One file, an obvious change, three steps or fewer: say so and do it as normal work, no advisor calls.
- **Hard at every step** (every move is a judgment call) and you are not Fable: say Fable on the whole job fits better (`/model fable`, or hand it back to the orchestrator), then carry on.
- **Chat already long with other work?** Every advisor call re-reads all of it with no caching. Say a fresh chat makes each call cheaper, then carry on. (Workers start fresh; skip this.)

## 1. Read first
Read the files the task touches, `docs/PLANS.md`, and the CLAUDE.md rules that apply, before planning. The advisor only knows what is in the conversation, so whatever it should judge must be read in first.

## 2. Ask the advisor at these three points
1. **Before choosing an approach.** Write the plan in a few lines (what gets made, which files, how it will be checked), then call the advisor.
2. **When the same failure comes back twice** (same error, same failing test, same wrong picture after a fix). Call it before the third try.
3. **Before saying it is done.** First check the result the way this repo asks (typecheck, `npm run sim`, tests, a screenshot), then call the advisor with that proof in the conversation. For anything the designer will look at, ask it whether the result clearly beats what is in the game now.

No calls for the routine steps in between.

After each call, write one line: `Advisor: <what it changed in the plan>` (or `no change`). If the advice clashes with what the files or tests show, go with the evidence and say so in that line.

## 3. Finish
End with these short lines:
- **Changed:** the files, as links.
- **Checked:** how, and what it showed.
- **Open doubts:** what was not checked, and any advice you did not follow.
- **Advisor calls:** how many, at which points, and the final check's verdict. Zero means you paid for an advisor that never spoke; say why.

Then follow the repo's finish rules (CLAUDE.md). A worker leaves commits to the orchestrator.
