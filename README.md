> [!IMPORTANT]
> **ПРАВИЛА ДЛЯ ИИ-АГЕНТОВ (AI AGENT GUIDELINES)**:
> Перед началом любой работы в этом репозитории **ОБЯЗАТЕЛЬНО** изучите документы в каталоге `docs/` для восстановления контекста:
> 1. **[docs/status.md](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/docs/status.md)** — Актуальный статус проекта, дорожная карта (Roadmap) и архитектурная карта.
> 2. **[docs/task.md](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/docs/task.md)** — Текущий ToDo-лист задач.
> 3. **[docs/development_log.md](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/docs/development_log.md)** — Журнал разработки.
> 4. **[docs/README.md](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/docs/README.md)** — Регламент ведения документации.
> 5. **[docs/preprompt.md](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/docs/preprompt.md)** — Системные правила по архитектуре (Avalonia UI / HMI SCADA) и экономии токенов.
>
> Каждый раз при завершении сеанса или перед отправкой финального отчета **вы обязаны** обновить статус в `status.md`, `task.md` и добавить запись в `development_log.md` по регламенту.

# Кроссплатформенная HMI-панель и IoT-шлюз на Avalonia UI

Данный проект представляет собой высокопроизводительную кроссплатформенную HMI-панель (Human-Machine Interface) и IoT-шлюз, разработанный на платформе **.NET 8** с использованием фреймворка **Avalonia UI** и архитектурного паттерна **MVVM** (CommunityToolkit.Mvvm).

Интерфейс спроектирован по принципу адаптивного плиточного дашборда с поддержкой **многодокументарного интерфейса (MDI)** — системы внутренних плавающих окон для отображения вложенных технологических схем (например, газоперекачивающих агрегатов ГПА).

---

## 🏗 Архитектура Проекта (Layers & Directories)

Структура исходного кода разделена на логические уровни в соответствии с MVVM:

```
AvaloniaApplication1/
├── Models/                # Описание конфигурационных схем (JSON-структура)
│   └── Config/
│       ├── ConnectionConfig.cs  # Настройки Modbus/MQTT соединений
│       ├── DashboardConfig.cs   # Сетка виджетов и вложенность
│       ├── HmiConfiguration.cs  # Корневой JSON проект
│       └── WidgetConfig.cs      # Конфигурация отдельной плитки/виджета
│
├── ViewModels/            # Реактивная бизнес-логика компонентов
│   ├── MainViewModel.cs         # Главный экран (список окон, загрузка проекта)
│   ├── ChildWindowViewModel.cs  # Логика плавающего окна (X, Y, ZIndex, Minimize)
│   ├── DashboardViewModel.cs    # Управление сеткой и действиями конструктора
│   ├── ContainerButtonViewModel.cs # Запуск плавающего окна из плитки
│   ├── SetValueViewModel.cs     # Ввод уставки и вызов виртуального Numpad
│   └── *ViewModel.cs            # Модели представления для остальных виджетов
│
├── Views/                 # Визуальное представление (XAML)
│   ├── MainWindow.axaml         # Главное окно, MDI холст и Grid
│   ├── MainWindow.axaml.cs      # Drag-and-drop, Resize и ZIndex логика
│   ├── DashboardView.axaml      # Шаблоны рендеринга каждого виджета
│   ├── DashboardPanel.cs        # Кастомный панельный контейнер для сетки
│   ├── NumpadWindow.axaml       # Встроенный виртуальный нумпад для тач-панелей
│   └── TrendLineControl.cs      # Кастомный легковесный графический контрол
│
└── Services/              # Системные службы и драйверы
    ├── DataCoreService.cs       # Единое ядро связи (Modbus TCP + MQTT)
    ├── ConfigurationService.cs  # Чтение/запись JSON-файла конфигурации
    └── DialogService.cs         # Управление диалоговыми окнами и редакторами
```

---

## 🎛 Подсистема Внутренних Плавающих Окон (MDI / Floating Panels)

