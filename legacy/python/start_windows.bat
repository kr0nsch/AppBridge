@echo off
cd /d "%~dp0"
if exist "..\..\.venv\Scripts\python.exe" (
  "..\..\.venv\Scripts\python.exe" -X utf8 app.py
) else (
  py -3.12 -X utf8 app.py
)
if errorlevel 1 (
  echo.
  echo Make sure Python 3.12 is installed with tkinter support.
  pause
)
