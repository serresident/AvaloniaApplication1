# Industrial SCADA Controls Library Extraction & Hierarchical Project Reorganization

* **Author:** Senior C# / .NET / Avalonia UI Architect
* **Status:** Approved
* **Date:** 2026-11-14

--------------------------------------------------------------------------------

## Overview

This technical design document specifies the architectural extraction and physical re-structuring of `AvaloniaApplication1` (an industrial HMI/SCADA desktop application built with .NET 8, Avalonia UI 12.0.4, and CommunityToolkit.Mvvm).

The refactoring achieves two primary milestones:
1. **Extraction of Pure Visual SCADA Controls**: Isolates equipment rendering (`ValveControl`, `PipeControl`, `ReactorControl`, `HeatExchangerControl`, `LevelSensorControl`, `TrendLineControl`, `ColorPickerBox`, `FormatEditorBox`) into an independent class library `AvaloniaApplication1.Controls.csproj` with unified XML namespace registration (`[XmlnsDefinition]`).
2. **Hierarchical Project Restructuring**: Decomposes flat `Views/` (40+ files) and `ViewModels/` (37 files) into clean domain-oriented subdirectories (`Equipment/`, `Canvas/`, `Windows/`, `Popups/`, `Widgets/`, `Core/`) with strict C# namespace parity and verified XAML compiled bindings.

--------------------------------------------------------------------------------

## The Problem

1. **Monolithic UI Coupling & Directory Pollution**: Over 40 view classes and 37 viewmodel classes reside flat in `Views/` and `ViewModels/`. Hardware rendering controls (e.g., `ReactorControl` with ~30 FPS animated impeller) are mixed directly alongside application windows (`TagManagerWindow`, `SettingsWindow`) and layout containers (`DashboardPanel`).
2. **Circular Dependency Hazard**: The interactive editing canvas `DashboardPanel.cs` and piping control `PipeControl.cs` currently retain direct references to specific ViewModels (`WidgetViewModelBase`, `PipeWidgetViewModel`, `DashboardViewModel`). Extracting all visual components blindly into a class library would introduce a cyclic project reference (`Controls <--> App`).
3. **Ergonomic Namespace Burden**: Using custom controls currently requires multiple distinct `clr-namespace` declarations in every AXAML file instead of a single, canonical industrial SCADA schema URI.
4. **Maintenance Fragility**: Any modification to a widget or dialog risks breaking compiled bindings (`x:CompileBindings="True"`, `x:DataType`) without strict folder-to-namespace parity.

--------------------------------------------------------------------------------

## Goals

* **G-1: Independent Controls Library**: Create `AvaloniaApplication1.Controls.csproj` targeting .NET 8 and Avalonia 12.0.4. All pure equipment, piping, charting, and editor controls reside here with zero references to application-specific ViewModels.
* **G-2: Circular Dependency Elimination**: Keep `DashboardPanel` and canvas overlines in `AvaloniaApplication1/Views/Canvas/` while pure controls move to `Controls.csproj`, decoupling `PipeControl` from `PipeWidgetViewModel` via pure Avalonia attached/styled properties.
* **G-3: Canonical Namespace Parity**: Every moved C# and AXAML file adopts strict namespace alignment matching its physical folder (`AvaloniaApplication1.Controls.Equipment`, `AvaloniaApplication1.Views.Windows`, `AvaloniaApplication1.ViewModels.Widgets`).
* **G-4: Unified XAML Namespace**: Declare `[assembly: XmlnsDefinition("https://github.com/avaloniaapplication1/controls", ...)]` so consumer XAML uses a single prefix (`xmlns:scada="https://github.com/avaloniaapplication1/controls"`).
* **G-5: Zero Build & Test Regressions**: Preserve 100% test passing (20/20 test suites in `AvaloniaApplication1.UIValidation`) and maintain 0 compiler warnings and 0 errors on Debug/Release builds.

--------------------------------------------------------------------------------

## Non-Goals