Вместо создания тяжелых системных окон ОС, которые вызывают проблемы при работе на встраиваемых Linux-системах (ARM/Raspberry Pi) или Android, все всплывающие экраны (контейнеры ГПА) отображаются внутри общего холста `Canvas` в главном окне.

### 1. Управление окном (Window Chrome)
Каждое плавающее окно ([ChildWindowViewModel](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/ViewModels/ChildWindowViewModel.cs)) содержит:
*   **Заголовок (Title)** для идентификации.
*   **Кнопку Свернуть (🗕)**: при клике состояние `IsMinimized` переключается в `true`. Это временно уменьшает высоту окна до `36px` (высота заголовка) и скрывает его содержимое через привязку `IsVisible="{Binding !IsMinimized}"` в XAML. При повторном клике восстанавливается исходная высота.
*   **Кнопку Закрыть (✕)**: удаляет окно из коллекции `ActiveChildWindows` в `MainViewModel`.

### 2. Drag-and-Drop (Перетаскивание)
Перетаскивание выполняется за область заголовка окна. В [MainWindow.axaml.cs](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/MainWindow.axaml.cs) реализованы обработчики:
*   `OnTitleBarPointerPressed`: захватывает указатель с помощью `e.Pointer.Capture(titleBar)` и запоминает стартовую позицию мыши относительно главного окна, а также исходные координаты `X`/`Y` окна.
*   `OnTitleBarPointerMoved`: рассчитывает смещение (дельта) мыши и обновляет свойства `X` и `Y` во ViewModel. Благодаря двухсторонней привязке (`Mode=TwoWay`) в стилях `ItemsControl` (свойства `Canvas.Left` и `Canvas.Top`), окно плавно перемещается.
*   `OnTitleBarPointerReleased`: отпускает захват указателя и сбрасывает флаги перетаскивания.

### 3. Изменение размеров (Resizing)
В правом нижнем углу окна находится прозрачная зона захвата (Resize Handle) с курсором `BottomRightCorner`. При зажатии кнопки мыши в этой зоне:
*   Активируется режим `_isResizing`.
*   Через аналогичный механизм расчета дельты в `OnResizeHandlePointerMoved` изменяются свойства `Width` и `Height` во ViewModel (с ограничением минимального размера `200x120px` для стабильности).

### 4. Фокусировка и Z-Index Layering (На передний план)
Для того чтобы клик в любую область плавающего окна (включая кнопки, графики и текстовые поля внутри него) поднимал окно на передний план:
1.  Во ViewModel окна добавлено свойство `ZIndex`.
2.  В стилях контейнеров `ItemsControl` в `MainWindow.axaml` прописано:
    `<Setter Property="ZIndex" Value="{Binding ZIndex, Mode=TwoWay}" />`.
3.  В конструкторе `MainWindow` зарегистрирован **тунельный обработчик кликов** на стадии превью (`RoutingStrategies.Tunnel`):
    ```csharp
    AddHandler(PointerPressedEvent, (s, e) => {
        var visual = e.Source as Visual;
        while (visual != null) {
            if (visual is Border border && border.DataContext is ChildWindowViewModel vm) {
                BringToFront(vm);
                break;
            }
            visual = visual.GetVisualParent();
        }
    }, RoutingStrategies.Tunnel);
    ```
    Это решение перехватывает клик на пути "вниз" к целевому контролу (например, кнопке) и вызывает `BringToFront(vm)`. Метод находит максимальный текущий `ZIndex` среди открытых окон и устанавливает выбранному окну `maxZIndex + 1`. Так как визуальный элемент не пересоздается в коллекции, **захват мыши не теряется**, что обеспечивает непрерывность drag-and-drop.
4.  **Разблокировка кликов на фоне**: Холст MDI-окон (`Canvas`) не имеет заданного фона (свойство `Background` равно `null`), что делает пустые зоны холста прозрачными для кликов. Это позволяет пользователю беспрепятственно управлять элементами основного дашборда под плавающими окнами.

---

## 📡 Ядро Связи и Источники Данных (Data Core)

