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

# Tasks: Visual Flange Snapping for Valves and Pumps (Сессия 9 — Выполнено)

- [x] Выравнивание прилипания труб к визуальным фланцам задвижек и насосов:
  - [x] Реализовать метод `GetVisualPortsInGrid` в `DashboardPanel.cs`
  - [x] Обновить обработчик `OnPointerPressed` для привязки труб по визуальным портам
  - [x] Обновить обработчик `OnPointerMoved` для прилипания конца трубы при перетаскивании к визуальным портам
  - [x] Обновить обработчик `OnPointerReleased` для перемещения подключенных труб по дельте координат
  - [x] Обновить `GetSnappedPosition` для выравнивания позиционирования виджета по визуальным портам
  - [x] Проверить сборку проекта и выполнить верификацию

# Tasks: Dedicated Pipe Vertex Editing Mode (Сессия 10 — Выполнено)

- [x] Внедрить выделенный режим редактирования вершин для труб:
  - [x] Добавить свойство `IsEditingVertices` и команду `ToggleEditingVertices` в `PipeWidgetViewModel.cs`
  - [x] Добавить StyledProperty `IsEditingVertices` в `PipeControl.cs` и ограничить показ маркеров
  - [x] Обновить сброс режима при смене выбранного элемента в `DashboardPanel.cs`
  - [x] Ограничить перетаскивание вершин и открытие вертексных/сегментных меню в `DashboardPanel.cs`
  - [x] Добавить привязку `IsEditingVertices` и кнопки включения/выключения режима в контекстное меню в `DashboardView.axaml`
  - [x] Проверить сборку проекта и выполнить верификацию

# Tasks: Code Review Fixes & MDI Lifecycle (Сессия 12 — Выполнено)

- [x] Культуронезависимый парсинг чисел и строк (IFormatProvider & NumberStyles):
  - [x] `TagValueConverter.cs`: обновить `ToBool` и `ToDouble` на `CultureInfo.InvariantCulture` и `OrdinalIgnoreCase`
  - [x] `MqttProtocolDriver.cs`: культуронезависимый парсинг вещественного payload и безопасное сравнение булевых строк
  - [x] `ModbusProtocolDriver.cs`: заменить прямой `double.Parse` на `TagValueConverter.ToDouble` для предотвращения `FormatException`
  - [x] `MockProtocolDriver.cs`: парсинг `ZAS_Setpoint_Power` через `CultureInfo.InvariantCulture`
  - [x] `TrendLineControl.cs`: парсинг элементов коллекции точек с `NumberStyles.Float` и `CultureInfo.InvariantCulture`
  - [x] `PumpWidgetViewModel.cs`: унификация разбора значения через `TagValueConverter.ToBool`
  - [x] `SetValueViewModel.cs`, `TankWidgetViewModel.cs`, `RealTimeTrendViewModel.cs`, `ValveControlPopupViewModel.cs`: передача `CultureInfo.InvariantCulture` и `NumberStyles.Float`
- [x] Устранение утечки памяти и подписок MDI-окон (Lifecycle & Disposables):
  - [x] `ChildWindowViewModel.cs`: реализовать интерфейс `IDisposable`, утилизировать `Content as IDisposable` и очистить `CloseAction`
  - [x] `MainViewModel.cs`: вызывать `childWindow.Dispose()` в обработчике закрытия окна `CloseAction`
  - [x] `MainViewModel.cs`: утилизировать все активные окна в `Dispose()`
- [x] Проверить сборку проекта и устранить предупреждения
# Tasks: Code Review Stage 2 — Compiler Warnings, Render Allocations & DashboardPanel Decomposition (Сессия 13 — Выполнено)

- [x] Устранение предупреждений компилятора (CS8602, CS8604, NU1603):
  - [x] `DialogService.cs`: устранить разыменование вероятной пустой ссылки через локальную переменную `targetWindow`
  - [x] `MainViewModel.cs`: устранить передачу `content` (CS8604) через валидацию и локальную переменную `windowContent`
  - [x] `AvaloniaApplication1.csproj`: обновить `MQTTnet` до `4.3.6.1152`
- [x] Оптимизация аллокаций в Render (Custom Controls Zero-Allocation):
  - [x] `TrendLineControl.cs`: статическое кэширование перьев/кистей (`GridPen`, `DefaultLinePen`, `DefaultAreaBrush`) и переиспользование списков `_valList`, `_points`
  - [x] `ValveControl.cs`: статическое кэширование перьев и кистей (`ValveBorderPen`, `BoxOutlinePen`, `CenterTrianglePen`, `RedAlarmBorderPen`, `BlackAlarmPen`, `FlangeBrush`, `FlangePen`, `FeedbackBarBgBrush`, `FeedbackBarBgPen`)
- [x] Изоляция Code-Behind (Раздел 7.3 Кодекса):
  - [x] `DashboardViewModel.cs`: добавить метод `AdjustZoom(delta)` с инкапсулированным `Math.Clamp`
  - [x] `DashboardView.axaml.cs`: вызывать `viewModel.AdjustZoom(delta)` вместо прямой мутации состояния из UI-события
- [x] Декомпозиция God Object `DashboardPanel.cs` (Разделы 1.1, 2.1 Кодекса):
  - [x] Создать вспомогательный класс `Views/DashboardPanelHelpers/VisualPortHelper.cs`
  - [x] Вынести рекурсивные методы обхода визуального дерева и геометрический расчет портов прилипания («резиновых связей»)
  - [x] Разгрузить `DashboardPanel.cs` почти на 300 строк кода
- [x] Проверить сборку проекта и прогнать тесты

# Tasks: Code Review & Quality Audit (Сессия 14 — Текущая)

- [x] Провести комплексный повторный аудит кодовой базы по 14 разделам Кодекса разработки:
  - [x] Оценка архитектуры (Clean Architecture, SOLID, SRP, OCP, DIP)
  - [x] Аудит управления памятью и жизненного цикла ресурсов (IDisposable, Rx.NET subscriptions)
  - [x] Анализ рендеринга и профилирование аллокаций (Zero-Allocation в Render)
  - [x] Проверка асинхронности, потокобезопасности и отсутствия блокировок
  - [x] Проверка локалезависимости и кроссплатформенности
  - [x] Аудит логирования и обработки исключений
- [x] Сформировать детальный аналитический отчет с оценками и динамикой улучшений

# Tasks: SCADA Mimic Evolution & MQTT Fix (Сессия 15 — Выполнено)

- [x] Исправление культуронезависимости в `MqttProtocolDriver.WriteAsync`:
  - [x] Форматирование уставки через `IFormattable` / `CultureInfo.InvariantCulture`
- [x] Создание алгоритма автотрассировки труб `PipeAutoRouter.cs`:
  - [x] Ортогональный A* Manhattan маршрутизатор с штрафом за изгибы (90° bends) и обходом препятствий
  - [x] Удаление коллинеарных промежуточных точек `SimplifyCollinearPoints`
  - [x] Команда `AutoRoutePipeCommand` в `DashboardViewModel`
  - [x] Кнопка в контекстном меню трубы в `DashboardView.axaml`
- [x] Разработка новых промышленных аппаратов SCADA:
  - [x] **Теплообменник (`HeatExchanger`)**:
    - [x] `HeatExchangerConfig` в `HmiConfiguration.cs` и десериализатор в `WidgetJsonConverter.cs`
    - [x] `HeatExchangerControl.cs` с кожухотрубным/пластинчатым рендерингом и фланцами
    - [x] `HeatExchangerWidgetViewModel.cs` с поддержкой первичного и вторичного контуров
  - [x] **Реактор с мешалкой (`Reactor`)**:
    - [x] `ReactorConfig` в `HmiConfiguration.cs`
    - [x] `ReactorControl.cs` с рубашкой обогрева/охлаждения и анимированным импеллером (~30 FPS)
    - [x] `ReactorWidgetViewModel.cs` с командами управления мешалкой
  - [x] **Датчик уровня (`LevelSensor`)**:
    - [x] `LevelSensorConfig` в `HmiConfiguration.cs`
    - [x] `LevelSensorControl.cs` со стандартом ISA 5.1 (символ LT), волноводом и аварийными зонами
    - [x] `LevelSensorWidgetViewModel.cs` с отслеживанием уставок Hi/Lo
- [x] Поддержка портов прилипания и «резиновых связей» в `VisualPortHelper.cs` и `DashboardPanel.cs`
- [x] Интеграция в фабрику `WidgetFactory.cs`, редактор `WidgetEditorViewModel.cs` и разметку `DashboardView.axaml`
- [x] Обновление демонстрационной мнемосхемы в `config.json`
- [x] Успешная сборка (0 ошибок, 0 предупреждений) и запуск приложения

# Tasks: Fix Hit-Testing, Selection & ContextMenu Deletion (Сессия 16 — Выполнено)