* **NG-1: Business Logic & Driver Alterations**: No changes to Modbus/MQTT communication drivers, DataCoreService, or TagManagerService internal mechanics.
* **NG-2: Standalone NuGet Packaging**: The controls library remains an in-solution project reference (`.sln`); public NuGet packaging, versioning pipelines, and publishing are out of scope.
* **NG-3: UI Redesign or Theme Rework**: Visual styling, palette constants, and geometry metrics of existing industrial apparatuses remain byte-for-byte identical.
* **NG-4: Moving Canvas to Controls**: As resolved in the concept brief, `DashboardPanel` remains within the host application (`Views/Canvas/`) to avoid premature abstraction of the editor drag-and-drop system.

--------------------------------------------------------------------------------

## Open Questions

* None. The architectural boundary between pure rendering controls (in `Controls.csproj`) and interactive canvas orchestration (in `Views/Canvas/`) was explicitly affirmed during the concept brief review.

--------------------------------------------------------------------------------

## Critical User Journeys (CUJs)

### CUJ 1: Developing a New Equipment Widget in Clean Architecture
* **Given**: An engineer wants to add a new industrial apparatus (e.g., `CompressorControl`).
* **When**: The engineer creates `CompressorControl.cs` inside `AvaloniaApplication1.Controls/Equipment/`.
* **Then**: The control compiles independently without referencing any application ViewModels, is immediately available in XAML via `xmlns:scada="https://github.com/avaloniaapplication1/controls"`, and maintains zero-allocation rendering rules.

### CUJ 2: Navigating and Modifying Application Dialogs
* **Given**: A developer needs to modify tag management dialogs (`TagManagerWindow`, `TagEditWindow`, `TagForceWindow`).
* **When**: The developer opens the solution explorer.
* **Then**: All tag-related windows are grouped in `Views/Windows/Tags/` and their respective ViewModels in `ViewModels/Windows/Tags/`, eliminating visual noise and file hunting across 40+ unrelated files.

### CUJ 3: Automated UI Validation & Headless Regression Testing
* **Given**: Structural migration and namespace updating is complete.
* **When**: `dotnet run --project AvaloniaApplication1.UIValidation/AvaloniaApplication1.UIValidation.csproj` executes.
* **Then**: All 20 automated tests pass without reflection or type resolution errors, and headless UI screenshots match pixel-perfect layouts.

--------------------------------------------------------------------------------

## Technical Plan (Architecture)

### 1. Project Topology & Dependency Inversion

The solution topology strictly enforces Clean Architecture boundaries. All visual primitives and industrial rendering components reside in a dedicated leaf project, eliminating cyclical dependencies.

```mermaid
flowchart TD
    subgraph Solution ["AvaloniaApplication1.sln"]
        UIVal ["AvaloniaApplication1.UIValidation\n(Headless Test Runner & Verifier)"]
        App ["AvaloniaApplication1\n(Application Host, Shell & Canvas)"]
        Ctrl ["AvaloniaApplication1.Controls\n(Pure Visual SCADA Controls Library)"]
        
        UIVal --> App
        UIVal --> Ctrl
        App --> Ctrl
    end

    subgraph Frameworks ["External Dependencies"]
        Ava ["Avalonia 12.0.4 / .NET 8 SDK"]
        CTM ["CommunityToolkit.Mvvm 8.2.2"]
    end

    Ctrl --> Ava
    App --> Ava
    App --> CTM
```

### 2. Detailed Structure: `AvaloniaApplication1.Controls.csproj`

This new library targets `net8.0`, references `Avalonia 12.0.4`, and contains no references to the host application or its ViewModels:

