# КОДЕКС РАЗРАБОТКИ И ПРАВИЛА ПРОЕКТА (C# / .NET / AVALONIA 11+)

---

## ⚡ 1. Автономность и минимизация подтверждений (Zero Friction / Auto-Execution)

* **Автономное выполнение:** Агент ОБЯЗАН выполнять терминальные команды (`dotnet build`, `dotnet test`, `git status`, `git diff`, `git add`, `git commit`, `git push`, скрипты сборки и рендеринга), чтение и правку файлов проекта автономно и проактивно БЕЗ предварительных вопросов и запросов разрешения у пользователя.
* **Действие вместо лишних вопросов:** Запрещено задавать промежуточные вопросы вида «Запустить ли сборку?», «Сделать ли коммит?», «Разрешите выполнить команду?». Если действие необходимо для решения инженерной задачи — выполнять его немедленно.
* **Исключения (только деструктивные действия):** Запрашивать подтверждение ТОЛЬКО при операциях с риском безвозвратной потери данных (`git reset --hard`, удаление веток, `git push --force` или удаление пользовательских файлов).
* **Журналирование:** При любой работе с репозиторием обязательно вести журнал разработки в `docs/development_log.md` и трекер задач в `docs/task.md`.

---

## 🏗️ 2. Архитектура и специфика SCADA / HMI

### 2.1 Clean Architecture и распределение слоев
1. **Domain Layer**: 
   * Чистая бизнес-модель, сущности, DTO (`TagData`, конфигурации), интерфейсы драйверов (`IProtocolDriver`).
   * Строго изолирован от UI и инфраструктуры (никаких зависимостей от Avalonia, MQTTnet, NModbus).
2. **Application / Services Layer**:
   * `DataCoreService` — единая шина событий и реактивный концентратор тегов (`IObservable<TagData>`).
   * `SimulationService` — генерация технологических данных и симуляция процессов.
   * `ConfigurationService` — сохранение/загрузка конфигурации мнемосхем (`config.json`).
   * `IProtocolDriverFactory` — фабрика промышленных протоколов (Modbus TCP/RTU over TCP, MQTT).
   * `PipeAutoRouter` — алгоритмическая автотрассировка технологических трубопроводов по ортогональной сетке.
3. **Presentation Layer**:
   * Мнемосхема (`DashboardView`, `DashboardPanel`): технологические аппараты (`ValveControl`, `PumpControl`, `ReactorControl`, `HeatExchangerControl`, `LevelSensorControl`, `PipeControl`, `TrendLineControl`).
   * Поддержка режимов исполнения и редактирования (`IsDesignMode`), Drag & Drop, изменение размеров, перемещение по слоям (Z-Order: `BringToFront`, `SendToBack`), единое контекстное меню.
   * Окна редактора конфигураций (`ConnectionManagerWindow`, `WidgetEditorWindow`, `ConnectionEditorWindow`, `ValveControlPopupView`).
4. **Запрет Service Locator:**
   * Все зависимости внедряются через конструктор (`Constructor Injection`).
   * Запрещено использовать `serviceProvider.GetRequiredService<T>()` как скрытую замену DI внутри бизнес-кода.

---

## 🎨 3. Стандарты Avalonia 11+ и современного C#

### 3.1 Стек и фреймворк
* **Платформа:** .NET 8 / C# 12 / Avalonia UI строго 11+ (12.0.4).
* **Запрет legacy-кода:** Категорически запрещен синтаксис WPF, UWP или старой Avalonia 0.10.
* **MVVM:** Базируется на `CommunityToolkit.Mvvm` с использованием source generators:
  * Модели представления наследуются от `ObservableObject` / `ViewModelBase`.
  * Свойства: `[ObservableProperty] private string _name;` (генерирует `Name` и `OnNameChanged`).
  * Команды: `[RelayCommand]` с поддержкой асинхронности (`Task`) и `CanExecute`.

### 3.2 Строгие ограничения XAML и Avalonia Property System
* **Запрет синтаксиса WPF:**
  * Запрещены `DependencyProperty.Register`, `<Style.Triggers>`, `Trigger`, `Setter.Value`.
* **Свойства контролов:**
  * Свойства объявляются строго через `StyledProperty<T>.Register<TOwner, T>()` или `DirectProperty<TOwner, T>`.
  ```csharp
  public static readonly StyledProperty<bool> IsActiveProperty =
      AvaloniaProperty.Register<MyControl, bool>(nameof(IsActive));

  public bool IsActive
  {
      get => GetValue(IsActiveProperty);
      set => SetValue(IsActiveProperty, value);
  }
  ```
* **Псевдоклассы Avalonia 11+:**
  * Использовать селекторы нового синтаксиса: `^:pointerover`, `^:pressed`, `^:disabled`, `^:selected`.
  ```xml
  <Style Selector="Button.danger:pointerover">
      <Setter Property="Background" Value="#FF3B30" />
  </Style>
  ```

### 3.3 Compiled Bindings и типизация
* **Строгая компиляция разметки:**
  * В корневых контейнерах (`Window`, `UserControl`) и внутри `DataTemplate` ОБЯЗАТЕЛЕН атрибут `x:DataType="vm:TargetViewModel"`.
  * Флаг компиляции: `x:CompileBindings="True"`.