- [x] Устранение багов хит-тестинга и ложного удаления предыдущих объектов:
  - [x] Внедрить точный визуальный хит-тестинг через `e.Source` и восхождение по дереву предков до прямого потомка `DashboardPanel`
  - [x] Реализовать обратный обход `Children` (Reverse Z-Order) для fallback-проверки вместо прямого (0 -> N)
  - [x] Исключить использование прямоугольного хит-бокса для труб `Pipe`: проверять попадание только по фактическим сегментам (`IsPointNearSegment`)
  - [x] Использовать координаты напрямую из ViewModel (`vm.Col`, `vm.Row`, `vm.SizeX`, `vm.SizeY`) в `DashboardPanel`, исключив рассинхрон с attached properties
  - [x] Принудительно синхронизировать свойства `ColProperty`, `RowProperty`, `SizeXProperty`, `SizeYProperty` на `_dragChild` при завершении перетаскивания и ресайза
  - [x] Использовать прямое считывание `vm` координат в `MeasureOverride` и `ArrangeOverride`
- [x] Коррекция позиционирования и параметров контекстного меню:
  - [x] Настроить открытие меню точно под курсором мыши (`menu.Placement = PlacementMode.Pointer`)
  - [x] Явно передавать `menu.DataContext = clickedVm` для гарантии верного параметра в `RemoveWidgetCommand`
  - [x] Добавить защитный fallback `widget ??= Widgets.FirstOrDefault(w => w.IsSelected);` в `RemoveWidget` и `DuplicateWidget` в `DashboardViewModel`
  - [x] Добавить удаление выбранного виджета по нажатию клавиши `Delete` на клавиатуре в `OnKeyDown`
- [x] Сборка решения (`0 ошибок, 0 предупреждений CS`) и запуск приложения

# Tasks: Build Lock Diagnostics & Verification (Сессия 17 — Завершено)

- [x] Диагностика причин сбоя сборки проекта:
  - [x] Зафиксирована блокировка файла `AvaloniaApplication1.dll` процессами ОС Windows при работающем приложении в фоне
  - [x] Завершены активные задачи и освобождены дескрипторы исполняемых файлов
  - [x] Выполнена чистая сборка решения в конфигурации `Release` (0 ошибок, 0 предупреждений)
  - [x] Выполнена чистая сборка решения в конфигурации `Debug` (0 ошибок, 0 предупреждений)
  - [x] Зафиксирован результат сборки и обновлен журнал разработки

# Tasks: Fix Selection, Tunnel Handling & Native ContextMenu Deletion (Сессия 18 — Завершено)

- [x] Устранение дефекта перехвата кликов дочерними контролами (Buttons, Switches):
  - [x] Внедрить перехватчик `AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel)` в `DashboardPanel`
  - [x] Исключить срабатывание команд кнопок внутри виджетов (открытие попапов задвижек/насосов) в режиме редактирования
  - [x] Обеспечить выбор и начало перетаскивания любого виджета с первого клика мыши
- [x] Кардинальное решение проблемы контекстного меню и удаления виджетов:
  - [x] Удалить 15 дублированных и подверженных ошибкам блоков `<Border.ContextMenu>` из `DashboardView.axaml`
  - [x] Реализовать единый программный генератор контекстного меню `ShowWidgetContextMenu` в `DashboardPanel.cs`
  - [x] Привязать действия меню («Удалить», «Дублировать», «Свойства», «Z-порядок») напрямую к целевому `targetVm` без XAML-рефлексии
  - [x] Корректно сбрасывать `SelectedVm = null` и `widget.IsSelected = false` при удалении виджета
  - [x] Автоматически закрывать контекстные меню при смене режима редактирования
- [x] Проверить сборку Debug/Release (0 ошибок, 0 предупреждений)

# Tasks: Avalonia 11+ Standards Audit, Headless UI Runner & Protocol Integration (Сессия 19 — Завершено)

- [x] Этап 1. Поиск и сбор ранее реализованных инструкций:
  - [x] Проведен аудит `AGENTS.md`, `GEMINI.md`, `docs/agent.md`, `.agents/rules/agent.md`, `docs/preprompt.md`
  - [x] Зафиксированы все уникальные особенности архитектуры SCADA мнемосхемы, технологических аппаратов, реактивного ядра DataCoreService и протоколов
- [x] Этап 2. Сопоставление с новыми стандартами разработки Avalonia 11+:
  - [x] Интеграция стандартов .NET 8 / C# 12 / Avalonia 11+ (12.0.4) и MVVM на базе `CommunityToolkit.Mvvm`
  - [x] Строгий запрет синтаксиса WPF (`DependencyProperty.Register`, `<Style.Triggers>`)
  - [x] Фиксация StyledProperty/DirectProperty и селекторов псевдоклассов `^:pointerover`, `^:pressed`, `^:disabled`
  - [x] Обязательные Compiled Bindings (`x:DataType`, `x:CompileBindings="True"`, решение AVLNxxxx типизацией)
  - [x] Многопоточность и нулевые аллокации в `Render(DrawingContext)` с вызовом `AffectsRender`
- [x] Этап 3. Инкрементальное обновление и сохранение:
  - [x] Сформирован объединенный кодекс правил в `.antigravity/rules.md`
  - [x] Продублирован кодекс в корневой `AGENT_RULES.md`
  - [x] Актуализированы `AGENTS.md`, `GEMINI.md`, `docs/agent.md` и `.agents/rules/agent.md`
  - [x] Создан корневой `.editorconfig` для стандартов C# и XAML
  - [x] Развернут headless UI-раннер `AvaloniaApplication1.UIValidation` с устранением сетевых блокировок
  - [x] Созданы скрипты `scripts/render_ui.sh`, `scripts/render_ui.ps1`, `scripts/check-build.sh`, `scripts/check-build.ps1`
- [x] Этап 4. Проверка и верификация:
  - [x] Выполнен тестовый рендер `MainWindow` и `DashboardView` (3-4 сек)
  - [x] Сгенерированы артефакты `artifacts/ui_preview.png` и `artifacts/ui_tree.json`
  - [x] Выполнена чистая сборка `dotnet build AvaloniaApplication1.sln` (0 ошибок, 0 предупреждений)

# Tasks: Pipe-Valve Fast Docking & Flush Flange Geometry (Сессия 20 — Завершено)

- [x] Разработка математической модели плотной стыковки (Вариант 2 — встык без зазоров и нахлестов):
  - [x] В `PipeControl.cs` скорректировать метод `DrawFlangePlate`: внешняя грань фланца строго по точке порта $P$, толщина уходит назад по вектору трубы
  - [x] Сместить цилиндры 3D трубы (`seg0Start`, `segLastEnd`) на `flangeThickness` назад, исключив врезание трубы во фланец
  - [x] Отключить по умолчанию двойные фланцы на клапане в `ValveControl.cs` (`ShowFlangesProperty = false`)
- [x] Точный расчет координат портов задвижек:
  - [x] В `VisualPortHelper.cs` учесть шкалу обратной связи `availableH = isRegulating && showFeedbackBar ? h - 18 : h`
  - [x] Центрировать порты по основанию треугольников задвижки с учетом поворота (`IsVertical`)
- [x] Магнитные порты с визуальным snap-индикатором и автофланцем:
  - [x] Увеличить радиус примагничивания в `DashboardPanel.cs` до 25 px
  - [x] Добавить свойства `ActiveSnapTarget` и флаг `_isSnappedToEquipmentPort`
  - [x] Реализовать отрисовку неоново-зеленого кольца примагничивания в `SelectionOverlay.cs`
  - [x] Автоматически устанавливать `StartFitting = "Flange"` / `EndFitting = "Flange"` в `OnPointerReleased` при стыковке
- [x] Универсальный парсинг координат точек труб:
  - [x] Поддержка как пробелов, так и точек с запятой в `PipeControl.cs` и `PipeWidgetViewModel.cs`
- [x] Визуальная верификация и Headless UI тест:
  - [x] Добавить поддержку рендера мнемосхемы `MimicView` в `AvaloniaApplication1.UIValidation`
  - [x] Сгенерировать скриншот `artifacts/ui_preview.png` и проверить геометрию стыка клапана `YV1`
  - [x] Проверить сборку `check-build.ps1` (0 ошибок, 0 предупреждений)

# Tasks: Valve-Pipe Alignment & Zero-Gap Docking Fix (Сессия 21 — Завершено)

- [x] Диагностика и локализация смещения трубы относительно клапана:
  - [x] Обнаружено смещение графики `ValveControl` из-за сжатия в `DockPanel` нижней панелью заголовка/значения
  - [x] Замена `DockPanel` на полноразмерный `Grid` в `DashboardView.axaml`, центрирующий бабочку клапана ровно по координатной сетке ячейки
  - [x] Вынос подписи и текущего значения клапана в нижний оверлей без деформации графического тела
- [x] Устранение полуячеечного сдвига (Half-cell Grid Offset) для портов прилипания:
  - [x] В `VisualPortHelper.cs` скорректированы fallback-координаты и порты аппаратов (`-0.5` ячейки) в соответствии с правилом центрирования `PipeControl` ($X_{pixel} = gp \cdot S + S/2$)
  - [x] В `config.json` и `AvaloniaApplication1/config.json` скорректированы координаты `Линия R101 -> YV1` (`Row: 29.5, Col: 18.5`) и `Линия YV1 -> E102` (`Row: 23.5, Col: 35.5`)