```
AvaloniaApplication1.Controls/
├── AvaloniaApplication1.Controls.csproj
├── Properties/
│   └── AssemblyInfo.cs              <-- Unified [XmlnsDefinition] attributes
├── Equipment/                       <-- Namespace: AvaloniaApplication1.Controls.Equipment
│   ├── ValveControl.cs              (CutOff/Regulating, Solenoid/Diaphragm/Manual actuators)
│   ├── ReactorControl.cs            (Jacketed vessel with animated impeller)
│   ├── HeatExchangerControl.cs      (Shell & tube with counter-flow ports)
│   └── LevelSensorControl.cs        (ISA 5.1 radar/waveguide level sensor)
├── Piping/                          <-- Namespace: AvaloniaApplication1.Controls.Piping
│   └── PipeControl.cs               (3D orthogonal segments, zero VM coupling)
├── Charting/                        <-- Namespace: AvaloniaApplication1.Controls.Charting
│   └── TrendLineControl.cs          (Zero-allocation min-max decimation curve)
└── Common/                          <-- Namespace: AvaloniaApplication1.Controls.Common
    ├── ColorPickerBox.axaml/.cs     (Hex color selector UserControl)
    └── FormatEditorBox.axaml/.cs    (String formatting mask builder)
```

#### Decoupling `PipeControl` from ViewModels
In the original implementation, `PipeControl.cs` inspected `DataContext as PipeWidgetViewModel` to read normalized geometry. Under the new architecture, `PipeControl` exposes a pure Avalonia `StyledProperty<string> PointsProperty` and two-way binds to the host ViewModel via XAML without any runtime casting.

### 3. Detailed Structure: Host Application Reorganization

The main project decomposes into modular namespaces mirroring physical directories:

```
AvaloniaApplication1/
├── Views/
│   ├── Canvas/                      <-- Namespace: AvaloniaApplication1.Views.Canvas
│   │   ├── DashboardPanel.cs        (Layout arrange/measure & drag engine)
│   │   ├── Helpers/                 <-- Namespace: AvaloniaApplication1.Views.Canvas.Helpers
│   │   │   ├── VisualPortHelper.cs
│   │   │   └── GridMathHelper.cs
│   │   └── Overlays/                <-- Namespace: AvaloniaApplication1.Views.Canvas.Overlays
│   │       ├── GridOverlay.cs
│   │       └── SelectionOverlay.cs
│   ├── Panels/                      <-- Namespace: AvaloniaApplication1.Views.Panels
│   │   └── DashboardView.axaml/.cs  (Main mimic workspace & DataTemplates)
│   ├── Popups/                      <-- Namespace: AvaloniaApplication1.Views.Popups
│   │   ├── ValveControlPopupView.axaml/.cs
│   │   ├── CalculatorView.axaml/.cs
│   │   └── NumpadView.axaml/.cs
│   └── Windows/                     <-- Namespace: AvaloniaApplication1.Views.Windows
│       ├── Common/                  <-- Namespace: AvaloniaApplication1.Views.Windows.Common
│       │   ├── MainWindow.axaml/.cs
│       │   ├── SettingsWindow.axaml/.cs
│       │   ├── DashboardPropertiesWindow.axaml/.cs
│       │   ├── WidgetEditorWindow.axaml/.cs
│       │   ├── ConfirmPromptWindow.axaml/.cs
│       │   └── PasswordPromptWindow.axaml/.cs
│       ├── Connections/             <-- Namespace: AvaloniaApplication1.Views.Windows.Connections
│       │   ├── ConnectionManagerWindow.axaml/.cs
│       │   └── ConnectionEditorWindow.axaml/.cs
│       └── Tags/                    <-- Namespace: AvaloniaApplication1.Views.Windows.Tags
│           ├── TagManagerWindow.axaml/.cs
│           ├── TagEditWindow.axaml/.cs
│           ├── TagForceWindow.axaml/.cs
│           ├── TagPickerWindow.axaml/.cs
│           └── TagImportExportWindow.axaml/.cs
│
└── ViewModels/
    ├── Core/                        <-- Namespace: AvaloniaApplication1.ViewModels.Core
    │   ├── ViewModelBase.cs
    │   ├── WidgetViewModelBase.cs
    │   ├── ChildWindowViewModel.cs
    │   └── IWidgetFactory.cs
    ├── Panels/                      <-- Namespace: AvaloniaApplication1.ViewModels.Panels
    │   └── DashboardViewModel.cs
    ├── Popups/                      <-- Namespace: AvaloniaApplication1.ViewModels.Popups
    │   ├── ValveControlPopupViewModel.cs
    │   ├── CalculatorViewModel.cs
    │   └── NumpadViewModel.cs
    ├── Windows/                     <-- Namespace: AvaloniaApplication1.ViewModels.Windows
    │   ├── Common/                  <-- Namespace: AvaloniaApplication1.ViewModels.Windows.Common
    │   │   ├── MainViewModel.cs
    │   │   ├── SettingsViewModel.cs
    │   │   ├── DashboardPropertiesViewModel.cs
    │   │   ├── WidgetEditorViewModel.cs
    │   │   ├── ConfirmPromptViewModel.cs
    │   │   └── PasswordPromptViewModel.cs
    │   ├── Connections/             <-- Namespace: AvaloniaApplication1.ViewModels.Windows.Connections
    │   │   ├── ConnectionManagerViewModel.cs
    │   │   └── ConnectionEditorViewModel.cs
    │   └── Tags/                    <-- Namespace: AvaloniaApplication1.ViewModels.Windows.Tags
    │       ├── TagManagerViewModel.cs
    │       ├── TagEditViewModel.cs
    │       ├── TagForceViewModel.cs
    │       ├── TagPickerViewModel.cs
    │       ├── TagImportExportViewModel.cs
    │       └── TagItemViewModel.cs
    └── Widgets/                     <-- Namespace: AvaloniaApplication1.ViewModels.Widgets
        ├── WidgetFactory.cs
        ├── ValueDisplayViewModel.cs
        ├── PilotLightViewModel.cs
        ├── CommandButtonViewModel.cs
        ├── SliderViewModel.cs
        ├── SetValueViewModel.cs
        ├── RealTimeTrendViewModel.cs
        ├── PipeWidgetViewModel.cs
        ├── ValveWidgetViewModel.cs
        ├── TankWidgetViewModel.cs
        ├── PumpWidgetViewModel.cs
        ├── HeatExchangerWidgetViewModel.cs
        ├── ReactorWidgetViewModel.cs
        ├── LevelSensorWidgetViewModel.cs
        └── ContainerButtonViewModel.cs
```

