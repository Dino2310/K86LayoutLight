# K86LayoutLight

[Русский](#русский) | [English](#english)

## Русский

Утилита для Windows на C# / Windows Forms, которая переключает сохранённые слои подсветки клавиатуры K86 в зависимости от языка ввода активного окна: EN или RU. Цвета слоёв предварительно настраиваются в фирменном ПО клавиатуры. Интерфейс приложения — на русском языке.

### Возможности

- Автоматический выбор одного из двух сохранённых слоёв для EN и RU.
- Ручная проверка слоёв и настройка яркости от 1 до 4.
- Переключение после паузы во вводе: 100–2000 мс, по умолчанию 300 мс. Движения и кнопки мыши не задерживают переключение.
- Работа через USB или поддерживаемый приёмник 2,4 ГГц.
- Значок EN/RU в системном трее, автозапуск при входе в Windows и журнал диагностики.

Для других языков ввода приложение не отправляет команду смены слоя. Оно не меняет саму раскладку Windows.

### Что установить

| Сценарий | Зависимости |
| --- | --- |
| Сборка из исходников | Windows и [.NET 8 SDK для Windows](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). SDK включает Desktop Runtime. |
| Запуск обычной сборки | [.NET Desktop Runtime 8 для Windows](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), соответствующий архитектуре приложения. Одного обычного .NET Runtime недостаточно для Windows Forms. |
| Запуск автономной сборки | Отдельная установка .NET не требуется: среда выполнения включена в публикацию. |
| Настройка цветов | Фирменное ПО именно вашей модели клавиатуры для сохранения двух пользовательских слоёв. |

Проект использует `net8.0-windows` и системные библиотеки Windows (`hid.dll`, `setupapi.dll`, `user32.dll`, `kernel32.dll`). Сторонних NuGet-пакетов нет; отдельно скачивать эти DLL не нужно. Linux и macOS для запуска не поддерживаются.

Редактор необязателен: достаточно SDK и терминала. В Visual Studio установите рабочую нагрузку «Разработка классических приложений .NET» с поддержкой .NET 8 и откройте `K86LayoutLight.csproj`. В VS Code команды ниже выполняются во встроенном терминале. Git нужен только для клонирования; исходники также можно скачать ZIP-архивом.

### Совместимость клавиатуры

В коде предусмотрены устройства с VID `3151`, внутренними ID `1168`, `2730` или `4094` и соответствующим HID-интерфейсом. Приёмник 2,4 ГГц определяется по PID `4011`. Это ограничения реализации, а не гарантия совместимости со всеми ревизиями K86. Bluetooth не поддерживается.

### Сборка и запуск

В PowerShell из папки проекта:

```powershell
dotnet restore K86LayoutLight.csproj
dotnet build K86LayoutLight.csproj -c Release
dotnet run --project K86LayoutLight.csproj -c Release
```

Результат обычной сборки: `bin\Release\net8.0-windows\K86LayoutLight.exe`. При переносе такой сборки копируйте всю папку с результатом сборки.

### Автономная сборка для Windows x64

```powershell
dotnet publish K86LayoutLight.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

Перенесите всю папку `artifacts\publish\win-x64` на целевой компьютер и запустите `K86LayoutLight.exe`. .NET отдельно устанавливать не требуется. Команда предназначена для x64; для другой архитектуры нужен соответствующий идентификатор среды выполнения и проверка на целевом устройстве.

### Первый запуск

1. В фирменном ПО задайте и сохраните два пользовательских слоя подсветки с нужными цветами.
2. Полностью закройте фирменное ПО, включая его значок в трее.
3. Подключите K86 через USB либо приёмник 2,4 ГГц. Для беспроводного подключения включите соответствующий режим клавиатуры и разбудите её нажатием клавиши.
4. Запустите утилиту и проверьте оба варианта кнопками «Проверить вариант 1» и «Проверить вариант 2». Ручная проверка отключает автоматический режим.
5. Выберите варианты для EN и RU, яркость и длительность паузы. По умолчанию EN — слой 1, RU — слой 2, яркость — 4.
6. Включите автоматическое переключение и, при необходимости, запуск при входе в Windows.

Крестик окна скрывает приложение в трей. Двойной щелчок по значку открывает окно; пункт «Выход» завершает приложение. Параметр `--tray` запускает его со скрытым окном:

```powershell
.\bin\Release\net8.0-windows\K86LayoutLight.exe --tray
```

Автозапуск сохраняет путь к текущему EXE в `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, значение `K86LayoutLight`. Включайте его после размещения сборки в постоянной папке; при переносе выключите и снова включите эту настройку.

### Настройки и диагностика

- Настройки: `%LOCALAPPDATA%\K86LayoutLight\settings-v2.json`.
- Журнал: `%LOCALAPPDATA%\K86LayoutLight\diagnostics.log`, доступен кнопкой «Открыть журнал».
- Если клавиатура не найдена, проверьте режим подключения, закройте фирменное ПО и нажмите «Подключиться».
- Если обнаружено несколько K86, оставьте подключённой одну. При наличии одного проводного устройства код отдаёт ему приоритет.
- Если подсветка не меняется, проверьте сохранённые слои, автоматический режим и язык активного окна. Отпустите клавиши и дождитесь заданной паузы.

Лицензия: [MIT](LICENSE).

## English

A C# / Windows Forms utility for Windows that switches saved K86 keyboard lighting layers according to the active window's input language: EN or RU. Configure the layer colors beforehand using the keyboard vendor's software. The application UI is in Russian.

### Features

- Automatic selection of one of two saved lighting layers for EN and RU.
- Manual layer testing and brightness levels from 1 to 4.
- Switching after a keyboard input pause: 100–2000 ms, default 300 ms. Mouse movement and buttons do not delay switching.
- USB and supported 2.4 GHz receiver connections.
- EN/RU system tray icon, optional Windows sign-in startup, and diagnostic logging.

For other input languages, the application sends no layer-switch command. It does not change the Windows keyboard layout itself.

### Dependencies

| Scenario | Requirements |
| --- | --- |
| Build from source | Windows and the [.NET 8 SDK for Windows](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). The SDK includes the Desktop Runtime. |
| Run a framework-dependent build | [.NET Desktop Runtime 8 for Windows](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), matching the application architecture. The plain .NET Runtime alone is insufficient for Windows Forms. |
| Run a self-contained build | No separate .NET installation: the published output includes the runtime. |
| Configure lighting colors | Your keyboard model's vendor software to save two custom lighting layers. |

The project targets `net8.0-windows` and uses Windows system libraries (`hid.dll`, `setupapi.dll`, `user32.dll`, `kernel32.dll`). There are no third-party NuGet dependencies or additional DLL downloads. Running on Linux or macOS is not supported.

An IDE is optional; the SDK and a terminal are sufficient. For Visual Studio, install the “.NET desktop development” workload with .NET 8 support and open `K86LayoutLight.csproj`. In VS Code, use the integrated terminal for the commands below. Git is only needed for cloning; downloading the source as a ZIP also works.

### Keyboard compatibility

The implementation accepts VID `3151`, internal device IDs `1168`, `2730`, or `4094`, and a matching HID interface. PID `4011` identifies the 2.4 GHz receiver. These are implementation filters, not a compatibility guarantee for every K86 revision. Bluetooth is not supported.

### Build and run

Run in PowerShell from the project directory:

```powershell
dotnet restore K86LayoutLight.csproj
dotnet build K86LayoutLight.csproj -c Release
dotnet run --project K86LayoutLight.csproj -c Release
```

The framework-dependent executable is `bin\Release\net8.0-windows\K86LayoutLight.exe`. Copy the entire build output directory when moving this build to another computer.

### Self-contained Windows x64 build

```powershell
dotnet publish K86LayoutLight.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

Copy the entire `artifacts\publish\win-x64` directory to the target computer and run `K86LayoutLight.exe`. No separate .NET installation is needed. This command targets x64; other architectures require the appropriate runtime identifier and verification on the target device.

### First launch

1. Use the vendor software to configure and save two custom lighting layers with your preferred colors.
2. Completely exit the vendor software, including its tray application.
3. Connect the K86 using USB or its 2.4 GHz receiver. For wireless use, select the corresponding keyboard mode and press a key to wake it.
4. Launch the utility and test both layers using “Проверить вариант 1” and “Проверить вариант 2”. Manual testing disables automatic mode.
5. Choose the EN and RU layers, brightness, and input pause. Defaults: EN layer 1, RU layer 2, brightness 4.
6. Enable automatic switching and, optionally, startup at Windows sign-in.

Closing the window hides it in the system tray. Double-click the tray icon to reopen it; choose “Выход” to quit. Use `--tray` to start with the window hidden:

```powershell
.\bin\Release\net8.0-windows\K86LayoutLight.exe --tray
```

Startup stores the current EXE path in the `K86LayoutLight` value under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. Enable it after placing the build in its permanent directory; toggle the setting off and on again if you move it.

### Settings and troubleshooting

- Settings: `%LOCALAPPDATA%\K86LayoutLight\settings-v2.json`.
- Log: `%LOCALAPPDATA%\K86LayoutLight\diagnostics.log`; the “Открыть журнал” button opens it.
- If no keyboard is found, check its connection mode, exit the vendor software, and click “Подключиться” to reconnect.
- If multiple K86 devices are detected, leave one connected. A single wired device takes priority in the implementation.
- If lighting does not change, check the saved layers, automatic mode, and active window's input language. Release the keys and wait for the configured pause.

License: [MIT](LICENSE).