- [x] Визуальная верификация и сборка:
  - [x] Рендеринг `scripts/render_ui.ps1 MimicView` и Vision-анализ срезов `artifacts/valve_zoom2.png`
  - [x] Подтверждена математически идеальная стыковка фланцев труб вплотную к основаниям треугольников клапана (зазор 0 px, нахлест 0 px, соосность 100%)
  - [x] Проверка сборки `scripts/check-build.ps1` (0 ошибок, 0 предупреждений)

# Tasks: Reverse Port Snapping (Widget to Pipe Endpoint) (Сессия 22 — Завершено)

- [x] Разработка математической модели обратного примагничивания портов:
  - [x] Расчет локальных смещений портов виджета $relX_1, relY_1, relX_2, relY_2$ относительно его угла `Col, Row`
  - [x] Сканирование крайних вершин труб на панели ($P_{pipe} \in \{start, end\}$) с исключением уже присоединенных труб
  - [x] Вычисление расстояния в пикселях от портов перемещаемого виджета до вершин труб с радиусом захвата 25 px
- [x] Интеграция в интерактивный цикл перетаскивания `DashboardPanel.cs`:
  - [x] В `GetSnappedPosition` внедрен расчет оптимального прилипания порта клапана/насоса/аппарата к вершине трубы
  - [x] Индикация точки прилипания в реальном времени через `ActiveSnapTarget` и неоновое кольцо `SelectionOverlay`
  - [x] В `OnPointerReleased` автоматическая установка фланца трубы (`StartFitting = "Flange"` / `EndFitting = "Flange"`) при обратной стыковке
- [x] Верификация:
  - [x] Сборка `scripts/check-build.ps1`: 0 ошибок, 0 предупреждений
  - [x] Headless-рендеринг `scripts/render_ui.ps1 MimicView` успешен



# Tasks: PLC Project 511-05 Integration & HMI Configuration (РЎРµСЃСЃРёСЏ 23 вЂ” Р—Р°РІРµСЂС€РµРЅРѕ)

