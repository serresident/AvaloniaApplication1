# -*- coding: utf-8 -*-
"""
Скрипт: переносит команду удаления из плавающей панели в контекстное меню ПКМ.
1. Убирает кнопку ❌ (и разделитель перед ней) из каждого плавающего тулбара.
2. Добавляет 'Дублировать' + 'Удалить' в каждое ContextMenu после 'Свойства'.
"""

import re

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

with open(file_path, "r", encoding="utf-8-sig") as f:
    content = f.read()

# Нормализуем переводы строк для работы
content = content.replace('\r\n', '\n')
lines = content.split('\n')

# ── Шаг 1: Добавляем «Дублировать» и «Удалить» в ContextMenu после «Свойства» ──
new_lines = []
for line in lines:
    new_lines.append(line)
    # Строка с пунктом «Свойства», привязанная к EditWidgetCommand
    if 'EditWidgetCommand' in line and 'Header=' in line:
        # Определяем отступ строки
        indent = len(line) - len(line.lstrip())
        ind = ' ' * indent
        # Вытаскиваем часть пути к DashboardViewModel из той же строки
        # Например: {Binding #WidgetsItemsControl.((vm:DashboardViewModel)DataContext).EditWidgetCommand}
        # Заменяем EditWidgetCommand на нужные команды
        base_cmd = line.split('EditWidgetCommand')[0].strip()
        # base_cmd будет вида: <MenuItem Header="Свойства" Command="{Binding #WidgetsItemsControl.((vm:DashboardViewModel)DataContext).
        # Нам нужен только префикс до имени команды
        # Проще: построим строки команд по шаблону из уже найденной строки
        # Находим шаблон Command="{Binding #WidgetsItemsControl...DataContext)."
        m = re.search(r'Command="\{Binding #WidgetsItemsControl\.\(\(vm:DashboardViewModel\)DataContext\)\.', line)
        if m:
            cmd_prefix = m.group(0)  # Command="{Binding #WidgetsItemsControl.((vm:DashboardViewModel)DataContext).
            new_lines.append(ind + '<Separator />')
            new_lines.append(ind + '<MenuItem Header="Дублировать" ' + cmd_prefix + 'DuplicateWidgetCommand}" CommandParameter="{Binding}" />')
            new_lines.append(ind + '<MenuItem Header="Удалить" ' + cmd_prefix + 'RemoveWidgetCommand}" CommandParameter="{Binding}" />')

# ── Шаг 2: Удаляем ❌ кнопку и разделитель перед ней из плавающих тулбаров ──
DELETE_EMOJI = '\u274c'  # ❌

result_lines = []
i = 0
while i < len(new_lines):
    line = new_lines[i]
    # Нашли начало ❌-кнопки
    if DELETE_EMOJI in line and '<Button' in line:
        # Пропускаем все строки до закрывающего />
        while i < len(new_lines) and '/>' not in new_lines[i]:
            i += 1
        i += 1  # пропускаем саму строку с />
        # Удаляем предшествующий разделитель из тулбара (Separator Width="1" — отличительный признак от контекстного)
        if result_lines and 'Separator Width="1"' in result_lines[-1]:
            result_lines.pop()
        continue
    result_lines.append(line)
    i += 1

# ── Записываем результат ──
output = '\r\n'.join(result_lines)
with open(file_path, "w", encoding="utf-8-sig") as f:
    f.write(output)

# Статистика
remove_count = sum(1 for l in result_lines if 'RemoveWidgetCommand' in l)
dup_count    = sum(1 for l in result_lines if 'DuplicateWidgetCommand' in l)
del_btn      = sum(1 for l in result_lines if DELETE_EMOJI in l)
print(f"Готово! Строк: {len(result_lines)}")
print(f"  RemoveWidgetCommand упоминаний: {remove_count}  (ожидается 24 = 12 меню)")
print(f"  DuplicateWidgetCommand упоминаний: {dup_count}")
print(f"  ❌-кнопок в тулбаре: {del_btn}  (ожидается 0)")
