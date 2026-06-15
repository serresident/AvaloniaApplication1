# -*- coding: utf-8 -*-
"""
v3 - чистый: убирает все дубли Удалить/Дублировать из меню, 
потом добавляет по одному разу после Свойства.
"""
import re

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

with open(file_path, 'rb') as f:
    raw = f.read()
if raw.startswith(b'\xef\xbb\xbf'):
    raw = raw[3:]

content = raw.decode('utf-8')
content = content.replace('\r\r\n', '\n').replace('\r\n', '\n').replace('\r', '\n')
lines = content.split('\n')
print(f"Начальных строк: {len(lines)}")

# ── Шаг 1: Убираем уже добавленные пункты «Дублировать» и «Удалить» из меню ──
# (чтобы не дублировать при повторном запуске)
cleaned = []
i = 0
while i < len(lines):
    line = lines[i]
    skip = False
    # Пункт-дубликат в ContextMenu (не кнопка тулбара)
    if '<MenuItem' in line and ('DuplicateWidgetCommand' in line or 'RemoveWidgetCommand' in line):
        skip = True
    # Разделитель, который мы добавляли (перед Дублировать — проверяем следующую строку)
    # Отдельные <Separator /> не в контексте кнопок тулбара
    if not skip:
        cleaned.append(line)
    i += 1

# ── Шаг 2: Теперь у нас файл только с «Свойства» в меню (без Удалить/Дублировать)
# Убираем лишние Separator что могли остаться (2 подряд внутри ContextMenu)
# Для надёжности — просто ищем двойные <Separator /> подряд и схлопываем
deduped = []
for idx, line in enumerate(cleaned):
    if '<Separator />' in line and deduped and '<Separator />' in deduped[-1]:
        continue  # пропускаем дубль разделителя
    deduped.append(line)

print(f"После очистки строк: {len(deduped)}")

# ── Шаг 3: Добавляем «Дублировать» и «Удалить» после «Свойства» ───────────────
DELETE_CH = '\u274c'
final = []
for line in deduped:
    final.append(line)
    if 'EditWidgetCommand' in line and 'Header=' in line:
        indent = len(line) - len(line.lstrip())
        ind = ' ' * indent
        m = re.search(r'Command="\{Binding (#WidgetsItemsControl\.\(\(vm:DashboardViewModel\)DataContext\))\.', line)
        if m:
            ref = m.group(1)
            pfx = f'Command="{{Binding {ref}.'
            final.append(ind + '<Separator />')
            final.append(ind + f'<MenuItem Header="\u0414\u0443\u0431\u043b\u0438\u0440\u043e\u0432\u0430\u0442\u044c" {pfx}DuplicateWidgetCommand}}" CommandParameter="{{Binding}}" />')
            final.append(ind + f'<MenuItem Header="\u0423\u0434\u0430\u043b\u0438\u0442\u044c" {pfx}RemoveWidgetCommand}}" CommandParameter="{{Binding}}" />')

print(f"Итого строк: {len(final)}")

# ── Шаг 4: Убираем ❌ кнопку из тулбара ───────────────────────────────────────
result = []
j = 0
while j < len(final):
    line = final[j]
    if DELETE_CH in line and '<Button' in line:
        while j < len(final) and '/>' not in final[j]:
            j += 1
        j += 1
        if result and 'Separator Width="1"' in result[-1]:
            result.pop()
        continue
    result.append(line)
    j += 1

# ── Статистика ─────────────────────────────────────────────────────────────────
remove = sum(1 for l in result if 'RemoveWidget' in l)
dup    = sum(1 for l in result if 'DuplicateWidget' in l)
del_b  = sum(1 for l in result if DELETE_CH in l)
udal_m = sum(1 for l in result if 'MenuItem' in l and 'RemoveWidget' in l)
dup_m  = sum(1 for l in result if 'MenuItem' in l and 'DuplicateWidget' in l)
print(f"RemoveWidgetCommand всего: {remove}")
print(f"  из них в MenuItem меню: {udal_m}  (ожидается 12)")
print(f"DuplicateWidgetCommand всего: {dup}")
print(f"  из них в MenuItem меню: {dup_m}  (ожидается 12)")
print(f"Delete emoji кнопок: {del_b}  (ожидается 0)")
print(f"Итого строк в файле: {len(result)}")

# ── Запись в бинарном режиме ───────────────────────────────────────────────────
out = b'\xef\xbb\xbf' + '\r\n'.join(result).encode('utf-8')
with open(file_path, 'wb') as f:
    f.write(out)

# Проверка
with open(file_path, 'rb') as f:
    v = f.read()
print(f"Двойных \\r\\r\\n: {v.count(b'\r\r\n')}  (должно быть 0)")
print("Готово!")