* **Ошибки компилятора XAMLIL (`AVLNxxxx`):**
  * Ошибки компилятора XAMLIL должны устраняться правильной типизацией, конвертерами (`IValueConverter`) и приведением типов, а НЕ отключением проверки биндингов.

### 3.4 Многопоточность и рендеринг кастомных контролов
* **Маршалинг в UI-поток:**
  * Любые обновления свойств UI из фоновых потоков / телеметрии протоколов маршалируются СТРОГО через:
    `Dispatcher.UIThread.Post(() => { ... })` или `.ObserveOn(RxApp.MainThreadScheduler)`.
* **Высокочастотные данные:**
  * Потоки телеметрии ограничиваются операторами Rx: `.Sample(TimeSpan.FromMilliseconds(50))` / `.Throttle(...)` / `.DistinctUntilChanged()`.
* **Нулевые аллокации в Render:**
  * В методах `Render(DrawingContext context)` кастомных компонентов (`PipeControl`, `ValveControl`, `TrendLineControl`) ЗАПРЕЩЕНО создавать новые объекты `new Pen(...)`, `new SolidColorBrush(...)`, `new FormattedText(...)` или аллоцировать геометрии на каждый кадр.
  * Кисти, перья и вспомогательные структуры должны кэшироваться в полях класса.
  * Для инвалидации перерисовки при изменении свойств обязательно использовать регистрацию:
    `AffectsRender<MyControl>(Property1, Property2);`
* **Очистка ресурсов:**
  * Все подписки и нативные ресурсы должны освобождаться при `DetachedFromVisualTree` или через `CompositeDisposable` в `Dispose()`.

---

## 🔄 4. Self-Verification Loop (Цикл самопроверки через терминал)

После любых правок разметки (`.axaml`) или исходного кода C# (`.cs`) агент обязан автономно выполнить цикл самопроверки:
1. Запуск скрипта проверки сборки:
   * `./scripts/check-build.sh` (Linux/macOS) или `powershell -ExecutionPolicy Bypass -File scripts/check-build.ps1` (Windows).
   * Либо прямая команда: `dotnet build --no-incremental -v:normal /p:AvaloniaShowTrace=true`.
2. Завершение задачи допускается **ТОЛЬКО** при статусе: **0 ошибок, 0 предупреждений (Exit code 0)**.

---

## 👁️ 5. Visual Feedback & UI Debugging Protocol (Avalonia UI)

Вы — автономный инженер интерфейсов на Avalonia UI. Любое изменение разметки (XAML / C# Markup) и стилей должно проходить обязательную автономную валидацию через визуальный цикл обратной связи.

### 5.1 Доступный инструментарий
Для проверки интерфейса в проекте развернут headless-раннер:
* Команда генерации артефактов:
  * PowerShell (Windows): `powershell -ExecutionPolicy Bypass -File scripts/render_ui.ps1 [ViewName]`
  * Bash (Linux/macOS): `./scripts/render_ui.sh [ViewName]`
* Скриншот экрана: `./artifacts/ui_preview.png`
* Дерево элементов (Visual Tree Dump): `./artifacts/ui_tree.json` (содержит типы контролов, Name, Bounds [X, Y, Width, Height], Margin, Padding, IsVisible)

### 5.2 Обязательный цикл работы (TDD для UI)

При получении задачи на изменение или верстку интерфейса строго следуйте регламенту:

1. **Исходный снимок (Baseline):**
   * Соберите текущий проект и сгенерируйте скриншот экрана до правок.
   * Оцените исходное состояние.
2. **Внесение изменений:**
   * Скорректируйте `.axaml`, стили или код View.
   * Соберите проект через `dotnet build`. При ошибках компиляции устраните их до перехода к рендеру.
3. **Снятие и мультимодальный анализ (Vision Inspection):**
   * Запустите скрипт рендеринга для генерации свежего `ui_preview.png` и `ui_tree.json`.
   * Изучите полученное изображение, обращая внимание на:
     - Обрезку текста и компонентов (Clipping).
     - Неправильное выравнивание колонок/строк в Grid (проверка `*`, `Auto`, фиксированных величин).
     - Наложение элементов друг на друга из-за некорректного ZIndex или ошибок позиционирования.
     - Пропорции и растяжение векторных иконок/Path.
     - Корректность контраста и отрисовки активной темы оформления.
4. **Локализация дефекта по дереву элементов:**
   * Если на скриншоте виден визуальный артефакт, найдите соответствующий узел в `ui_tree.json` по координатам Bounds (X, Y, Width, Height).
   * Определите источник проблемы (например, жестко заданная высота Height у родительского StackPanel, блокирующая растяжение).
5. **Итеративное исправление:**
   * Повторяйте правки и повторный запуск рендера до тех пор, пока визуальный результат полностью не совпадет с требованиями задачи.
   * Не отдавайте задачу пользователю без подтверждения успешного финального рендера.

### 5.3 Правила верстки Avalonia
* **Никаких фиксированных размеров для динамического контента:** Избегайте явных Width/Height у текстовых блоков и контейнеров списков; используйте MinHeight, MaxHeight и относительные пропорции Grid.
* **Управление компоновкой:** Для растягивания контента используйте Grid или DockPanel. Помните, что StackPanel зажимает дочерние элементы по направлению своего потока и не растягивает их на оставшееся пространство.
* **Привязки и стили:** Всегда проверяйте, не переопределяет ли локальное свойство контрола централизованный стиль темы.
