# Tasks: HMI Dashboard SCADA Mimic Refinements (Сессия 4)

- [x] Реализовать предотвращение дублирования MDI-окон (закрытие при повторном клике):
  - [x] Изменить сигнатуру `OpenChildWindow` in `MainViewModel.cs`, чтобы возвращать созданное окно
  - [x] Реализовать отслеживание и закрытие открытого окна во `ValveWidgetViewModel.cs`
  - [x] Реализовать аналогичную логику в `PumpWidgetViewModel.cs`
- [x] Доработать контрол трубы `PipeControl.cs` для адаптивного масштабирования толщины и фланцев от `CellSize`
- [x] Изменить шаг сетки мнемосхемы на `10x10`:
  - [x] Пересчитать (умножить на 2) координаты виджетов и точек труб в `ConfigurationService.cs`
  - [x] Установить дефолтный `CellSize` для мнемосхемы равным `10` в `MainViewModel.cs`
- [x] Проверить сборку проекта и протестировать UX-поведение окон и труб

# Tasks: Valve Control Rework & Mimic Alignment (Сессия 5)

- [x] Создать единый кастомный контрол `ValveControl.cs` с поддержкой:
  - [x] Отрисовки запорных (CutOff) и регулирующих (Regulating) клапанов
  - [x] Динамического масштабирования под размер ячеек
  - [x] Вращения на 90 градусов для вертикальной ориентации (`IsVertical`)
  - [x] Нескольких типов приводов (`Solenoid` с буквами S/P, `Diaphragm`, `Manual`, `None`)
  - [x] Использования динамических цветов `ActiveColor` и `InactiveColor` из настроек
- [x] Удалить устаревший файл `RegulatingValveControl.cs`
- [x] Расширить свойства модели `WidgetConfig` (`IsVertical`, `ActuatorType`) в `HmiConfiguration.cs`
- [x] Добавить свойства во вьюмодели `ValveWidgetViewModel.cs` и `WidgetEditorViewModel.cs`
- [x] Встроить новые поля (выбор привода и галочку вертикальности) в форму `WidgetEditorWindow.axaml`
- [x] Заменить визуальное отображение в `DashboardView.axaml` на новый `ValveControl`
- [x] Обновить дефолтную конфигурацию в `ConfigurationService.cs` и очистить старые кэши `config.json`
- [x] Убедиться, что проект успешно собирается и работает

# Tasks: UX Polish, Context Menus & Bounds Constraints (Сессия 6 — Текущая)

- [x] Внедрить плавное панорамирование холста (Spacebar + ЛКМ) с физической инерцией и затуханием скорости:
  - [x] Добавить скролл-таймер и физику Lerp/Decelerate в `DashboardView.axaml.cs`
  - [x] Настроить смену курсора на указатель руки/перемещения
- [x] Исправить проблему с множественным открытием контекстных меню труб:
  - [x] Создать трекер открытого меню `_activeMenu` в `PipeControl.cs`
  - [x] Вызывать автозакрытие старых меню при клике по фону или открытии нового
- [x] Ограничить перемещение внутренних окон MDI:
  - [x] Запретить утаскивание заголовка окон под верхнее меню в `MainWindow.axaml.cs`
  - [x] Ограничить ресайз окон рамками родительского холста
- [x] Оптимизировать зону ресайза виджетов:
  - [x] Уменьшить область захвата ресайза с 60x60 до 20x20 пикселей в `DashboardPanel.cs`
  - [x] Настроить отображение курсора ресайза только при наведении на правый нижний угол
- [x] Проверить финальную сборку проекта

# Tasks: Pipe Rubber-banding & Dynamic Port Snapping (Сессия 7 — Выполнено)

- [x] Разработать геометрический механизм прилипания труб к фланцам задвижек и насосов:
  - [x] Определить точные координаты портов задвижек/насосов в зависимости от `IsVertical` и размеров виджетов в `DashboardPanel.cs`
  - [x] Добавить структуру связи `ConnectedPipePoint` и список `_connectedPipePoints` в `DashboardPanel.cs`
  - [x] Внедрить сканирование присоединенных труб при старте перемещения в `OnPointerPressed`
  - [x] Обновлять координаты вершин труб при перемещении виджетов на новое место в `OnPointerReleased`
  - [x] Обеспечить вызов `NormalizePointsAndSize()` во вьюмодели трубы для автоматической перестройки размеров и сохранения в `config.json`
- [x] Проверить компиляцию и выполнить верификацию работы «резиновой связи»

# Tasks: Architectural Refactoring — Attached Properties, Direct Properties & Coercion (Сессия 8 — Выполнено)

- [x] Декаплировать координаты разметки в `DashboardPanel.cs` и `DashboardView.axaml` с помощью Attached Properties:
  - [x] Объявить `Col`, `Row`, `SizeX`, `SizeY` как присоединенные свойства в `DashboardPanel.cs`
  - [x] Перевести расчеты `MeasureOverride`/`ArrangeOverride` и обработчики драга на их использование
  - [x] Удалить устаревшие подписки `SubscribeVm` на VM у виджетов
  - [x] Настроить двухстороннюю привязку свойств разметки в стиле контейнеров `ContentPresenter` в `DashboardView.axaml`
- [x] Перевести высокочастотную телеметрию на `DirectProperty`:
  - [x] Свойства `Setpoint` и `Feedback` в `ValveControl.cs`
  - [x] Свойство `IsFilled` в `PipeControl.cs`
  - [x] Свойство `Values` в `TrendLineControl.cs`
- [x] Реализовать защиту данных на этапе коэрсии (Coercion) в свойствах:
  - [x] `CellWidth` и `CellHeight` в `DashboardPanel` (ограничение `[10.0, 500.0]`)
  - [x] `Thickness` в `ValveControl` и `PipeControl` (ограничение `[2.0, 50.0]`)
  - [x] `MinY` и `MaxY` в `TrendLineControl` (ограничение `[-10000.0, 10000.0]`)
  - [x] Уставки и обратная связь в сеттерах Direct-свойств `ValveControl` (ограничение `[0.0, 100.0]`)
- [x] Удалить временное логирование в `debug_log.txt` из `DashboardPanel.cs`
- [x] Верифицировать успешную компиляцию проекта с новыми изменениями


