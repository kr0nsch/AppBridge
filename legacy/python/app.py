"""FreeReturn: a local graphical wrapper for ipatool + pymobiledevice3.

This app NEVER requests, stores, transmits, or logs Apple Account credentials.
Apple authentication is handled by ipatool in a separate OS terminal.
"""
from __future__ import annotations

import os
from pathlib import Path
import queue
import shlex
import subprocess
import sys
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import webbrowser

from core import (
    device_list_cmd,
    download_cmd,
    find_ipatool,
    has_mobiledevice,
    install_cmd,
    parse_app_id,
)

ROOT = Path(__file__).resolve().parent
DEFAULT_FOLDER = Path.home() / "Downloads" / "FreeReturn"
IPATOOL_URL = "https://github.com/majd/ipatool/releases/latest"
MOBILE_URL = "https://github.com/doronz88/pymobiledevice3"


class FreeReturn(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("FreeReturn — приложения на iPhone без оплаты")
        self.geometry("790x730")
        self.minsize(680, 630)
        self.configure(bg="#f5f7fb")
        self.events: queue.Queue = queue.Queue()
        self.busy = False
        self.app_id = tk.StringVar()
        self.ipa_file = tk.StringVar()
        self.udid = tk.StringVar()
        self.status = tk.StringVar(value="Готово к работе")
        self._setup_style()
        self._layout()
        self.after(125, self._drain_events)
        self.after(200, self._first_check)

    def _setup_style(self) -> None:
        s = ttk.Style(self)
        if "clam" in s.theme_names():
            s.theme_use("clam")
        s.configure("TFrame", background="#f5f7fb")
        s.configure("Card.TFrame", background="#ffffff")
        s.configure("TLabel", background="#f5f7fb", foreground="#19263c", font=("Arial", 11))
        s.configure("Heading.TLabel", font=("Arial", 22, "bold"), foreground="#19263c")
        s.configure("Sub.TLabel", font=("Arial", 10), foreground="#526078")
        s.configure("Card.TLabel", background="#ffffff", font=("Arial", 11), foreground="#19263c")
        s.configure("CardTitle.TLabel", background="#ffffff", font=("Arial", 13, "bold"), foreground="#19263c")
        s.configure("TButton", font=("Arial", 10), padding=(11, 9))
        s.configure("Primary.TButton", background="#3765df", foreground="white", font=("Arial", 10, "bold"))
        s.map("Primary.TButton", background=[("active", "#244fc2"), ("disabled", "#98aad4")])
        s.configure("TEntry", padding=8)

    def _layout(self) -> None:
        holder = ttk.Frame(self, padding=(25, 19))
        holder.pack(fill="both", expand=True)
        ttk.Label(holder, text="FreeReturn", style="Heading.TLabel").pack(anchor="w")
        ttk.Label(holder, text="Бесплатное восстановление приложений из Apple Account · Mac / Windows · USB", style="Sub.TLabel").pack(anchor="w", pady=(0, 16))
        self._card(holder, "1   Подготовка", self._setup_card)
        self._card(holder, "2   Скачать оригинальный IPA", self._download_card)
        self._card(holder, "3   Установить на iPhone", self._install_card)

        footer = ttk.Frame(holder)
        footer.pack(fill="both", expand=True, pady=(15, 0))
        ttk.Label(footer, textvariable=self.status, style="Sub.TLabel").pack(anchor="w")
        logbox = ttk.Frame(footer)
        logbox.pack(fill="both", expand=True, pady=(6, 0))
        self.log = tk.Text(logbox, height=9, wrap="word", font=("Consolas" if os.name == "nt" else "Menlo", 10), state="disabled", relief="flat", bg="#17253a", fg="#e2ebff", padx=12, pady=10)
        self.log.pack(side="left", fill="both", expand=True)
        bar = ttk.Scrollbar(logbox, command=self.log.yview)
        bar.pack(side="right", fill="y")
        self.log.configure(yscrollcommand=bar.set)
        ttk.Label(holder, text="Только для приложений, доступных вашему Apple Account. Защиту Apple программа не обходит.", style="Sub.TLabel").pack(anchor="w", pady=(9, 0))

    def _card(self, parent, title: str, builder) -> None:
        frame = ttk.Frame(parent, style="Card.TFrame", padding=(15, 10))
        frame.pack(fill="x", pady=(0, 10))
        ttk.Label(frame, text=title, style="CardTitle.TLabel").pack(anchor="w", pady=(0, 8))
        builder(frame)

    def _setup_card(self, f) -> None:
        buttons = ttk.Frame(f, style="Card.TFrame")
        buttons.pack(fill="x")
        ttk.Button(buttons, text="Проверить iPhone", command=self.check_phone).pack(side="left", padx=(0, 7))
        ttk.Button(buttons, text="Войти в Apple Account", command=self.open_login).pack(side="left", padx=(0, 7))
        ttk.Button(buttons, text="Где взять инструменты?", command=self.show_tools).pack(side="left")
        ttk.Label(f, text="Подключите разблокированный iPhone кабелем и нажмите «Доверять» на экране.", style="Card.TLabel").pack(anchor="w", pady=(8, 0))

    def _download_card(self, f) -> None:
        line = ttk.Frame(f, style="Card.TFrame")
        line.pack(fill="x")
        ttk.Label(line, text="App Store ID или ссылка:", style="Card.TLabel").pack(side="left", padx=(0, 9))
        ttk.Entry(line, textvariable=self.app_id).pack(side="left", fill="x", expand=True)
        ttk.Button(line, text="Скачать IPA", style="Primary.TButton", command=self.download).pack(side="left", padx=(8, 0))
        ttk.Label(f, text="Подходит ссылка вида apps.apple.com/.../id123456789 или только её цифровой ID.", style="Card.TLabel").pack(anchor="w", pady=(8, 0))

    def _install_card(self, f) -> None:
        line = ttk.Frame(f, style="Card.TFrame")
        line.pack(fill="x")
        ttk.Entry(line, textvariable=self.ipa_file).pack(side="left", fill="x", expand=True)
        ttk.Button(line, text="Выбрать IPA", command=self.pick_ipa).pack(side="left", padx=(8, 0))
        line2 = ttk.Frame(f, style="Card.TFrame")
        line2.pack(fill="x", pady=(9, 0))
        ttk.Label(line2, text="UDID (необязательно):", style="Card.TLabel").pack(side="left", padx=(0, 9))
        ttk.Entry(line2, textvariable=self.udid).pack(side="left", fill="x", expand=True)
        ttk.Button(line2, text="Установить", style="Primary.TButton", command=self.install).pack(side="left", padx=(8, 0))

    def _write(self, msg: str) -> None:
        self.log.configure(state="normal")
        self.log.insert("end", msg.rstrip() + "\n")
        self.log.see("end")
        self.log.configure(state="disabled")

    def _first_check(self) -> None:
        ipa = find_ipatool()
        self._write("ipatool: " + ("найден" if ipa else "не найден — скачайте с GitHub"))
        self._write("pymobiledevice3: " + ("установлен" if has_mobiledevice() else "не найден — установите через pip"))
        self._write("Пароль и коды Apple Account вводятся только в отдельном терминале ipatool.")

    def show_tools(self) -> None:
        messagebox.showinfo("Что установить", "1. Скачайте ipatool (ссылка откроется в браузере), положите исполняемый файл рядом с app.py или добавьте в PATH.\n\n2. В терминале выполните:\npython -m pip install -U pymobiledevice3\n\n3. На Windows установите Apple Devices из Microsoft Store.\n\nПосле этого перезапустите FreeReturn.")
        webbrowser.open(IPATOOL_URL)

    def _run(self, label: str, args: list[str], on_success=None) -> None:
        if self.busy:
            messagebox.showwarning("Операция идёт", "Дождитесь окончания текущей операции.")
            return
        self.busy = True
        self.status.set(label + "…")
        self._write("▶ " + label)

        def work():
            try:
                p = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace", bufsize=1)
                assert p.stdout is not None
                for line in p.stdout:
                    self.events.put(("log", line.rstrip()))
                result = p.wait()
                self.events.put(("done", (label, result, on_success)))
            except Exception as exc:
                self.events.put(("error", (label, str(exc))))

        threading.Thread(target=work, daemon=True).start()

    def _drain_events(self) -> None:
        try:
            while True:
                event, payload = self.events.get_nowait()
                if event == "log":
                    self._write(payload)
                elif event == "done":
                    label, code, callback = payload
                    self.busy = False
                    if code == 0:
                        self.status.set("Готово: " + label)
                        self._write("✓ Команда завершилась успешно")
                        if callback:
                            callback()
                    else:
                        self.status.set("Ошибка: " + label)
                        self._write(f"✗ Ошибка (код {code}). Смотрите сообщение выше.")
                elif event == "error":
                    label, reason = payload
                    self.busy = False
                    self.status.set("Ошибка: " + label)
                    self._write("✗ " + reason)
        except queue.Empty:
            pass
        self.after(125, self._drain_events)

    def check_phone(self) -> None:
        if not has_mobiledevice():
            messagebox.showerror("Не найдено", "Сначала установите pymobiledevice3: python -m pip install -U pymobiledevice3")
            return
        self._run("Проверка iPhone по USB", device_list_cmd())

    def open_login(self) -> None:
        ipatool = find_ipatool()
        if not ipatool:
            messagebox.showerror("Не найден ipatool", "Скачайте ipatool по кнопке «Где взять инструменты?». ")
            return
        self._write("Вход Apple Account откроется в отдельном терминале. FreeReturn не получает пароль.")
        if sys.platform == "darwin":
            command = shlex.quote(ipatool) + " auth login"
            command = command.replace("\\", "\\\\").replace('"', '\\"')
            subprocess.Popen(["osascript", "-e", f'tell application "Terminal" to do script "{command}"'])
        elif os.name == "nt":
            command = subprocess.list2cmdline([ipatool, "auth", "login"])
            subprocess.Popen(["cmd.exe", "/k", command], creationflags=subprocess.CREATE_NEW_CONSOLE)
        else:
            messagebox.showinfo("Команда входа", f"Выполните в терминале:\n{shlex.quote(ipatool)} auth login")

    def download(self) -> None:
        ipatool = find_ipatool()
        if not ipatool:
            messagebox.showerror("Не найден ipatool", "Установите ipatool с GitHub.")
            return
        try:
            app_id = parse_app_id(self.app_id.get())
            DEFAULT_FOLDER.mkdir(parents=True, exist_ok=True)
            target = DEFAULT_FOLDER / f"{app_id}.ipa"
            if target.exists() and not messagebox.askyesno("Файл уже есть", "Скачать IPA заново и заменить существующий файл?"):
                self.ipa_file.set(str(target))
                return
        except Exception as exc:
            messagebox.showerror("Проверьте ID", str(exc))
            return

        def downloaded():
            if target.is_file() and target.stat().st_size > 0:
                self.ipa_file.set(str(target))
                self._write(f"IPA сохранён: {target}")
            else:
                self._write("Файл IPA не найден. Проверьте сообщение ipatool выше.")

        self._run("Загрузка оригинального IPA", download_cmd(ipatool, app_id, target), downloaded)

    def pick_ipa(self) -> None:
        file = filedialog.askopenfilename(title="Выберите файл IPA", filetypes=[("IPA-файлы", "*.ipa")])
        if file:
            self.ipa_file.set(file)

    def install(self) -> None:
        if not has_mobiledevice():
            messagebox.showerror("Не найден pymobiledevice3", "Установите pymobiledevice3 по инструкции.")
            return
        try:
            cmd = install_cmd(Path(self.ipa_file.get()), self.udid.get())
        except Exception as exc:
            messagebox.showerror("Ошибка", str(exc))
            return
        if not messagebox.askyesno("Установить IPA", "Убедитесь, что у вас есть резервная копия важных данных.\n\nУстановить этот IPA на подключённый iPhone?"):
            return
        self._run("Установка IPA на iPhone", cmd)


if __name__ == "__main__":
    FreeReturn().mainloop()
