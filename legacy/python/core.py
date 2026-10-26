"""Safe, local command builders for restoring Apple-authorized IPA packages."""

from __future__ import annotations

import importlib.util
import os
from pathlib import Path
import re
import shutil
import sys


APPLE_LINK = re.compile(r"(?:apps\.apple\.com/[^\s?#]*?/)id(\d+)", re.IGNORECASE)


def parse_app_id(value: str) -> str:
    """Accept a numeric App Store ID or an apps.apple.com link."""
    value = value.strip()
    if re.fullmatch(r"\d{5,20}", value):
        return value
    found = APPLE_LINK.search(value)
    if found:
        return found.group(1)
    raise ValueError("Введите цифровой App Store ID или ссылку apps.apple.com/.../id123456789")


def find_ipatool() -> str | None:
    """Find official ipatool binary on PATH or next to this program."""
    found = shutil.which("ipatool")
    if found:
        return found
    here = Path(__file__).resolve().parent
    # The prototype lives in legacy/python; also look in the repository root.
    for root in (here, here.parent.parent):
        for candidate in ("ipatool.exe", "ipatool"):
            p = root / candidate
            if p.is_file() and (os.name == "nt" or os.access(p, os.X_OK)):
                return str(p)
        for p in sorted(root.glob("ipatool-*")):
            if p.is_file() and p.suffix not in (".zip", ".tar", ".gz", ".txt") and (os.name == "nt" or os.access(p, os.X_OK)):
                return str(p)
    return None


def has_mobiledevice() -> bool:
    return importlib.util.find_spec("pymobiledevice3") is not None


def device_list_cmd() -> list[str]:
    return [sys.executable, "-m", "pymobiledevice3", "usbmux", "list"]


def download_cmd(ipatool: str, app_id: str, dest: Path) -> list[str]:
    # No shell=True: untrusted user input is never interpreted as shell syntax.
    return [ipatool, "download", "--app-id", parse_app_id(app_id), "--output", str(dest)]


def install_cmd(ipa_file: Path, udid: str = "") -> list[str]:
    if not ipa_file.is_file() or ipa_file.suffix.lower() != ".ipa":
        raise ValueError("Выберите существующий файл .ipa")
    cmd = [sys.executable, "-m", "pymobiledevice3", "apps", "install"]
    if udid.strip():
        if not re.fullmatch(r"[a-zA-Z0-9-]{8,80}", udid.strip()):
            raise ValueError("Неверный UDID. Скопируйте его из результата проверки iPhone.")
        cmd += ["--udid", udid.strip()]
    return cmd + [str(ipa_file)]