- [x] РђСѓРґРёС‚ СЂРµРїРѕР·РёС‚РѕСЂРёСЏ РџР›Рљ `511-05` (https://github.com/serresident/511-05.git):
  - [x] РђРЅР°Р»РёР· С‚РёРїРѕРІ РґР°РЅРЅС‹С… IEC 61131-3: `Coils.dut.st`, `Inputs.dut.st`, `HoldingRegs.dut.st`, `InputRegs.dut.st`
  - [x] РђРЅР°Р»РёР· Р»РѕРіРёРєРё СѓРїСЂР°РІР»РµРЅРёСЏ: `PLC_PRG.prg.st`, `PRG_Apparatus511.prg.st`, `PRG_Apparatus511_CascadeDrain.prg.st`, `PRG_Apparatus511_Heating.prg.st`, `PRG_Valves.prg.st`
  - [x] РђРЅР°Р»РёР· РїР°СЂР°РјРµС‚СЂРѕРІ Modbus TCP Slave (`PLC_PRG_MBS_TCP_SLAVE`, СЃРјРµС‰РµРЅРёСЏ СЂРµРіРёСЃС‚СЂРѕРІ, РїРѕСЂСЏРґРѕРє Р±Р°Р№С‚ `CDAB`)
- [x] Р Р°Р·СЂР°Р±РѕС‚РєР° РїРѕР»РЅРѕР№ РєР°СЂС‚С‹ Modbus-СЂРµРіРёСЃС‚СЂРѕРІ РґР»СЏ РІРµРґРµРЅРёСЏ С‚РµС…РЅРѕР»РѕРіРёС‡РµСЃРєРѕРіРѕ РїСЂРѕС†РµСЃСЃР°:
  - [x] Coils `00001`вЂ“`00013`: Р СѓС‡РЅРѕР№/РђРІС‚Рѕ СЂРµР¶РёРј РґРёР°Р·Рѕ, СЃРѕРґС‹, РїР°СЂР°, РІРѕРґС‹; РїСѓСЃРє РґРѕР·РёСЂРѕРІР°РЅРёСЏ РІРѕРґС‹, РїСѓСЃРє РЅР°РіСЂРµРІР°, РїСѓСЃРє/РїР°СѓР·Р° СЃРѕС‡РµС‚Р°РЅРёСЏ, СѓРґРµСЂР¶Р°РЅРёРµ С‚РµРјРїРµСЂР°С‚СѓСЂС‹
  - [x] Discrete Inputs `10500`вЂ“`10502`: РЎС‚Р°С‚СѓСЃ СЃРѕР»РµРЅРѕРёРґР° Рё РєРѕРЅС†РµРІРёРєРѕРІ РѕС‚СЃРµС‡РЅРѕРіРѕ РєСЂР°РЅР° РІРѕРґС‹
  - [x] Input Registers `30009`вЂ“`30045`: РћР±СЂР°С‚РЅР°СЏ СЃРІСЏР·СЊ РїРѕР»РѕР¶РµРЅРёСЏ СЂРµРіСѓР»РёСЂСѓСЋС‰РёС… РєР»Р°РїР°РЅРѕРІ РґРёР°Р·Рѕ, СЃРѕРґС‹, РїР°СЂР°; pH СЃСЂРµРґС‹, С‚РµРјРїРµСЂР°С‚СѓСЂР° СЂРµР°РєС‚РѕСЂР°, РІРµСЃС‹ WE-1/WE-2, РјРіРЅРѕРІРµРЅРЅС‹Рµ Рё СЃСѓРјРјР°СЂРЅС‹Рµ СЂР°СЃС…РѕРґС‹ (Р­Р›Р•РњР•Р -Р Р­Рњ), РѕСЃС‚Р°С‚РѕРє РІСЂРµРјРµРЅРё СЃР»РёРІР°, РєРѕРґ СЌС‚Р°РїР°
  - [x] Holding Registers `40101`вЂ“`40157`: Р СѓС‡РЅС‹Рµ СѓСЃС‚Р°РІРєРё РєР»Р°РїР°РЅРѕРІ, С‚РµС…РЅРѕР»РѕРіРёС‡РµСЃРєРёРµ СѓСЃС‚Р°РІРєРё С‚РµРјРїРµСЂР°С‚СѓСЂС‹, РґРѕР·С‹ РІРѕРґС‹, СЂР°СЃС…РѕРґР° РґРёР°Р·Рѕ, РїР°СЂР°РјРµС‚СЂС‹ СЂРµРіСѓР»СЏС‚РѕСЂРѕРІ
- [x] РџСЂРѕРµРєС‚РёСЂРѕРІР°РЅРёРµ Рё РіРµРЅРµСЂР°С†РёСЏ РєРѕРЅС„РёРіСѓСЂР°С†РёРё HMI (`config.json` Рё `default_config.json`):
  - [x] Р РµР·РµСЂРІРЅРѕРµ РєРѕРїРёСЂРѕРІР°РЅРёРµ РїСЂРµРґС‹РґСѓС‰РµР№ РєРѕРЅС„РёРіСѓСЂР°С†РёРё РІ `config.gpa_backup.json`
  - [x] РЎРѕР·РґР°РЅРёРµ РїРѕРґРєР»СЋС‡РµРЅРёСЏ `plc_511` (ModbusTCP, 502, `CDAB`, РёРЅС‚РµСЂРІР°Р» 200 РјСЃ)
  - [x] Р Р°Р·СЂР°Р±РѕС‚РєР° P&ID РјРЅРµРјРѕСЃС…РµРјС‹ Mimic: Р РµР°РєС‚РѕСЂ 511, 4 С‚СЂСѓР±РѕРїСЂРѕРІРѕРґРЅС‹Рµ Р»РёРЅРёРё СЃ С„Р»Р°РЅС†Р°РјРё, 4 РєР»Р°РїР°РЅР° (YV1, FCV1, FCV2, TCV1), СЂР°СЃС…РѕРґРѕРјРµСЂС‹, РґР°С‚С‡РёРєРё, РєРЅРѕРїРєРё РїСѓСЃРєР°/РїР°СѓР·С‹ РѕРїРµСЂР°С†РёР№
  - [x] Р Р°Р·СЂР°Р±РѕС‚РєР° РѕРїРµСЂР°С‚РѕСЂСЃРєРѕРіРѕ Dashboard: 4 С‚СЂРµРЅРґР° СЂРµР°Р»СЊРЅРѕРіРѕ РІСЂРµРјРµРЅРё (РўРµРјРїРµСЂР°С‚СѓСЂР°, pH, СЂР°СЃС…РѕРґС‹), РёРЅР¶РµРЅРµСЂРЅС‹Рµ РїР°РЅРµР»Рё СѓСЃС‚Р°РІРѕРє
- [x] Р’РµСЂРёС„РёРєР°С†РёСЏ:
  - [x] РЎР±РѕСЂРєР° РїСЂРѕРµРєС‚Р° `scripts/check-build.ps1`: 0 РѕС€РёР±РѕРє, 0 РїСЂРµРґСѓРїСЂРµР¶РґРµРЅРёР№
  - [x] Headless-СЂРµРЅРґРµСЂРёРЅРі `scripts/render_ui.ps1 MimicView` Рё `DashboardView`
  - [x] РџСЂРѕРІРµСЂРєР° РѕС‚СЃСѓС‚СЃС‚РІРёСЏ РЅР°Р»РѕР¶РµРЅРёР№, РёРґРµР°Р»СЊРЅРѕР№ СЃС‚С‹РєРѕРІРєРё С‚СЂСѓР± Рё С‡РёС‚Р°РµРјРѕСЃС‚Рё РјРµС‚РѕРє

# Tasks: Widget Copy/Paste Context Menu & Valve Port Invariance (Сессия 24 — Завершено)

- [x] Устранение сбоя магнитности портов трубы и клапана при смене типа клапана (`CutOff` / `Regulating`):
  - [x] Фиксация центра графического тела бабочки клапана строго в $(w/2, h/2)$ в `ValveControl.cs` независимо от наличия шкалы обратной связи
  - [x] Выравнивание расчета портов в `VisualPortHelper.GetVisualPortsInGrid` с центром $(w/2, h/2)$ и полным `flowSize = isFlowVertical ? h : w`
  - [x] Вынос отрисовки шкалы обратной связи к нижней границе виджета без вертикального смещения бабочки клапана и его штуцеров
  - [x] Сохранение `_connectedPipePoints` и целостности резиновых связей при переключении типа клапана
- [x] Реализация универсального сервиса буфера обмена виджетов:
  - [x] Создан `Services/WidgetClipboard.cs` с полиморфной JSON-сериализацией и глубоким клонированием любых типов виджетов
- [x] Копирование и вставка виджетов через контекстное меню и горячие клавиши:
  - [x] В `DashboardViewModel.cs` добавлены `CopyWidgetCommand`, `PasteWidgetCommand`, `PasteWidgetAt(col, row)` и `AddWidgetFromConfig`
  - [x] В контекстное меню виджета `ShowWidgetContextMenu` добавлены пункты «Копировать» и «Вставить»
  - [x] В `DashboardPanel.cs` реализовано контекстное меню пустого пространства канвы `ShowCanvasContextMenu` с пунктом «Вставить» по координатам клика
  - [x] В `DashboardPanel.OnKeyDown` добавлена поддержка горячих клавиш `Ctrl+C` (копирование) и `Ctrl+V` (вставка)
- [x] Поддержка контейнера `ContainerButton`:
  - [x] В `ContainerButtonViewModel.cs` снята блокировка открытия в режиме редактирования (`IsDesignMode`)
  - [x] Реализована команда `PasteWidgetIntoContainerCommand` / метод `PasteWidgetIntoContainer()` для добавления в `TypedConfig.Children` и динамического обновления открытого дочернего окна контейнера
  - [x] В контекстное меню `ContainerButton` добавлены пункты «Открыть контейнер» и «Вставить внутрь контейнера»
  - [x] В `WidgetEditorViewModel.cs` обеспечено сохранение дочерних виджетов `Children` при редактировании свойств контейнера
- [x] Верификация:
  - [x] 5 автоматических проверок в `AvaloniaApplication1.UIValidation/Program.cs`:
    - Копирование/вставка простых виджетов
    - Копирование/вставка контейнеров с вложенными детьми
    - Вставка внутрь `ContainerButton`
    - 100% совпадение портов клапана между CutOff и Regulating (погрешность < 0.001)
    - Вставка на канву по заданным координатам `(col, row)`
  - [x] Сборка `scripts/check-build.ps1`: 0 ошибок, 0 предупреждений
  - [x] Рендеринг `scripts/render_ui.ps1 MimicView` и `DashboardView` успешно пройден

# Tasks: Canvas Context Menu, Add Element & Grid/Scale Properties (Сессия 25 — Завершено)

- [x] Надежный вызов контекстного меню рабочей области в режиме редактирования (Design Mode):
  - [x] Снятие мертвых зон: обработчик `OnBubblePointerPressed` в `DashboardView.axaml.cs` для перехвата правого клика по пустым полям и отступам
  - [x] Расширение минимальной области `MeasureOverride` в `DashboardPanel.cs` в режиме редактирования (50x40 ячеек)
  - [x] Публичный метод `ShowCanvasContextMenu(Point clickPoint)` в `DashboardPanel.cs`
- [x] Добавление элементов через контекстное меню канвы:
  - [x] Пункт «➕ Добавить элемент...» с вызовом диалога `ShowWidgetEditorAsync` и предзаполненными координатами `(col, row)`
  - [x] Подменю «⚡ Быстро добавить» для мгновенной вставки любого аппарата в 1 клик (Задвижка, Насос, Труба, Бак, Теплообменник, Реактор, Датчик уровня, Контейнер, Значение, Уставка, Тренд, Кнопка, Лампа, Ползунок)
  - [x] Методы `AddWidgetAtAsync(col, row)` и `AddQuickWidgetAt(type, col, row)` в `DashboardViewModel.cs`
- [x] Диалог настройки свойств рабочей области («Свойства»):
  - [x] Создание диалога `Views/DashboardPropertiesWindow.axaml` и вьюмодели `ViewModels/DashboardPropertiesViewModel.cs`
  - [x] Настройка размера ячейки сетки (Cell Size): пресеты (10, 20, 40, 80, 160 px) и точный `NumericUpDown` (10–500 px)
  - [x] Настройка масштаба (Zoom Scale): пресеты (50%, 75%, 100%, 125%, 150%, 200%), бегунок `Slider` и `NumericUpDown` (20%–300%)
  - [x] Кнопка «🔄 По умолчанию» (40 px / 100%), «Отмена» и «Применить»
  - [x] Реактивное обновление `CellWidth`, `CellHeight`, `ZoomScale` в `DashboardViewModel.cs`
  - [x] Обработчики смены размера сетки в `DashboardPanel.cs` с динамической перерисовкой `_gridOverlay`
- [x] Полная поддержка для окон контейнеров `ContainerButton`:
  - [x] Свойства `CellSize` и `ZoomScale` в `ContainerButtonConfig` (`HmiConfiguration.cs`)
  - [x] Синхронизация масштаба и сетки между внутренним `DashboardViewModel` и `ContainerButtonConfig` в `ContainerButtonViewModel.cs`
  - [x] Настройки сетки и масштаба контейнера в форме `WidgetEditorWindow.axaml` (`ShowContainerSettings`)
- [x] Верификация и тестирование:
  - [x] 8 автоматических тестов в `AvaloniaApplication1.UIValidation/Program.cs`:
    - Копирование/вставка простых и вложенных виджетов
    - Вставка внутрь ContainerButton и инвариантность портов клапанов
    - Тестирование пресетов и сброса `DashboardPropertiesViewModel`
    - Тестирование `UpdateGridAndScale`, реактивности и событий `OnGridOrScaleChanged`
    - Быстрое добавление элементов `AddQuickWidgetAt` точно по координатам ячейки
    - Наследование и двухсторонняя синхронизация `CellSize`/`ZoomScale` окна-контейнера `ContainerButton`
  - [x] Сборка `scripts/check-build.ps1`: 0 ошибок, 0 предупреждений
  - [x] Рендеринг `scripts/render_ui.ps1 MimicView` и `DashboardView` успешно пройден, артефакты сформированы

# Tasks: Widget Instant Deletion & Overlays Decoupling (Сессия 26 — Завершено)

- [x] Устранение дефекта «фантомного» удаления виджетов (зависание элементов на канве без переключения вкладок):
  - [x] Анализ жизненного цикла `ItemsControl.ItemsPanel` (`DashboardPanel.Children`) и локализация дефекта: служебные оверлеи `_gridOverlay` и `_selectionOverlay` вставлялись напрямую в `Children` (`Insert(0, _gridOverlay)` и `Add(_selectionOverlay)`), сбивая 1:1 соответствие между `Widgets` и `Children` и приводя к удалению оверлея вместо виджета
  - [x] Архитектурная изоляция оверлеев: вынос `GridOverlay` и `SelectionOverlay` из `DashboardPanel.Children` в трехуровневый `<Grid>` в `DashboardView.axaml` (`GridOverlay` снизу, `ItemsControl` по центру, `SelectionOverlay` сверху)
  - [x] Рефакторинг `DashboardPanel.cs`: полное удаление манипуляций с `Children`, удаление `EnsureOverlaysState()`, обеспечение чистоты коллекции `Children` (только контейнеры виджетов)
  - [x] Рефакторинг `GridOverlay.cs` и `SelectionOverlay.cs`: поддержка автономного конструктора без параметров, автоматический поиск родителя `DashboardView`/`DashboardPanel` и явный метод связывания `LinkOverlays()`
  - [x] Связывание в `DashboardView.axaml.cs`: вызов `panel.LinkOverlays(DashboardGridOverlay, DashboardSelectionOverlay)`
  - [x] Исправление порядка выполнения команды удаления в `DashboardPanel.cs`: фиксация удаляемого экземпляра в локальной переменной, вызов `RemoveWidgetCommand.Execute(toRemove)` до сброса `SelectedVm = null`
- [x] Автоматическое тестирование:
  - [x] Добавлен тест №9 в `AvaloniaApplication1.UIValidation/Program.cs`: создание 3 виджетов, поочередное удаление с проверкой мгновенного уменьшения `DashboardPanel.Children.Count` (с 3 до 2, 1, 0) без переключения вкладок
- [x] Верификация:
  - [x] Все 9 тестов `AvaloniaApplication1.UIValidation` пройдены успешно (100%)
  - [x] `scripts/check-build.ps1`: 0 ошибок, 0 предупреждений
  - [x] `scripts/render_ui.ps1 DashboardView`: успешный рендер, артефакты `ui_preview.png` и `ui_tree.json` сгенерированы

# Tasks: Dynamic Pipe Selection Frame & Child Container Synchronization (Сессия 27 — Завершено)

- [x] Устранение десинхронизации трубы и рамки выделения при модификации вершин:
  - [x] Локализована коренная причина (RCA): локальные значения `SetCol/SetRow` на `ContentPresenter` переопределяли Style binding, а отсутствие подписки `DashboardPanel` на `vm.PropertyChanged` приводило к тому, что контейнер оставался на старых координатах после `NormalizePointsAndSize()`, сдвигая трубу относительно канвы
  - [x] Внедрена реактивная синхронизация в `DashboardPanel.cs`: централизованные методы `SubscribeChildVm`, `UnsubscribeChildVm`, `FindChildForVm` и обработчик `OnWidgetVmPropertyChanged` для немедленной актуализации `SetCol`, `SetRow`, `SetSizeX`, `SetSizeY` на `ContentPresenter` при любых изменениях VM
  - [x] Реализована динамическая перерисовка рамки выделения в `SelectionOverlay.cs`: расчет габаритов рамки для `PipeWidgetViewModel` напрямую по реальным координатам `pipeVm.GetAbsoluteGridPoints()`
  - [x] Добавлена инвалидация рамки выделения `_selectionOverlay?.InvalidateVisual()` на каждый тик перемещения вершины в `OnPointerMoved` и при отпускании в `OnPointerReleased`
  - [x] Добавлен метод `NotifyPanelAfterPointsChanged()` в `PipeControl.cs` для контекстных действий добавления/удаления вершин
  - [x] Заблокирован ложный захват ресайза за правый нижний угол трубы в режиме редактирования вершин `IsEditingVertices`
- [x] Автоматическое тестирование:
  - [x] Добавлен тест №10 в `AvaloniaApplication1.UIValidation/Program.cs`: создание трубы (10, 10), добавление вершины со смещением (-3, -2), автоматическая нормализация в (7, 8) и мгновенная синхронизация `GetCol`/`GetRow` контейнера в панели без переключения экранов
  - [x] Добавлена регистрация `IChildWindowService` в DI `Program.cs`
- [x] Верификация:
  - [x] Все 10 автоматических тестов `AvaloniaApplication1.UIValidation` пройдены успешно (100% PASS)
  - [x] Сборка `scripts/check-build.ps1`: **0 ошибок, 0 предупреждений**
  - [x] Рендеринг `scripts/render_ui.ps1 MimicView` и `DashboardView`: успешный рендер, артефакты `ui_preview.png` и `ui_tree.json` сгенерированы и проверены через Vision Inspection
# Tasks: ColorPickerBox + FormatEditorBox Integration (Сессия 28 — Завершено)

- [x] Создан ColorPickerBox UserControl (Views/Controls/ColorPickerBox.axaml + .cs):
  - [x] 36 цветовых ячеек (6x6 UniformGrid) с палитрой HMI/SCADA
  - [x] RGB-слайдеры с NumericUpDown для точного ввода каждого канала (0-255)
  - [x] Спектральный слайдер Hue (0-360 градусов) с радужным LinearGradientBrush
  - [x] Hex TextBox для прямого ввода цвета с двунаправленной синхронизацией
  - [x] SelectedColorHex StyledProperty (TwoWay) — точка интеграции с VM
  - [x] HSV/RGB математика без внешних зависимостей; флаг isInternalUpdate для предотвращения петли
- [x] Создан FormatEditorBox UserControl (Views/Controls/FormatEditorBox.axaml + .cs):
  - [x] MenuFlyout с 11 категориями (~50 пресетов): Температура, Давление, Расход, Уровень, Масса, Химия, Электрика, Обороты, Объем, Время, Числа
  - [x] 5 кнопок быстрой точности: 0 / .0 / .00 / .000 / Авто (F0/F1/F2/F3/{0})
  - [x] Умная замена точности через Regex с сохранением единицы измерения
  - [x] Format StyledProperty (TwoWay) — точка интеграции с VM
- [x] Интегрированы оба контрола в WidgetEditorWindow.axaml:
  - [x] Добавлен xmlns:controls для namespace Controls
  - [x] Format -> FormatEditorBox (раздел Value Display Settings)
  - [x] ValueColor -> ColorPickerBox (Value Display Settings)
  - [x] TrueColor / FalseColor -> ColorPickerBox (PilotLight Settings)
  - [x] ActiveColor / InactiveColor -> ColorPickerBox (Mimic Color Settings)
- [x] Верификация:
  - [x] Сборка: 0 ошибок, 0 предупреждений
  - [x] Все 10 тестов UIValidation PASS
  - [x] Коммит 34e8ea7, запушен в origin/main

# Tasks: Project Management in Design Mode (Сессия 29 — Выполнено)

- [x] Расширение сервиса конфигурации `IConfigurationService`:
  - [x] Добавить свойство `CurrentFilePath`
  - [x] Поддержка произвольных путей в `LoadConfigurationAsync` и `SaveConfigurationAsync`
- [x] Интеграция системных диалогов открытия/сохранения в `IDialogService` и `DialogService`:
  - [x] Метод `ShowOpenProjectDialogAsync()` через `StorageProvider.OpenFilePickerAsync`
  - [x] Метод `ShowSaveProjectAsDialogAsync()` через `StorageProvider.SaveFilePickerAsync`
  - [x] Фильтрация по типам файлов `*.json` («Файлы проекта HMI»)
- [x] Реализация проектных команд и свойств в `MainViewModel`:
  - [x] Свойства `CurrentProjectName` и `WindowTitle`
  - [x] Команда `OpenProjectCommand` с корректной остановкой/перезапуском рантайма `DataCoreService` и утилизацией дашбордов
  - [x] Команда `SaveConfigAsCommand`
  - [x] Обновление `SaveConfigCommand` с выводом Toast-уведомления
- [x] Размещение элементов управления в `MainWindow.axaml`:
  - [x] Бейдж активного файла проекта `📁 <имя>`
  - [x] Кнопки `📂 Загрузить...`, `💾 Сохранить`, `💾 Сохранить как...`
  - [x] Горячие клавиши `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`
  - [x] Привязка заголовка окна к `WindowTitle`
- [x] Устранение всех предупреждений компилятора CS8602 и CS8604
- [x] Проверка успешной сборки проекта

# Tasks: MMA Tank Farm HMI Configuration & RealLab Debian 9 Deployment (Сессия 30 — Выполнено)

- [x] Анализ проектной документации парка хранения ММА (4 емкости 500 м³):
  - [x] Изучение чертежей P&ID, спецификаций заказчика `СП-21-Монтаж 4-х емкостей ММА 500 м3` и параметров панели RealLab NLcon-LXD12-IP65 (лист 10)
  - [x] Анализ C++ исходников ПЛК Wiren Board 8 (`IR.hpp`, `HR.hpp`, `CL.hpp`, `IN.hpp`) и 100% маппинг карты адресов Modbus TCP (`30001`–`30065`, `40001`–`40029`, `00001`–`00019`, `10001`–`10031`)
- [x] Проектирование и создание HMI-конфигурации `config_mma_park.json`:
  - [x] Настройка подключения к ПЛК Wiren Board 8 (Modbus TCP `10.10.10.234:502`, порядок байт `CDAB`, unit 1, опрос 200 мс) и MQTT брокеру (`10.10.10.234:1883`)
  - [x] Адаптация геометрии мнемосхемы Mimic под экран панели 12.1" 1024x768 (емкости поз. 22–25, насосы 502в,г,д, краны отсечные LV 22..25, сливные V 22..25, расходомеры FT 1-1, FT 2-1)
  - [x] Создание операторского дашборда (уровни емкостей, тренды расхода, азотная подушка, дозирование налива, аварии ПАЗ)
  - [x] Размещение файла в `AvaloniaApplication1/config_mma_park.json` и в папке проекта `C:\Users\adm\Desktop\Парк хранения ММА4 емкости\`
- [x] Разработка руководства по развертыванию под панель RealLab (Debian 9 Stretch):
  - [x] Анализ аппаратной части (Broadcom BCM2837B0, Cortex-A53 @ 1.2 ГГц) и ограничений glibc 2.24 в Debian 9
  - [x] Документирование трех путей сборки/миграции (.NET 8 self-contained под Debian 11/12, таргетинг .NET 6.0 под Debian 9, запуск с изолированным glibc 2.28+)
  - [x] Инструкции по настройке LinuxFB / X11 и автозапуску через systemd в `docs/reallab_panel_guide.md`
  - [x] Создание сборочных скриптов `scripts/build_reallab_panel.ps1` и `scripts/build_reallab_panel.sh`
- [x] Автоматическое тестирование и верификация:
  - [x] Добавлен тест №11 в `AvaloniaApplication1.UIValidation/Program.cs`: валидация JSON-структуры `config_mma_park.json`, проверка параметров связи и создание ViewModel виджетов в памяти
  - [x] Успешное прохождение всех 11 автоматических тестов UIValidation (100% PASS)
  - [x] Сборка решения: 0 ошибок, 0 предупреждений

# Tasks: Top Bar Visibility, Viewport Overflow Fix & Debug Auto-Design Mode (Сессия 31 — Выполнено)

- [x] Анализ проблемы отображения кнопки «Загрузить...» и панели управления проектами:
  - [x] Диагностика через Avalon MCP (`lint_ui`, `inspect_ui`): обнаружено скрытие кнопки `📂 Загрузить...` и бейджа проекта `📁 CurrentProjectName` в режиме Runtime (`IsDesignMode == false` по умолчанию на старте)
  - [x] Локализация `VIEWPORT_OVERFLOW` и `ZERO_BOUNDS`: правая панель шириной >970 px вытесняла левую навигацию за пределы экрана при разрешениях <1500 px
- [x] Оптимизация верхней панели и адаптивная верстка в `MainWindow.axaml`:
  - [x] Установка базовых габаритов окна (`Width="1400" Height="900" MinWidth="1024" MinHeight="600" WindowStartupLocation="CenterScreen"`)
  - [x] Снятие ограничения `IsDesignMode`: кнопка `📂 Загрузить...` и бейдж активного проекта `📁 CurrentProjectName` теперь отображаются **всегда**
  - [x] Рефакторинг `Grid ColumnDefinitions="Auto,*,Auto"` с компактными отступами и иконками, исключающими выпадение за пределы экрана на 1024x768 и 1280x800
- [x] Автоматический вход в режим редактирования при отладке:
  - [x] В `ProjectContextService.cs` добавлена директива `#if DEBUG` с `_isDesignMode = true` по умолчанию при запуске под Debug / AnyCPU
- [x] Верификация:
  - [x] Проверка разметки через Avalon MCP: 0 ошибок, 0 предупреждений на 1024 и 1280
  - [x] Headless UI-рендеринг `MainWindow` подтвердил идеальное визуальное отображение `artifacts/ui_preview.png`
  - [x] Чистая сборка Debug и Release: 0 ошибок, 0 предупреждений

# Tasks: RealLab Panel (1024x768) Mimic Calibration & Layout Optimization (Сессия 32 — Выполнено)

- [x] Полная калибровка геометрии мнемосхемы Mimic парка хранения ММА под разрешение 1024x768 (1024x724 чистый вьюпорт):
  - [x] Пересчет всех координат элементов по сетке `CellSize = 10` в диапазоне `X: 0..98` (980 px) и `Y: 0..66` (660 px)
  - [x] Устранение наложения расходомеров FT1-1 на магистраль приема ММА (разнос по строкам `Row: 1` и `Row: 6`)
  - [x] Устранение наложения температурных датчиков `Т 22..25` на сливные клапаны `V 22..25` (смещение индикаторов вправо на `Col: 12, 28, 44, 60`)
  - [x] Разнос и выравнивание насосов 502в,г,д (`Row: 48`), датчиков давления (`Row: 56`) и кнопок «Пуск»/«Стоп» (`Row: 61`, ширина 60 px) с гарантированным зазором над нижним краем экрана
  - [x] Смещение блока налива автоцистерн и датчиков азота влево (`Col: 70..97`), исключающее обрезку по правому краю экрана
- [x] Устранение гонок асинхронной загрузки в `MainViewModel.cs`:
  - [x] Внедрение счетчика версий `_loadVersion` для исключения перезаписи активного проекта фоновыми загрузками
  - [x] Открытие публичного метода `LoadConfigAsync(string? filePath = null)`
- [x] Синхронизация файлов конфигурации:
  - [x] Обновление `AvaloniaApplication1/config_mma_park.json` в воркспейсе
  - [x] Копирование в `C:\Users\adm\Desktop\Парк хранения ММА4 емкости\config_mma_park.json`
  - [x] Копирование в проект Visual Studio `C:\Users\adm\projects\AvaloniaApplication1\AvaloniaApplication1\config_mma_park.json`
- [x] Тестирование и визуальная верификация:
  - [x] Настройка тестового раннера `AvaloniaApplication1.UIValidation` на рендеринг при разрешении 1024x768
  - [x] Прохождение всех 11 автоматических тестов UIValidation (100% PASS)
  - [x] Визуальная инспекция скриншота `artifacts/ui_preview.png`: идеальное расположение всех 4 емкостей, насосов, клапанов и трубопроводов
  - [x] Сборка решения: 0 ошибок, 0 предупреждений

# Tasks: Dual Limit Switch Valve Feedback (SQH/SQL) & 4-State Fault Logic (Сессия 33 — Выполнено)

- [x] Модель конфигурации и архитектура данных:
  - [x] Добавить свойство `ClosedFeedbackSource` в `ValveConfig.cs`
  - [x] Поддержать `ClosedFeedbackConnectionId` и `ClosedFeedbackAddress` в `WidgetEditorViewModel.cs` и `WidgetEditorWindow.axaml`
  - [x] Внедрить 4-позиционную дискретную логику в `ValveWidgetViewModel.cs`:
    - [x] `SQH=1, SQL=0` $\rightarrow$ Открыт (`#00E676`)
    - [x] `SQH=0, SQL=1` $\rightarrow$ Закрыт (`#D50000`)
    - [x] `SQH=0, SQL=0` $\rightarrow$ В пути / транзит (`#FFB300`)
    - [x] `SQH=1, SQL=1` $\rightarrow$ Авария концевиков / недостоверность (`#FF1744`, автоматическая активация тревоги)
  - [x] Реализовать контроль рассогласования команды управления и концевиков
  - [x] Перевести обработку тегов на безопасный маршалинг `Dispatcher.UIThread.Post(...)`
- [x] Визуализация и элементы управления:
  - [x] Добавить свойства `IsMoving` и `IsSensorFault` в `ValveControl.cs` с регистрацией в `AffectsRender`
  - [x] Отрисовка янтарного цвета `#FFB300` при ходе клапана и аварийного `#FF1744` при конфликте датчиков
  - [x] Добавить привязки в `DashboardView.axaml`
  - [x] Расширить окно попапа управления `ValveControlPopupView.axaml` и `ValveControlPopupViewModel.cs` визуальными LED-индикаторами концевиков SQH и SQL
- [x] Интеграция с ПЛК Wiren Board 8:
  - [x] Внести реальные дискретные входы (DI 10016-10025) для LV 22..25 и отсечного крана налива в `config_mma_park.json`
  - [x] Синхронизировать файл конфигурации в `C:\Users\adm\Desktop\Парк хранения ММА4 емкости\` и Visual Studio
- [x] Тестирование и верификация:
  - [x] Разработать тест №12 в `AvaloniaApplication1.UIValidation/Program.cs` для верификации всех 4 состояний и попапа
  - [x] Успешный прогон всех 12 тестов UIValidation (12/12 PASS, 100%)
  - [x] Headless-рендеринг `MimicView` и `ValveControlPopupView`
  - [x] Сборка решения: 0 ошибок, 0 предупреждений

# Tasks: 511 ContainerButtons, Settings Window, Password Protection & Runtime UI (Сессия 34 — Выполнено)

- [x] Реорганизация функций 511 аппарата:
  - [x] Упаковка 8 SetValue виджетов в 2 `ContainerButton` («🎛️ Уставки дозирования и нагрева», «⚙️ Параметры ПИД и pH»)
  - [x] Синхронизация `config.json` и `Assets/default_config.json`
- [x] Режим запуска приложения:
  - [x] Установка дефолтного запуска в режиме Runtime (`IsDesignMode = false`) в `ProjectContextService.cs`
- [x] Защита режима редактирования паролем:
  - [x] Добавление `SecurityConfig` (`RequirePasswordForDesignMode`, `DesignModePassword`) в `HmiConfiguration.cs`
  - [x] Создание `PasswordPromptViewModel.cs` и `PasswordPromptWindow.axaml` с виртуальным PIN-падом
  - [x] Запрос пароля при переключении `ToggleDesignModeCommand` в `MainViewModel.cs`
- [x] Окно настроек (`SettingsWindow`):
  - [x] Разработка `SettingsViewModel.cs` и `SettingsWindow.axaml` с 4 вкладками (Проект, Мнемосхема, Безопасность, Связь)
  - [x] Интеграция команд вызова `OpenSettingsCommand` (`Ctrl+,`)
- [x] Очистка панели управления в Runtime:
  - [x] Скрытие вкладок дашборда, кнопок загрузки/сохранения и симуляции в режиме исполнения
  - [x] Отображение только бейджа `📁 {CurrentProjectName}` и кнопки входа в режим редактирования
- [x] Тестирование и верификация:
  - [x] Разработан автоматический тест №13 в `AvaloniaApplication1.UIValidation`
  - [x] Успешное выполнение всех 13 тестов (13/13 PASS, 100%)
  - [x] Headless-рендеринг `MainWindow`, `SettingsWindow`, `PasswordPromptWindow`
  - [x] Чистая сборка: 0 ошибок, 0 предупреждений

# Tasks: Fix Design Mode Button Freeze, App Icon & Live Header Status (Сессия 35 — Выполнено)

- [x] Исправление бага блокировки кнопки Design Mode:
  - [x] Обработка `window.Closed` и `childWindow.CloseAction` в `DialogService.cs` с гарантированным завершением задачи `TrySetResult(false)`
  - [x] Оборачивание `ToggleDesignModeAsync` в блок `try-finally` с гарантированным вызовом `ToggleDesignModeCommand.NotifyCanExecuteChanged()`
- [x] Фирменная иконка приложения HMI SCADA:
  - [x] Генерация иконки `Assets/app_icon.png` (256x256) через SkiaSharp
  - [x] Привязка иконки к `MainWindow.axaml` (`Icon="/Assets/app_icon.png"`)
- [x] Панель оперативной телеметрии в шапке окна:
  - [x] Посекундный таймер в `MainViewModel.cs` для обновления живого времени (`HH:mm:ss`) и даты (`dd.MM.yyyy`)
  - [x] Бейджи статуса связи с ПЛК (`🟢 Онлайн` / `🟡 Симуляция`) и аварий (`🔔 0 Аварий`)
  - [x] Информативный заголовок окна OS `[Проект] — HMI SCADA | Онлайн | Runtime | 12:00:00`
- [x] Верификация и тесты:
  - [x] Успешное выполнение всех 13 тестов UIValidation (13/13 PASS, 100%)
  - [x] Визуальная инспекция скриншота `artifacts/ui_preview.png`
  # Tasks: Responsive Typography, Russian Localized UI, MDI Calculator & Driver Connection Health (Сессия 36 — Выполнено)

- [x] Адаптивная типографика и устранение срезания/наездов текста:
  - [x] Расширение `WidgetConfig` свойствами: `FontSize`, `AutoScaleText`, `ShowTitle`, `ShowBorder`, `LabelPosition`, `CompactMode`
  - [x] Расширение `CommandButtonConfig`, `ValueDisplayConfig`, `ValveConfig`, `TankConfig`, `PumpConfig`, `ProjectConfig` (`IsCompactMode`)
  - [x] Внедрение класса кнопок `.hmi-compact` с минимальными внутренними отступами (`Padding="2,1"`)
  - [x] Оборачивание текстовых меток виджетов в `Viewbox Stretch="Uniform"` для гарантированного автоматического вписывания
  - [x] Устранение жесткого выноса подписей клапанов (`Margin="0,0,0,-24"`) с переходом на структурированный `Grid RowDefinitions="*,Auto"`
- [x] Русскоязычный интерфейс и контекстные подсказки:
  - [x] Полная русификация всех надписей, кнопок и форм (`WidgetEditorWindow`, `SettingsWindow`, `MainWindow`)
  - [x] Добавление подробных подсказок `ToolTip.Tip` для оператора (горячие клавиши, назначение элементов)
  - [x] Актуализация раздела помощи: создана вкладка «❓ Справка» в `SettingsWindow` с перечнем горячих клавиш и регламентом работы
- [x] Инженерно-технологический калькулятор (MDI):
  - [x] Разработка `CalculatorViewModel.cs` и `CalculatorView.axaml` (базовые расчеты $+,-,\times,\div,\sqrt{},\pm,\text{C},\leftarrow$ + расчет массы жидкости $m=V\times\rho$ и конвертер давлений бар/МПа/кПа/кгс/см²)
  - [x] Кнопка вызова `🧮` в шапке окна и горячая клавиша `F10` с автофокусом/BringToFront
- [x] Перенос симулятора в настройки:
  - [x] Удаление кнопок симуляции из шапки `MainWindow.axaml`
  - [x] Размещение управления симулятором во вкладке «🎮 Симуляция» в `SettingsWindow.axaml`
- [x] Динамический статус связи:
  - [x] Отслеживание активных подключений в `MainViewModel.cs` (`🟢 Онлайн ({count})` / `⚪ Нет связей` / `🟡 Симуляция`)
  - [x] Кликабельный бейдж связи в шапке для быстрого вызова менеджера подключений
- [x] Тестирование и верификация:
  - [x] Разработан автоматический тест №14 в `AvaloniaApplication1.UIValidation/Program.cs`
  - [x] Успешное выполнение всех 14 тестов (14/14 PASS, 100%)
  - [x] Headless-рендеринг `MainWindow` и `SettingsWindow` в `artifacts/ui_preview.png`
  # Tasks: Calculator Polish — Memory, History Journal, Keyboard Input & HMI Clipboard (Сессия 37 — Выполнено)

- [x] Изолированный HMI-буфер обмена C# без обращения к системному буферу ОС (Linux Debian ARM Kiosk ready):
  - [x] Создан интерфейс `IHmiClipboardService` и синглтон-сервис `HmiClipboardService`
  - [x] Регистрация в DI `App.axaml.cs` и внедрение в `MainViewModel`, `CalculatorViewModel`, `NumpadViewModel`, `DialogService`
- [x] Функции памяти в инженерном калькуляторе (`MC`, `MR`, `M+`, `M-`, `MS`):
  - [x] Реализована логика ячейки памяти `MemoryValue`, индикатор `[M]` на дисплее
  - [x] Размещен компактный ряд кнопок памяти в `CalculatorView.axaml`
- [x] Журнал расчетов (История операций):
  - [x] Структура `CalculationHistoryItem` с фиксацией времени, выражения и результата
  - [x] Боковая выдвижная панель `📜 Журнал` с динамическим расширением MDI-окна (360 -> 560 px) через `OnToggleHistory`
  - [x] Выбор исторического значения в дисплей по клику и кнопка быстрой очистки `🗑️`
- [x] Полноценный ввод с клавиатуры:
  - [x] Клавиатурный обработчик `OnKeyDown` в `CalculatorView.axaml.cs`: цифры 0-9, операции `+,-,*,/`, Enter (`=`), Backspace (`⌫`), Escape (закрытие окна), `Ctrl+C` (копировать в HMI-буфер), `Ctrl+V` (вставить из HMI-буфера)
  - [x] В `NumpadView.axaml.cs`: добавлена обработка горячей клавиши `Ctrl+V`
- [x] Интеграция с задатчиками уставок (`SetValue` / `Numpad`):
  - [x] Кнопка `📋 В уставку` на панели калькулятора и в блоках технологических расчетов (масса, давление)
  - [x] В `NumpadView.axaml` добавлена компактная кнопка `📥 Вставить из калькулятора: {значение}`
- [x] Тестирование и верификация:
  - [x] Разработан автоматический тест №15 в `AvaloniaApplication1.UIValidation/Program.cs`
  - [x] Успешное прохождение всех 15 тестов (15/15 PASS, 100%)
  - [x] Headless-рендеринг `CalculatorView`, `NumpadView`, `MainWindow` в `artifacts/ui_preview.png`
  - [x] Чистая сборка: 0 ошибок, 0 предупреждений

# Tasks: Calculator History Session Persistence, Explicit Calculation Buttons & Separate Tech Journal (Сессия 38 — Выполнено)

- [x] Сохранение истории расчетов между открытиями/закрытиями калькулятора:
  - [x] Внедрение кэширования `_cachedCalculatorVm` в `MainViewModel.cs` для сохранения журналов расчетов, регистров памяти и введенных параметров в течение всей сессии
  - [x] Корректное повторное связывание `CloseAction` и `OnToggleHistory` при повторном открытии MDI-окна
- [x] Кнопки явного расчета и запись в журнал:
  - [x] Добавление команд `CalculateTechMassCommand` и `CalculatePressureCommand` в `CalculatorViewModel.cs`
  - [x] Запись выражений и результатов в технологический журнал при клике на кнопки «⚡ Рассчитать»
- [x] Отдельный технологический журнал расчетов:
  - [x] Создание независимой коллекции `TechHistoryLog` для вкладки «📐 Тех. расчеты»
  - [x] Боковая выдвижная панель `📜 Тех. журнал` с динамическим расширением окна MDI (360 -> 560 px)
  - [x] Возможность быстрой очистки журнала `🗑️` и копирования результата в буфер HMI по клику
  - [x] Оптимизация компоновки: кнопки «⚡ Рассчитать» на всю ширину блока, устранение срезания текста и таймстампов
- [x] Тестирование и верификация:
  - [x] Расширен автоматический тест №15 в `AvaloniaApplication1.UIValidation/Program.cs` с проверкой сохранения сессии и `TechHistoryLog`
  - [x] Успешное выполнение всех 15 тестов (15/15 PASS, 100%)
  - [x] Headless-рендеринг `TechCalculatorView` и `MainWindow`
  - [x] Чистая сборка: 0 ошибок, 0 предупреждений

# Tasks: Multiselect, Rubber-Band Selection & Group Move in Design Mode (Сессия 39 — Выполнено)

- [x] Режим мультивыбора и резиновой рамки выделения (Rubber-band selection box):
  - [x] Реализована протяжка резиновой рамки выделения мышью по свободному пространству мнемосхемы (`_isBoxSelecting`, `SelectionBoxRect`) в `DashboardPanel.cs`
  - [x] Точечный мультивыбор виджетов с зажатым `Ctrl` / `Shift`
  - [x] Кнопка-тумблер `ToggleButton` «🗂️ Мультивыбор» на плавающей панели инструментов мнемосхемы в режиме редактирования (`IsMultiSelectMode`)
  - [x] Оптимизация `SelectionOverlay.cs` с кэшированием всех `Pen` и `SolidColorBrush` в статических неизменяемых полях (нулевые аллокации в `Render()`)
  - [x] Отрисовка контуров и маркеров для всех выделенных элементов группы и пунктирной рамки выделения
- [x] Групповое перемещение и операции с группой:
  - [x] Совместный драг всех выделенных виджетов (`_dragOriginalPositions`) с сохранением относительного расстояния
  - [x] Синхронное перемещение вершин трубопроводов, входящих в группу (`_dragOriginalPipePoints`)
  - [x] Автоматический rubber-banding внешних трубопроводов, подключенных к перемещаемому оборудованию
  - [x] Защита от выхода за границы холста (`Col >= 0`, `Row >= 0`) для всех элементов группы
  - [x] Групповое удаление по клавише `Delete` и команде `RemoveSelectedWidgetsCommand`
- [x] Инспектор группы:
  - [x] Плавающая карточка «Выделено объектов: N» (`HasMultiSelection`) со сводкой типов, кнопками «🗑️ Удалить группу» и «✖ Снять»
- [x] Тестирование и верификация:
  - [x] Разработан автоматический тест №16 в `AvaloniaApplication1.UIValidation/Program.cs`
  - [x] Успешное выполнение всех 16 тестов (16/16 PASS, 100%)
  - [x] Headless-рендеринг `DashboardView` в `artifacts/ui_preview.png`
  - [x] Чистая сборка: 0 ошибок, 0 предупреждений

# Tasks: CAD/SCADA Selection System, Alignment, Nudge, Group & Duplicate (Сессия 40 — Выполнено)

- [x] Отраслевая двунаправленная рамка выделения (AutoCAD / TIA Portal standard):
  - [x] Слева направо: Window Selection (синяя полупрозрачная рамка, сплошная линия), выделение строго только полностью попавших объектов
  - [x] Справа налево: Crossing Selection (зеленая рамка, пунктирная линия), выделение всех касающихся и пересеченных объектов
  - [x] CAD-обработка трубопроводов (Pipe): при Window selection труба выделяется только если ВСЕ точки и сегменты внутри; при Crossing selection — если рамка пересекает хотя бы одну линию трубы
- [x] Визуализация группы и Group Bounding Box:
  - [x] Отрисовка единого внешнего контейнера группы с 8 маркерами манипуляций (4 угла + 4 середины сторон)
  - [x] Мягкая ненавязчивая подсветка входящих в группу виджетов без визуального загромождения
  - [x] Нулевые аллокации в `SelectionOverlay.Render()` (кэшированные статические кисти и перья)
- [x] Инструменты выравнивания и распределения в тулбаре мнемосхемы:
  - [x] Выравнивание: по левому краю (`AlignLeft`), по правому краю (`AlignRight`), по центру гор. (`AlignCenterHorizontal`), по верхнему краю (`AlignTop`), по нижнему краю (`AlignBottom`), по центру верт. (`AlignCenterVertical`)
  - [x] Равномерное распределение: `DistributeHorizontally` и `DistributeVertically`
- [x] Клавиатурное позиционирование стрелками (Nudge):
  - [x] Стрелки клавиатуры смещают все выделенные виджеты группы на 1 клетку сетки
  - [x] Стрелки с зажатым `Shift` смещают группу на 5 клеток сетки
  - [x] Контроль границ холста (`Col >= 0`, `Row >= 0`)
- [x] Логическая постоянная мета-группировка SCADA (Group / Ungroup):
  - [x] Свойство `GroupId` в `WidgetConfig` и `WidgetViewModelBase`
  - [x] Команды `GroupSelectedWidgetsCommand` и `UngroupSelectedWidgetsCommand`
  - [x] Клик по любому объекту группы выделяет группу целиком
- [x] Групповое дублирование:
  - [x] Горячая клавиша `Ctrl+D` и кнопка `📋 Копия` дублируют группу со смещением на +2, +2 клетки
- [x] Тестирование и верификация:
  - [x] Разработан комплексный автоматический тест №17 в `AvaloniaApplication1.UIValidation/Program.cs`
  - [x] Успешное выполнение всех 17 тестов (17/17 PASS, 100%)
  - [x] Headless-рендеринг `DashboardView` и `MainWindow` в `artifacts/ui_preview.png`
  - [x] Чистая сборка: 0 ошибок, 0 предупреждений

# Tasks: Live Drag, Smart Alignment Guides, Coordinate Badge & Header Grip (Сессия 41 — Выполнено)

- [x] Интерактивное перемещение в реальном времени (Live Drag):
  - [x] Замена маленького эскиза сетки `GridOverlay` на прямое перемещение виджетов в `DashboardPanel.cs`
  - [x] Синхронное резиновое смещение присоединенных трубопроводов на лету при перемещении
  - [x] Синхронное смещение группы выделенных виджетов и внутренних труб
  - [x] Безопасный откат позиций в `OnPointerCaptureLost`
- [x] Умные динамические направляющие выравнивания (Smart Alignment Guides):
  - [x] Вычисление совпадений границ (Left, Center, Right) и горизонталей (Top, Middle, Bottom) с соседними элементами
  - [x] Отрисовка динамических пурпурных линий (`SmartGuideLine`) в `SelectionOverlay` (Zero-Allocation в `Render()`)
- [x] Индикатор координат перемещения (Drag Coordinate Badge):
  - [x] Бейдж `(X, Y)` рядом с курсором при перемещении элементов
- [x] Удобные зоны захвата (Header Grip + Border Frame):
  - [x] Убран темный непрозрачный оверлей (`Background="{x:Null}"`), кнопки виджетов доступны для кликов
  - [x] Добавлена верхняя плашка-ручка с иконкой `⋮⋮` (`Header Grip`) для быстрого и четкого перетаскивания
- [x] Автоматическое тестирование и рендеринг:
  - [x] Комплексный тест №18 в `AvaloniaApplication1.UIValidation/Program.cs`
  - [x] 18/18 тестов пройдено успешно (100% PASS)
  - [x] Headless-рендер `DashboardView` и валидация UI в `artifacts/ui_preview.png`

# Tasks: Unified Adaptive Bottom Toolbar (Сессия 42 — Выполнено)

- [x] Консолидация нижней панели инструментов мнемосхемы (`DashboardView.axaml`):
  - [x] Полная ликвидация отдельной центральной плавающей карточки мультивыбора (`HorizontalAlignment="Center" VerticalAlignment="Bottom"`)
  - [x] Объединение постоянных элементов масштаба и контекстных инструментов работы с выделением в единый цельный `Border`
  - [x] Использование адаптивного `WrapPanel` с `Orientation="Horizontal"` для предотвращения коллизий и переноса строк при сжатии
- [x] Контекстная адаптивность:
  - [x] Компактный вид панели (~400 px) при отсутствии выделения (`HasMultiSelection = false`)
  - [x] Отображение индикатора «Выделено: N [типы виджетов]», кнопок выравнивания, распределения, группировки и действий строго при наличии выделения (`HasMultiSelection = true`)
- [x] Сохранение полного набора команд:
  - [x] Выравнивание: `AlignLeft`, `AlignCenterHorizontal`, `AlignRight`, `AlignTop`, `AlignCenterVertical`, `AlignBottom`
  - [x] Распределение: `DistributeHorizontally`, `DistributeVertically`
  - [x] Группировка: `GroupSelectedWidgets`, `UngroupSelectedWidgets`
  - [x] Операции: `DuplicateSelectedWidgets`, `RemoveSelectedWidgets`, `ClearSelection`
- [x] Верификация и тестирование:
  - [x] Сборка: `dotnet build AvaloniaApplication1/AvaloniaApplication1.csproj` (0 ошибок, 0 предупреждений)
  - [x] Автоматизированные тесты: `AvaloniaApplication1.UIValidation` (18/18 PASS)
  - [x] Headless-рендер: `scripts/render_ui.ps1 DashboardView` (чистый скриншот без перекрытий)