### 4. Unified XMLNS Registration & Ergonomics

To prevent XAML files from requiring 6 separate `xmlns:clr-namespace` prefixes, `AvaloniaApplication1.Controls/Properties/AssemblyInfo.cs` declares:

```csharp
using Avalonia.Metadata;

[assembly: XmlnsDefinition("https://github.com/avaloniaapplication1/controls", "AvaloniaApplication1.Controls.Equipment")]
[assembly: XmlnsDefinition("https://github.com/avaloniaapplication1/controls", "AvaloniaApplication1.Controls.Piping")]
[assembly: XmlnsDefinition("https://github.com/avaloniaapplication1/controls", "AvaloniaApplication1.Controls.Charting")]
[assembly: XmlnsDefinition("https://github.com/avaloniaapplication1/controls", "AvaloniaApplication1.Controls.Common")]
```

In `DashboardView.axaml` and all dialog windows, the consumer uses a single unified namespace:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:scada="https://github.com/avaloniaapplication1/controls"
             xmlns:vmWidgets="clr-namespace:AvaloniaApplication1.ViewModels.Widgets"
             xmlns:vmCore="clr-namespace:AvaloniaApplication1.ViewModels.Core"
             x:CompileBindings="True">

    <DataTemplate DataType="{x:Type vmWidgets:ValveWidgetViewModel}">
        <scada:ValveControl ValveType="{Binding ValveType}"
                            ActuatorType="{Binding ActuatorType}"
                            IsVertical="{Binding IsVertical}" />
    </DataTemplate>
