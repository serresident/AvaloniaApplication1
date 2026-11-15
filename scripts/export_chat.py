#!/usr/bin/env python3
"""
Скрипт экспорта текущего чата в документацию проекта.
Копирует полный JSONL-лог и формирует удобочитаемый Markdown-файл истории диалога.
"""

import os
import re
import json
import shutil
from datetime import datetime

CONVERSATION_ID = "fb44155f-1d11-4c8c-b777-9870b9e1d8ca"
APP_DATA_DIR = os.path.expanduser(r"~/.gemini/antigravity")
TRANSCRIPT_SRC = os.path.join(APP_DATA_DIR, "brain", CONVERSATION_ID, ".system_generated", "logs", "transcript_full.jsonl")

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOCS_DIR = os.path.join(ROOT_DIR, "docs", "chat_history")
TRANSCRIPT_DST = os.path.join(DOCS_DIR, "transcript_full.jsonl")
MARKDOWN_DST = os.path.join(DOCS_DIR, "chat_history.md")

def clean_user_content(raw: str) -> str:
    m = re.search(r"<USER_REQUEST>(.*?)</USER_REQUEST>", raw, re.DOTALL)
    if m:
        text = m.group(1).strip()
    else:
        text = raw.strip()
    # Strip slash command system injections if present
    text = re.sub(r"<ORCHESTRATOR>.*?</ORCHESTRATOR>", "", text, flags=re.DOTALL).strip()
    text = re.sub(r"<ADDITIONAL_METADATA>.*?</ADDITIONAL_METADATA>", "", text, flags=re.DOTALL).strip()
    return text

def export_chat():
    if not os.path.exists(TRANSCRIPT_SRC):
        print(f"Error: Transcript source not found at {TRANSCRIPT_SRC}")
        return

    os.makedirs(DOCS_DIR, exist_ok=True)

    # 1. Копирование сырого JSONL
    shutil.copy2(TRANSCRIPT_SRC, TRANSCRIPT_DST)
    print(f"Copied raw transcript to: {TRANSCRIPT_DST}")

    # 2. Формирование Markdown
    md_lines = [
        "# История диалога разработки (Chat History)",
        "",
        f"* **ID сессии:** `{CONVERSATION_ID}`",
        f"* **Дата выгрузки:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"* **Исходный JSONL лог:** [transcript_full.jsonl](transcript_full.jsonl)",
        "",
        "---",
        ""
    ]

    turn_idx = 1
    with open(TRANSCRIPT_SRC, "r", encoding="utf-8") as f:
        for line in f:
            if not line.strip():
                continue
            try:
                data = json.loads(line)
            except Exception:
                continue

            src = data.get("source")
            tp = data.get("type")
            ts = data.get("created_at", "")
            content = data.get("content", "")
            tool_calls = data.get("tool_calls", [])

            if src == "USER_EXPLICIT" and tp == "USER_INPUT":
                user_text = clean_user_content(content)
                if not user_text:
                    continue
                md_lines.append(f"## 👤 Запрос #{turn_idx} — {ts}")
                md_lines.append("")
                md_lines.append(user_text)
                md_lines.append("")
                turn_idx += 1

            elif src == "MODEL" and tp == "PLANNER_RESPONSE":
                if content:
                    md_lines.append(f"### 🤖 Ответ ассистента — {ts}")
                    md_lines.append("")
                    md_lines.append(content)
                    md_lines.append("")
                    md_lines.append("---")
                    md_lines.append("")
                elif tool_calls:
                    names = [call.get("name", "tool") for call in tool_calls]
                    md_lines.append(f"*⚡ Действие ассистента [{ts}]: запуск инструментов: `{', '.join(names)}`*")
                    md_lines.append("")

    with open(MARKDOWN_DST, "w", encoding="utf-8") as out:
        out.write("\n".join(md_lines))

    print(f"Generated Markdown history at: {MARKDOWN_DST}")

if __name__ == "__main__":
    export_chat()
