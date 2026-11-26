@echo off
rem Ask Gemini to do what a brief file says, through Google's Antigravity CLI (agy). The work is in scripts\gemini.mjs.
rem Gemini CLI no longer serves personal Google accounts (2026-06-18); agy does. Signed in on the designer's PC 2026-11-26.
rem
rem Usage:  scripts\gemini.cmd <path\to\brief.md> [model]
rem   model: an id from "agy models" (default gemini-4-neon-high). agy also serves Claude models, so always pass a Gemini one.
rem   Writes next to the brief: <name>.gemini.txt (the answer), <name>.gemini.json, <name>.gemini_err.txt,
rem   and <name>.agy_log.txt (names the model that answered; it holds the account email, so never commit it).
rem
rem Claude's own shell is sandboxed and can't see agy or its login: run this through the terminal panel
rem (run_in_terminal), then Read the .gemini.txt. Keep briefs outside the repo (the scratchpad).
rem The brief goes inside the message and Gemini is told to use no tools. agy runs sandboxed, and its tool permission is
rem "request-review", so an edit would wait for an approval that never comes. Never add --mode auto, accept-edits or
rem --dangerously-skip-permissions. The Unity MCP server is disabled in agy (agy mcp disable unityMCP).
node "%~dp0gemini.mjs" %*