</UserControl>
```

### 5. Dependency Injection Registration Plan

In `App.axaml.cs`, service registration adapts to the new namespaces with zero behavioral change:
```csharp
// ViewModels
services.AddSingleton<IWidgetFactory, WidgetFactory>(); // in AvaloniaApplication1.ViewModels.Widgets
services.AddSingleton<MainViewModel>();                 // in AvaloniaApplication1.ViewModels.Windows.Common
services.AddSingleton<IDialogService, DialogService>();
services.AddSingleton<ITagManagerService, TagManagerService>();
```

--------------------------------------------------------------------------------

## Alternatives Considered

### Alternative A: Extracting DashboardPanel directly into AvaloniaApplication1.Controls
* **Pros**: All canvas layout and coordinate math reside in the reusable controls project.
* **Cons**: Introduces an immediate cyclic dependency (`Controls <--> App`) because `DashboardPanel` currently references `WidgetViewModelBase` (for dragging selection), `PipeWidgetViewModel` (for endpoint normalization), and `DashboardViewModel` (for context menu commands and clipboard operations).
* **Reason for Rejection**: Decoupling `DashboardPanel` from all application ViewModel interaction models simultaneously with the physical file migration introduces excessive regression risk. Keeping `DashboardPanel` in `Views/Canvas/` within the host application cleanly isolates pure rendering controls without risking editor breakage.

### Alternative B: Physical file reorganization without changing C# namespaces
* **Pros**: Zero churn on XAML `xmlns` prefixes and compiled bindings (`x:DataType`).
* **Cons**: Violates canonical .NET engineering standards ("one folder = one namespace"). Creates confusion in IDEs and code discovery when files in `Views/Windows/Tags/` declare `namespace AvaloniaApplication1.Views`.
* **Reason for Rejection**: Explicitly rejected by project owner in favor of strict namespace parity to establish a maintainable enterprise foundation.

### Alternative C: Publishing AvaloniaApplication1.Controls as an external NuGet package
* **Pros**: Independent versioning and distribution across completely separate repositories.
* **Cons**: High operational overhead: requires private NuGet feed configuration, API key management, version bumping, and debugging with SourceLink/PDBs.
* **Reason for Rejection**: Unnecessary operational burden for the current stage. A solution-level project reference (`.sln`) provides full boundary enforcement with zero release friction.

--------------------------------------------------------------------------------

## Non-Functional Requirements

### Performance & Zero-Allocation Invariants
* **Render Loop Invariance**: All custom controls (`ValveControl`, `PipeControl`, `TrendLineControl`, `ReactorControl`, `HeatExchangerControl`, `LevelSensorControl`) must retain static cached brushes/pens and `AffectsRender<T>` calls. Moving files to `Controls.csproj` must not introduce heap allocations inside `Render(DrawingContext)`.
* **Startup & Compile-Time Performance**: Moving pure controls to a separate project enables incremental compilation. Modifying a dialog view will not trigger recompilation of the vector graphics library.

### Reliability & Type Safety
* **Compiled Bindings (AVLN2000 Prevention)**: All AXAML files retain `x:CompileBindings="True"`. Any missing or mismatched namespace mapping will be caught at build time by XAMLIL, eliminating runtime reflection binding failures.

--------------------------------------------------------------------------------

## Detailed Implementation Plan

### Phased Migration Runbook

The implementation executes across 6 sequential phases, verifying a clean build and zero compiler warnings at each intermediate stage.

#### Phase 1: Create `AvaloniaApplication1.Controls` Project & Solution Plumbing
* **Files touched**:
  * `AvaloniaApplication1.Controls/AvaloniaApplication1.Controls.csproj` (Create)
  * `AvaloniaApplication1.Controls/Properties/AssemblyInfo.cs` (Create)
  * `AvaloniaApplication1.sln` (Modify: Add new project to solution)
  * `AvaloniaApplication1/AvaloniaApplication1.csproj` (Modify: Add project reference to `Controls`)
  * `AvaloniaApplication1.UIValidation/AvaloniaApplication1.UIValidation.csproj` (Modify: Add project reference to `Controls`)
* **Rationale**: Establishes the new leaf compilation target and project references before physical file moves.

#### Phase 2: Migrate Pure Visual Controls to `AvaloniaApplication1.Controls`
* **Files touched**:
  * `AvaloniaApplication1/Views/ValveControl.cs` -> `AvaloniaApplication1.Controls/Equipment/ValveControl.cs`
  * `AvaloniaApplication1/Views/ReactorControl.cs` -> `AvaloniaApplication1.Controls/Equipment/ReactorControl.cs`
  * `AvaloniaApplication1/Views/HeatExchangerControl.cs` -> `AvaloniaApplication1.Controls/Equipment/HeatExchangerControl.cs`
  * `AvaloniaApplication1/Views/LevelSensorControl.cs` -> `AvaloniaApplication1.Controls/Equipment/LevelSensorControl.cs`
  * `AvaloniaApplication1/Views/PipeControl.cs` -> `AvaloniaApplication1.Controls/Piping/PipeControl.cs` (Decouple from `PipeWidgetViewModel` cast)
  * `AvaloniaApplication1/Views/TrendLineControl.cs` -> `AvaloniaApplication1.Controls/Charting/TrendLineControl.cs`
  * `AvaloniaApplication1/Views/Controls/ColorPickerBox.axaml[.cs]` -> `AvaloniaApplication1.Controls/Common/ColorPickerBox.axaml[.cs]`
  * `AvaloniaApplication1/Views/Controls/FormatEditorBox.axaml[.cs]` -> `AvaloniaApplication1.Controls/Common/FormatEditorBox.axaml[.cs]`
* **Rationale**: Moves all hardware equipment rendering and zero-allocation graphics out of the main application. Namespace becomes `AvaloniaApplication1.Controls.<SubCategory>`.

#### Phase 3: Reorganize `ViewModels/` Hierarchy
* **Files touched**:
  * `ViewModels/Core/`: `ViewModelBase.cs`, `WidgetViewModelBase.cs`, `ChildWindowViewModel.cs`, `IWidgetFactory.cs`
  * `ViewModels/Widgets/`: `WidgetFactory.cs`, `ValveWidgetViewModel.cs`, `PumpWidgetViewModel.cs`, `PipeWidgetViewModel.cs`, `ReactorWidgetViewModel.cs`, `HeatExchangerWidgetViewModel.cs`, `LevelSensorWidgetViewModel.cs`, `TankWidgetViewModel.cs`, `ValueDisplayViewModel.cs`, `CommandButtonViewModel.cs`, `SetValueViewModel.cs`, `RealTimeTrendViewModel.cs`, `ContainerButtonViewModel.cs`, `PilotLightViewModel.cs`, `SliderViewModel.cs`
  * `ViewModels/Windows/Common/`: `MainViewModel.cs`, `SettingsViewModel.cs`, `DashboardPropertiesViewModel.cs`, `WidgetEditorViewModel.cs`, `ConfirmPromptViewModel.cs`, `PasswordPromptViewModel.cs`
  * `ViewModels/Windows/Connections/`: `ConnectionManagerViewModel.cs`, `ConnectionEditorViewModel.cs`
  * `ViewModels/Windows/Tags/`: `TagManagerViewModel.cs`, `TagEditViewModel.cs`, `TagForceViewModel.cs`, `TagPickerViewModel.cs`, `TagImportExportViewModel.cs`, `TagItemViewModel.cs`
  * `ViewModels/Popups/`: `ValveControlPopupViewModel.cs`, `CalculatorViewModel.cs`, `NumpadViewModel.cs`
  * `ViewModels/Panels/`: `DashboardViewModel.cs`
* **Rationale**: Grouping 37 flat ViewModels by functional domain with strict C# namespace updates.

#### Phase 4: Reorganize `Views/` Hierarchy
* **Files touched**:
  * `Views/Canvas/`: `DashboardPanel.cs`, `Helpers/VisualPortHelper.cs`, `Helpers/GridMathHelper.cs`, `Overlays/GridOverlay.cs`, `Overlays/SelectionOverlay.cs`
  * `Views/Panels/`: `DashboardView.axaml[.cs]`
  * `Views/Popups/`: `ValveControlPopupView.axaml[.cs]`, `CalculatorView.axaml[.cs]`, `NumpadView.axaml[.cs]`
  * `Views/Windows/Common/`: `MainWindow.axaml[.cs]`, `SettingsWindow.axaml[.cs]`, `DashboardPropertiesWindow.axaml[.cs]`, `WidgetEditorWindow.axaml[.cs]`, `ConfirmPromptWindow.axaml[.cs]`, `PasswordPromptWindow.axaml[.cs]`
  * `Views/Windows/Connections/`: `ConnectionManagerWindow.axaml[.cs]`, `ConnectionEditorWindow.axaml[.cs]`
  * `Views/Windows/Tags/`: `TagManagerWindow.axaml[.cs]`, `TagEditWindow.axaml[.cs]`, `TagForceWindow.axaml[.cs]`, `TagPickerWindow.axaml[.cs]`, `TagImportExportWindow.axaml[.cs]`
* **Rationale**: Groups windows, canvas, popups, and panels into domain subfolders.

#### Phase 5: Synchronize XAML Namespaces, DI & Dialog Mappings
* **Files touched**:
  * `AvaloniaApplication1/App.axaml.cs`: Update DI registrations (`using AvaloniaApplication1.ViewModels.Widgets`, `using AvaloniaApplication1.ViewModels.Windows.Common`, etc.)
  * `AvaloniaApplication1/Services/DialogService.cs`: Update window instantiations to new `Views.Windows.*` paths.
  * `AvaloniaApplication1/Views/Panels/DashboardView.axaml`:
    * Add `xmlns:scada="https://github.com/avaloniaapplication1/controls"`
    * Update `xmlns:vm` -> `xmlns:vmWidgets="clr-namespace:AvaloniaApplication1.ViewModels.Widgets"`, `xmlns:vmCore="clr-namespace:AvaloniaApplication1.ViewModels.Core"`
    * Update all 15 `<DataTemplate DataType="{x:Type vmWidgets:...}">`
    * Replace `<v:ValveControl>` with `<scada:ValveControl>`, `<v:ReactorControl>` with `<scada:ReactorControl>`, etc.
  * All Window AXAML files: Update `x:Class` and `x:DataType` attributes.
* **Rationale**: Guarantees zero XAMLIL compilation errors and preserves compiled binding type resolution.

#### Phase 6: Update UIValidation Test Runner & Automated Verification
* **Files touched**:
  * `AvaloniaApplication1.UIValidation/Program.cs`: Update namespaces in test harness.
  * Run: `dotnet build AvaloniaApplication1.sln -c Debug --nologo`
  * Run: `dotnet run --project AvaloniaApplication1.UIValidation/AvaloniaApplication1.UIValidation.csproj`
  * Run: `powershell -File scripts/render_ui.ps1 MainWindow`
* **Rationale**: Verifies 0 warnings, 0 errors, 20/20 passing tests, and identical visual layout.

--------------------------------------------------------------------------------

## Verification Plan

* **Compilation**: `dotnet build AvaloniaApplication1.sln -c Debug --nologo` (Exit code 0, 0 warnings, 0 errors).
* **Automated Unit & Integration Tests**: `dotnet run --project AvaloniaApplication1.UIValidation/AvaloniaApplication1.UIValidation.csproj` (All 20 test suites pass: 20/20 PASS, 100%).
* **Visual Headless Regression**: `powershell -ExecutionPolicy Bypass -File scripts/render_ui.ps1 MainWindow` generating `artifacts/ui_preview.png` and verifying node bounds in `artifacts/ui_tree.json`.
