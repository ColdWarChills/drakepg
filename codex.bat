@echo off
rem Double-click to open the codex page (codex.html): starts the game's dev server and opens the page in the browser.
rem The page needs the server to read the game data and save notes; closing this window stops it.
title drakepg codex - close this window when you're done
cd /d "%~dp0"
npm run dev -- --open /codex.html
