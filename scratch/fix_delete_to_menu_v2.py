# -*- coding: utf-8 -*-
"""
Версия 2: фиксит line endings + правильно переносит delete в context menu.
Пишет файл в бинарном режиме чтобы избежать \r\r\n.
"""
import re

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

# Читаем в бинарном режиме
with open(file_path, 'rb') as f:
    raw = f.read()

# Убираем BOM если есть
if raw.startswith(b'\xef\xbb\xbf'):
    raw = raw[3:]

# Нормализуем: сначала убираем двойные \r\r\n -> \r\n, потом все \r\n -> \n
content = raw.decode('utf-8')
content = content.replace('\r\r\n', '\n')
content = content.replace('\r\n', '\n')
content = content.replace('\r', '\n')

lines = content.split('\n')
print(f"Исходных строк: {len(lines)}")

# ─── Шаг 1: В каждое ContextMenu после «Свойства» добавляем Удалить ───────────
new_lines = []
for line in lines:
    new_lines.append(line)
    # Строка с EditWidgetCommand — это пункт «Свойства»
    if 'EditWidgetCommand' in line and 'Header=' in line:
        indent = len(line) - len(line.lstrip())
        ind = ' ' * indent
        m = re.search(r'Command="\{Binding #WidgetsItemsControl\.\(\(vm:DashboardViewModel\)DataContext\)\.', line)
        if m:
            pfx = m.group(0)
            new_lines.append(ind + '<Separator />')
            new_lines.append(ind + '<MenuItem Header="\u0414\u0443\u0431\u043b\u0438\u0440\u043e\u0432\u0430\u0442\u044c" '
                             + pfx + 'DuplicateWidgetCommand}" CommandParameter="{Binding}" />')
            new_lines.append(ind + '<MenuItem Header="\u0423\u0434\u0430\u043b\u0438\u0442\u044c" '
                             + pfx + 'RemoveWidgetCommand}" CommandParameter="{Binding}" />')

# ─── Шаг 2: Удаляем кнопку ❌ и разделитель перед ней из тулбара ─────────────
DELETE_CH = '\u274c'
result_lines = []
i = 0
while i < len(new_lines):
    line = new_lines[i]
    if DELETE_CH in line and '<Button' in line:
        # Пропускаем все строки до закрывающего />
        while i < len(new_lines) and '/>' not in new_lines[i]:
            i += 1
        i += 1  # skip строку с />
        # Удаляем предшествующий разделитель тулбара (Width="1" отличает от ContextMenu Separator)
        if result_lines and 'Separator Width="1"' in result_lines[-1]:
            result_lines.pop()
        continue
    result_lines.append(line)
    i += 1

print(f"Результирующих строк: {len(result_lines)}")

# Статистика
remove = sum(1 for l in result_lines if 'RemoveWidget' in l)
dup    = sum(1 for l in result_lines if 'DuplicateWidget' in l)
del_b  = sum(1 for l in result_lines if DELETE_CH in l)
print(f"RemoveWidgetCommand: {remove}  (ожидается 24: 12 в меню + 12 новые)")
print(f"DuplicateWidgetCommand: {dup}  (ожидается 24: 12 тулбар + 12 меню)")
print(f"Delete emoji buttons remaining: {del_b}  (ожидается 0)")

# ─── Записываем ПРАВИЛЬНО — бинарный режим с \r\n без авто-конвертации ────────
output_bytes = b'\xef\xbb\xbf'  # UTF-8 BOM
output_bytes += '\r\n'.join(result_lines).encode('utf-8')

with open(file_path, 'wb') as f:
    f.write(output_bytes)

print("Файл записан успешно (бинарный режим, нет двойных \\r\\n)!")

# Верификация
with open(file_path, 'rb') as f:
    verify = f.read()
double = verify.count(b'\r\r\n')
print(f"Двойных \\r\\r\\n в результате: {double}  (должно быть 0)")
