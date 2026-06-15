# План Доработки (Сессия 4): Уменьшение Сетки Мнемосхемы, Адаптация Труб и Предотвращение Дублирования окон

Внедрение корректировок в режим мнемосхемы для повышения точности позиционирования виджетов и улучшения пользовательского опыта (UX) при работе с плавающими окнами (MDI).

---

## Proposed Changes

### 1. Предотвращение дублирования MDI-окон

#### [MODIFY] [MainViewModel.cs](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/ViewModels/MainViewModel.cs)
* Изменить сигнатуру метода `OpenChildWindow`, чтобы он возвращал созданный `ChildWindowViewModel?`.
* Это позволит вызывающему виджету сохранить ссылку на свое открытое окно.

#### [MODIFY] [ValveWidgetViewModel.cs](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/ViewModels/ValveWidgetViewModel.cs)
* Хранить приватную ссылку `_controlWindow` типа `ChildWindowViewModel?`.
* В методе `OpenControlPopup()`:
  - Если `_controlWindow != null` и оно содержится в `ActiveChildWindows` главного вьюмоделя, удалить его оттуда (закрыть окно) и занулить ссылку.
  - Иначе вызвать `mainVm.OpenChildWindow()`, сохранить возвращенное окно в `_controlWindow` и переопределить его `CloseAction`, чтобы при ручном закрытии (по крестику) ссылка `_controlWindow` занулялась.

#### [MODIFY] [PumpWidgetViewModel.cs](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/ViewModels/PumpWidgetViewModel.cs)
* Реализовать аналогичную логику отслеживания и закрытия окна для насосов.

---

### 2. Сетка 10x10 и масштабирование трубопроводов

#### [MODIFY] [PipeControl.cs](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/Views/PipeControl.cs)
* Сделать толщину трубы (`thickness`), толщину центрального блика (`coreThickness`) и размеры угловых/конечных фланцев зависимыми от свойства `CellSize`.
* Это обеспечит идеальный внешний вид труб при уменьшении сетки до 10px.

#### [MODIFY] [ConfigurationService.cs](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/Services/ConfigurationService.cs)
* Изменить шаг сетки мнемосхемы `Mimic.CellSize` с `20` на `10`.
* Пересчитать (умножить на 2) координаты `Row`, `Col`, `SizeX`, `SizeY` и координаты точек `PipePoints` всех 12 виджетов мнемосхемы по умолчанию для соответствия новой сетке `10x10`.

#### [MODIFY] [MainViewModel.cs](file:///c:/Users/ess2/source/repos/AvaloniaApplication1/AvaloniaApplication1/ViewModels/MainViewModel.cs)
* В методе `LoadConfigAsync()` изменить автозамену размера ячейки мнемосхемы по умолчанию с `20` на `10` при значении 0 или 160.

---

## Verification Plan

### Automated Tests
- Собрать проект и убедиться в отсутствии ошибок компиляции:
  ```powershell
  dotnet build
  ```

### Manual Verification
1. Запустить приложение, открыть вкладку «Мнемосхема».
2. Кликнуть по регулирующему клапану `YV_S1` $\rightarrow$ должно открыться окно управления.
3. Кликнуть по нему еще раз $\rightarrow$ открытое окно должно закрыться (исчезнуть).
4. Открыть окно управления насосом, перетащить его, закрыть по крестику в углу. Кликнуть по насосу снова $\rightarrow$ окно должно успешно открыться.
5. Убедиться, что в режиме конструктора (Design Mode) элементы прилипают по мелкой сетке `10x10` пикселей, а трубопроводы и фланцы отображаются пропорционально и аккуратно.
