# Журнал Прогресса Рефакторинга (Progress Tracking)

## Фаза 1: Data Core Decomposition — **[ЗАВЕРШЕНО]**
- Устранен монолитный класс `DataCoreService`.
- Внедрен интерфейс `IProtocolDriver`.
- Вынесены классы `ModbusProtocolDriver`, `MqttProtocolDriver` и `MockProtocolDriver`.
- `DataCoreService` переведен в режим Фасада.

## Фаза 2: Reactive Data Routing — **[ЗАВЕРШЕНО]**
- Удалены C#-события `TagValueChanged`.
- Вся маршрутизация переведена на Rx.NET `IObservable<TagData>`.
- Реализована безопасная подписка `ObserveOn(RxApp.MainThreadScheduler)` и `Sample()` во всех 10 ViewModels.
- Очистка памяти управляется через `CompositeDisposable`.

## Фаза 3: Polymorphism & Factory Pattern — **[В ОЖИДАНИИ]**
- (Текущая задача) Внедрение `WidgetFactory` через DI-контейнер.
- Разработка кастомного `WidgetJsonConverter` для полиморфной десериализации конфигураций.
- Удаление `switch/case` фабричных методов из UI слоя.

## Фаза 4: Clean MVVM & Dashboard Refactoring — **[НЕ НАЧАТО]**
- Декомпозиция `DashboardPanel.cs` (>1600 строк).
- Удаление логики из Code-Behind `MainWindow.axaml.cs`.
- Включение строгих `CompiledBindings`.
