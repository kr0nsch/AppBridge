# AppBridge — Python prototype (unmaintained)

This is the original cross-platform proof of concept (Tkinter UI wrapping `ipatool` and
`pymobiledevice3`). It is kept for reference only. The supported app is the Windows app in
[`src/AppBridge`](../../src/AppBridge).

To try it: install Python 3.12 and `pip install pymobiledevice3`, place `ipatool` next to
`app.py` or on `PATH`, then run `python app.py` (or `start_windows.bat`).
Tests: `python -m unittest test_core`.
