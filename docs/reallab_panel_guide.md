# Руководство по компиляции и развертыванию HMI на панели RealLab NLcon-LXD12-IP65 (Debian 9)

Данный документ описывает процесс сборки, оптимизации и развертывания проекта Avalonia HMI/SCADA на промышленной панели оператора **NLcon-LXD12-IP65** (производство ООО «НИЛ АП» / «RealLab», г. Таганрог) для проекта **«Парк хранения ММА (4 емкости)»**.

---

## 1. Аппаратная платформа и окружение панели

Согласно проектной спецификации (**21/ВС-108-111849 КА СО**, лист 10), панель оператора имеет следующие характеристики:

| Параметр | Значение | Примечание |
|---|---|---|
| **Модель** | NLcon-LXD12-IP65 | ООО «НИЛ АП» («RealLab»), г. Таганрог |
| **Процессор** | Broadcom BCM2837B0 | 4 ядра ARM Cortex-A53, тактовая частота 1.2 ГГц |
| **Архитектура** | ARMv8 / ARMv7 (32-bit `armhf` / 64-bit `aarch64`) | Совместимо с Raspberry Pi Compute Module 3 |
| **Экран** | 12.1 дюймов, емкостный тач-скрин | Защита передней панели IP65 |
| **Разрешение экрана** | **1024 x 768** (4:3 XGA) | Все экраны мнемосхемы спроектированы под эту сетку |
| **Операционная система** | RealLab! Raspbian Linux | Базовый дистрибутив: **Debian GNU/Linux 9 (Stretch)** |
| **Библиотека C (libc)** | GNU C Library (glibc) **2.24** | Ключевой нюанс при выборе рантайма .NET |
| **Сетевые интерфейсы** | 2x Ethernet 10/100, 2x RS-485 | Modbus TCP к ПЛК Wiren Board 8 (`10.10.10.234:502`) |

---

## 2. Архитектурный нюанс: glibc 2.24 в Debian 9 и .NET 8

### Проблема совместимости:
1. Официальный рантайм **.NET 8 (CoreCLR)** скомпилирован с требованием **glibc >= 2.27** (Debian 10+, Ubuntu 18.04+).
2. Заводской дистрибутив **Debian 9 Stretch** на панели RealLab содержит библиотеку **glibc 2.24**. Попытка прямого запуска бинарника .NET 8 приведет к ошибке:
   ```text
   ./AvaloniaApplication1: /lib/arm-linux-gnueabihf/libc.so.6: version `GLIBC_2.27' not found
   ```

### Пути решения для производства:

#### Вариант 1 (Рекомендуемый): Обновление ОС панели до Debian 11 (Bullseye) или Debian 12 (Bookworm)
Поскольку процессор **Broadcom BCM2837B0** аппаратно является стандартным Raspberry Pi CM3, на панель NLcon можно установить современный дистрибутив Raspberry Pi OS / Debian 11 (bullseye) с glibc 2.31 или Debian 12 (bookworm) с glibc 2.36.
* **Преимущество:** Полная нативная поддержка .NET 8 `linux-arm64` или `linux-arm`, максимальная производительность JIT-компилятора и графического рендерера Skia.

#### Вариант 2: Запуск с изолированным контейнером / sysroot glibc 2.28+
Если прошивку Debian 9 на панели нельзя обновлять из-за заводских гарантий, развертывание выполняется с изолированным комплектом библиотек libc в папке приложения:
```bash
LD_LIBRARY_PATH=/opt/glibc-2.28/lib ./AvaloniaApplication1
```

#### Вариант 3: Таргетинг .NET 6.0
Рантайм **.NET 6.0** поддерживает glibc 2.23+ и полностью совместим со стоковым Debian 9 Stretch. При необходимости проект компилируется под `net6.0` без изменения кода разметки и логики MVVM.

---

## 3. Команды компиляции и сборки под Linux ARM

Сборка выполняется на рабочей машине Windows/Linux с помощью .NET SDK.

### 3.1. Сборка для 32-битного Raspbian (`linux-arm` / armhf):
```powershell
# Self-contained автономный пакет со всеми зависимостями
dotnet publish AvaloniaApplication1/AvaloniaApplication1.csproj `
  -c Release `
  -r linux-arm `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:UseAppHost=true `
  -o ./publish/reallab-arm32
```

### 3.2. Сборка для 64-битного Debian (`linux-arm64` / aarch64):
```powershell
dotnet publish AvaloniaApplication1/AvaloniaApplication1.csproj `
  -c Release `
  -r linux-arm64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:UseAppHost=true `
  -o ./publish/reallab-arm64
```

---

## 4. Развертывание и автозапуск на панели RealLab

### 4.1. Копирование файлов на панель (по SSH / SCP):
```powershell
$panelIp = "10.10.10.235" # IP-адрес панели оператора
scp -r ./publish/reallab-arm32/* "root@${panelIp}:/opt/mma_hmi/"
ssh "root@${panelIp}" "chmod +x /opt/mma_hmi/AvaloniaApplication1"
```

### 4.2. Настройка systemd-сервиса киоска (`/etc/systemd/system/mma-hmi.service`):
```ini
[Unit]
Description=MMA Park HMI SCADA Operator Panel
After=network-online.target graphical.target
Wants=network-online.target

[Service]
Type=simple
User=root
WorkingDirectory=/opt/mma_hmi
Environment=DISPLAY=:0
Environment=XAUTHORITY=/root/.Xauthority
ExecStart=/opt/mma_hmi/AvaloniaApplication1 --kiosk
Restart=always
RestartSec=3

[Install]
WantedBy=graphical.target
```

Активация сервиса:
```bash
systemctl daemon-reload
systemctl enable mma-hmi.service
systemctl start mma-hmi.service
```

---

## 5. Связь с контроллером Wiren Board 8

Панель RealLab опрашивает контроллер Wiren Board 8 по протоколу **Modbus TCP**:
- **IP-адрес контроллера:** `10.10.10.234`
- **Порт:** `502`
- **Период опроса:** 200 мс
- **Порядок байт для Float32:** `CDAB` (Little-Endian с перестановкой 16-битных слов, стандарт ARM/C++).
- Конфигурационный файл проекта: `/opt/mma_hmi/config_mma_park.json` (или `config.json`).
