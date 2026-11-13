# Устранение неполадок при работе с XamlMcp (Troubleshooting)

Частые проблемы и способы их быстрого решения.

---

## 1. `list-apps` возвращает пустой список `[]`

### Причины и решение:
1. **Приложение не запущено**:
   Убедитесь, что приложение запущено локально:
   ```powershell
   dotnet run --project AvaloniaApplication1/AvaloniaApplication1.csproj
   ```
2. **Не установлена переменная окружения `XAML_MCP=1`**:
   При вызове `builder.AttachXamlMcp()` агент активен только если задана переменная:
   ```powershell
   $env:XAML_MCP=1; dotnet run --project AvaloniaApplication1/AvaloniaApplication1.csproj
   ```
3. **Файлы дескрипторов заблокированы**:
   XamlMcp сохраняет дескрипторы активных сессий в каталоге:
   `%LOCALAPPDATA%\XamlMcp\instances\`
   Если процесс аварийно завершился, в папке могут остаться устаревшие файлы. Очистите папку:
   ```powershell
   Remove-Item "$env:LOCALAPPDATA\XamlMcp\instances\*" -Force -Recurse
   ```

---

## 2. Ошибка запуска `XamlMcp.Server`: 'Microsoft.NETCore.App', version '10.0.0' not found

### Причина:
Сервер `XamlMcp.Server` скомпилирован под платформу **.NET 10**.

### Решение:
Установите среду выполнения .NET 10 через диспетчер пакетов Windows (`winget`):
```powershell
winget install Microsoft.DotNet.Runtime.10
```
Или скачайте официальный установщик .NET 10 Runtime с сайта Microsoft:
`https://dotnet.microsoft.com/download/dotnet/10.0`

---

## 3. Ошибка сборки `NU1605: Package downgrade detected`

### Причина:
`XamlMcp.Avalonia` требует версию фреймворка Avalonia не ниже **11.3.18**.

### Решение:
Проверьте файл `AvaloniaApplication1.csproj`. В нашем проекте используется `Avalonia 12.0.4` (Avalonia 11+ compatible), что полностью удовлетворяет требованиям.

---

## 4. Ошибка `Target ambiguity: multiple candidates found`

### Причина:
При вызове инструментов поиска или манипуляции переданный селектор совпал с несколькими элементами на экране (например, несколько кнопок с одинаковым типом `Button`).

### Решение:
1. Задайте элементу уникальный `x:Name` в XAML.
2. Либо используйте параметр `nth`:
   ```json
   {
     "type": "Button",
     "nth": 0
   }
   ```
3. Либо ограничьте область поиска через `within`:
   ```json
   {
     "type": "Button",
     "within": { "name": "ValvesContainer" }
   }
   ```
