@echo off
rem Ask Gemini to do what a brief file says, read-only, through Google's Antigravity CLI (agy).
rem Gemini CLI no longer serves personal Google accounts (2026-06-18); agy does. Signed in on the designer's PC 2026-11-26.
rem
rem Usage:  scripts\gemini.cmd <path\to\brief.md> [model]
rem   model: an id from "agy models" (default gemini-4-neon-high). agy also serves Claude models, so always pass a Gemini one.
rem   Writes next to the brief: <name>.gemini.json (the "response" field is the answer), <name>.gemini_err.txt,
rem   and <name>.agy_log.txt (names the model that answered; it holds the account email, so never commit it).
rem
rem Claude's own shell is sandboxed and can't see agy or its login: run this through the terminal panel
rem (run_in_terminal), then Read the .json. Keep briefs outside the repo (the scratchpad). agy runs in plan mode and
rem sandboxed, from the brief's folder; its Unity MCP server is disabled (agy mcp disable unityMCP).
setlocal
if "%~1"=="" (
  echo Usage: scripts\gemini.cmd ^<brief.md^> [model]
  exit /b 1
)
set "MODEL=%~2"
if "%MODEL%"=="" set "MODEL=gemini-4-neon-high"
cd /d "%~dp1"
"%LOCALAPPDATA%\agy\bin\agy.exe" --print="Read the file %~nx1 in the current folder and follow it exactly. Write no files and change nothing. Reply with only what it asks for." --model %MODEL% --mode plan --sandbox --output-format json --print-timeout 600s --log-file "%~n1.agy_log.txt" > "%~n1.gemini.json" 2> "%~n1.gemini_err.txt"
echo done, exit %ERRORLEVEL%