Класс [DataCoreService](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/Services/DataCoreService.cs) является единым драйвером ввода-вывода, реализующим `IDataCoreService` и `IMockDataService`. Двунаправленный обмен данными работает асинхронно.

### 1. Modbus TCP / RTU over TCP (Библиотека NModbus)
*   **Циклический опрос**: Для каждого соединения типа `ModbusTCP` запускается фоновая задача `PollModbusLoop`. Она считывает теги, привязанные к этому соединению, с частотой `PollIntervalMs`.
*   **Чтение**: Поддерживаются функции `0x01` (Coils), `0x02` (Discrete Inputs), `0x03` (Holding Registers) и `0x04` (Input Registers).
*   **Поддержка байтового порядка (Endianness)**: Для 32-битных значений (`Float32`, `Int32`) выполняется перестановка регистров/байтов в соответствии с конфигурацией:
    *   `ABCD` — Прямой порядок (Big-Endian).
    *   `CDAB` — Перестановка слов (Word-Swap).
    *   `BADC` — Байтовая перестановка (Byte-Swap).
    *   `DCBA` — Обратный порядок (Little-Endian).
*   **Запись**: При записи уставки через `WriteCommand` сервис асинхронно подключается к Modbus-устройству, упаковывает значение согласно выбранному Byte Order и записывает его в ПЛК.

### 2. MQTT Клиент (Библиотека MQTTnet)
*   **Подключение**: Сервис подключается к брокеру (по умолчанию настроен хост `stp10`, порт `1883`).
*   **Подписка**: Для каждого MQTT-виджета генерируется индивидуальная подписка на топик с префиксом `gMqt/` (например, `gMqt/ZAS/ZAS_Actual_Power`).
*   **Парсинг**: Содержимое пакета переводится из UTF-8 строки в целевой тип данных (`Float32`, `Int32`, `Bool`). При изменении значения генерируется событие `TagValueChanged`, уведомляющее все подписанные ViewModels.
*   **Публикация**: Команды и уставки публикуются в соответствующие топики с флагом `Retain = true`.

---

## 💾 Конфигурационный Файл JSON

Вся структура проекта хранится в едином файле `config.json`. При его отсутствии служба [ConfigurationService](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/Services/ConfigurationService.cs) автоматически генерирует дефолтную конфигурацию:
*   **Соединение ModbusTCP**: `127.0.0.1:502` (интервал 500мс).
*   **Соединение MQTT**: хост `stp10:1883` (интервал 1000мс).
*   **Виджеты главного экрана**: индикаторы мощности ZAS (`gMqt/ZAS/...`) и 6 контейнерных кнопок для модулей ГПА 1-6.
*   **Виджеты окон ГПА**: каждый вложенный дашборд содержит индикаторы активной мощности, энергии, температуры выхлопа T404, наработки часов, статусных ламп MWM и предупреждений Word55, а также график тренда.

---

## 🎨 Виджеты HMI Панели

1.  **Value Display**: Отображение текстового или числового значения с форматированием (например, `{0:F1} °C`).
2.  **Pilot Light**: Светодиодный индикатор. Меняет цвет заливки (например, зеленый/красный) в зависимости от логического состояния тега (`true`/`false`).
3.  **Command Button**: Кнопка отправки дискретной команды.
4.  **SetValue**: Поле ввода уставки. При клике вызывает виртуальную клавиатуру ([NumpadWindow](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/Views/NumpadWindow.axaml)), позволяющую вводить дробные значения без физической клавиатуры.
5.  **Slider**: Ползунок задания аналогового значения (0-100%).
6.  **Real-Time Trend**: Легковесный график на базе кастомного контрола `TrendLineControl`, который строит графическое представление по точкам из циклического буфера памяти.
7.  **Container Button**: Кнопка папки/подсистемы. При клике в режиме Runtime запускает плавающее окно MDI с вложенным дашбордом.

---

## ⚙ Сборка и Запуск

Сборка осуществляется стандартными средствами .NET CLI:

1.  Восстановление зависимостей и компиляция:
    ```powershell
    dotnet build
    ```
2.  Запуск приложения:
    ```powershell
    dotnet run
    ```
