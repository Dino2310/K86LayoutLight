using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Microsoft.Win32;
using System.Text.Json;

namespace K86LayoutLight;

internal static class Program
{
    [STAThread] static void Main()
    {
        using var mutex = new Mutex(true, "Local\\K86LayoutLight", out bool first);
        if (!first) { MessageBox.Show("Утилита уже запущена. Открой её через значок в трее."); return; }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
internal sealed class Settings
{
    public bool Auto { get; set; }
    public int EnglishLayer { get; set; } = 1;
    public int RussianLayer { get; set; } = 2;
    public int Brightness { get; set; } = 4;
    public int QuietMilliseconds { get; set; } = 300;
}
internal sealed class MainForm : Form
{
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "K86LayoutLight");
    static readonly string Config = Path.Combine(Dir, "settings-v2.json");
    Settings settings = new();
    readonly Label status = new() { AutoSize = true, MaximumSize = new Size(600, 0) };
    readonly CheckBox auto = new() { Text = "Автоматически: EN → слой 1, RU → слой 2", AutoSize = true };
    readonly CheckBox startup = new() { Text = "Запускать при входе в Windows", AutoSize = true };
    readonly NotifyIcon tray = new() { Text = "K86 Light · пауза", Visible = true };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    Keyboard? keyboard;
    bool busy, quitting;
    int pendingManual, generation;
    readonly AppliedState applied = new();
    DateTime retryAfter;
    bool deviceChanged;
    readonly Icon baseIcon = LoadIcon("app");
    readonly Icon enIcon = LoadIcon("en");
    readonly Icon ruIcon = LoadIcon("ru");
    readonly NumericUpDown en = new() { Minimum = 1, Maximum = 2, Value = 1, Width = 55 };
    readonly NumericUpDown ru = new() { Minimum = 1, Maximum = 2, Value = 2, Width = 55 };
    readonly NumericUpDown brightness = new() { Minimum = 1, Maximum = 4, Value = 4, Width = 55 };
    readonly NumericUpDown quiet = new() { Minimum = 100, Maximum = 2000, Increment = 100, Value = 300, Width = 80 };
    public MainForm()
    {
        Icon = baseIcon; tray.Icon = baseIcon;
        Directory.CreateDirectory(Dir);
        try 
        { 
            if (File.Exists(Config)) 
                settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Config)) ?? new(); 
        } 
        catch
        { }

        Text = "K86 — подсветка по раскладке v6.1"; ClientSize = new Size(700, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        var panel = new FlowLayoutPanel {
                                            Dock = DockStyle.Fill,
                                            FlowDirection = FlowDirection.TopDown,
                                            WrapContents = false,
                                            Padding = new Padding(18),
                                            AutoScroll = true 
                                        };
        Controls.Add(panel);

        panel.Controls.Add(new Label { 
                                        Text = "Сначала закрой фирменную программу. Проверь оба варианта подсветки.",
                                        AutoSize = true
                                    }
                            );
        var buttons = new FlowLayoutPanel { Width = 610, Height = 45 };

        foreach (int layer in new[] { 1, 2 })
        { 
            int n = layer;
            var b = new Button { Text = "Проверить вариант " + n, Width = 175, Height = 32 };
            b.Click += (_, _) => 
                                {
                                    auto.Checked = false; pendingManual = n; 
                                    generation++; Report("Слой " + n + " ожидает паузы во вводе."); 
                                };
            buttons.Controls.Add(b); 
        }
        var reconnect = new Button 
                                {
                                    Text = "Подключиться",
                                    Width = 150,
                                    Height = 32 
                                };
        reconnect.Click += async (_, _) => {
                                                if (busy) 
                                                    return;
                                                keyboard?.Dispose();
                                                keyboard = null;
                                                applied.Invalidate();
                                                retryAfter = DateTime.MinValue;
                                                await Connect(); 
                                            };

        buttons.Controls.Add(reconnect); 
        panel.Controls.Add(buttons);
        var values = new FlowLayoutPanel 
                                        {
                                            Width = 610,
                                            Height = 40
                                        };
        values.Controls.Add(new Label { Text = "Вариант EN", AutoSize = true });
        values.Controls.Add(en);
        values.Controls.Add(new Label { Text = "Вариант RU", AutoSize = true }); 
        values.Controls.Add(ru);
        values.Controls.Add(new Label { Text = "Яркость", AutoSize = true });
        values.Controls.Add(brightness);
        en.Value = Math.Clamp(settings.EnglishLayer, 1, 2);
        ru.Value = Math.Clamp(settings.RussianLayer, 1, 2);
        brightness.Value = Math.Clamp(settings.Brightness, 1, 4);
        panel.Controls.Add(values);
        quiet.Value = Math.Clamp(settings.QuietMilliseconds, 100, 2000);
        var pause = new FlowLayoutPanel { Width = 650, Height = 40 };
        pause.Controls.Add(new Label { AutoSize = true, Text = "Пауза после ввода, мс" });
        pause.Controls.Add(quiet);
        panel.Controls.Add(pause);
        panel.Controls.Add(new Label { AutoSize = true, Text = "Пауза только после клавиатуры. Мышь не влияет на смену цвета." });
        quiet.ValueChanged += (_, _) => { generation++; Save(); };
        panel.Controls.Add(new Label { AutoSize = true, Text = "Используются сохранённые слои клавиатуры. Цвета задаются в фирменном ПО." });
        panel.Controls.Add(auto);
        panel.Controls.Add(startup);
        auto.Text = "Автоматически переключать подсветку по текущему языку ввода";
        auto.Checked = settings.Auto;
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) 
        { 
            startup.Checked = key?.GetValue("K86LayoutLight") != null; 
        }
        auto.CheckedChanged += (_, _) => 
                                        { 
                                            generation++;
                                            pendingManual = 0;
                                            applied.Invalidate();
                                            UpdateTray(0); 
                                            Save(); 
                                        };

        foreach (var num in new[] { en, ru, brightness })
        {
            num.ValueChanged += (_, _) => { generation++; applied.Invalidate(); Save(); };
        }

        startup.CheckedChanged += (_, _) => {
                                                try {
                                                    using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                                                    if (startup.Checked)
                                                        k.SetValue("K86LayoutLight", "\"" + Application.ExecutablePath + "\" --tray");
                                                    else k.DeleteValue("K86LayoutLight", false);
                                                } 
                                                catch (Exception e) 
                                                { 
                                                    Report(e.Message); 
                                                } 
                                            };
        panel.Controls.Add(new Label { AutoSize = true, Text = "Крестик сворачивает в трей. Полный выход — через меню значка." });
        panel.Controls.Add(status);
        var log = new Button { Text = "Открыть журнал", Width = 175, Height = 30 };
        log.Click += (_, _) => {
                                    File.AppendAllText(Path.Combine(Dir, "diagnostics.log"), "");
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", "\"" + Path.Combine(Dir, "diagnostics.log") + "\"") { UseShellExecute = true });
                                };
        panel.Controls.Add(log);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowWindow());
        menu.Items.Add("Выход", null, (_, _) => 
                                                {
                                                    quitting = true;
                                                    Close(); 
                                                }
                        );

        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowWindow();

        timer.Tick += async (_, _) => 
                                    {

                                        if (busy)
                                            return;

                                        if (deviceChanged) 
                                        {
                                            deviceChanged = false;

                                            if (keyboard == null || !keyboard.IsPresent()) 
                                            {
                                                keyboard?.Dispose(); keyboard = null; applied.Invalidate(); retryAfter = DateTime.MinValue;
                                            }
                                        }
                                        int lang = Native.ActiveLanguage();
                                        UpdateTray(lang);

                                        if (DateTime.UtcNow < retryAfter) 
                                            return;

                                        int layer = pendingManual > 0 ? pendingManual : !auto.Checked ? -1 : lang == 0x09 ? (int)en.Value : lang == 0x19 ? (int)ru.Value : -1;

                                        if (layer > 0 && (pendingManual > 0 || applied.Needs(layer, (int)brightness.Value)) && InputQuiet.CanSend((int)quiet.Value))
                                            await Apply(layer, pendingManual > 0 ? 0 : lang);
                                    };
        Shown += async (_, _) => 
                                { 
                                    if (!InputQuiet.Register(Handle))
                                    {
                                        Report("Не удалось включить наблюдение за клавиатурой. Перезапусти утилиту.");
                                        return; 
                                    } 
                                    if (Environment.GetCommandLineArgs().Contains("--tray"))
                                        Hide();
                                    await Connect(); 
                                    timer.Start();
                                };
        FormClosing += (_, e) => 
                                {
                                    if (!quitting && e.CloseReason == CloseReason.UserClosing) 
                                    {
                                        e.Cancel = true;
                                        Hide(); return;
                                    } 

                                    timer.Stop();
                                    tray.Visible = false;
                                    tray.Dispose(); 

                                    if (!busy) 
                                        keyboard?.Dispose(); 
                                };
    }
    static Icon LoadIcon(string name)
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("K86LayoutLight." + name + ".ico")!;
        using var original = new Icon(stream);
        return (Icon)original.Clone();
    }
    void UpdateTray(int lang)
    {
        var wantedIcon = !auto.Checked ? baseIcon : lang == 9 ? enIcon : lang == 25 ? ruIcon : baseIcon;

        if (!ReferenceEquals(tray.Icon, wantedIcon)) 
            tray.Icon = wantedIcon;

        tray.Text = !auto.Checked ?
                                "K86 Light · ручной режим" :
                                lang == 9 ?
                                "K86 Light · EN · вариант " + en.Value :
                                lang == 25 ? "K86 Light · RU · вариант " + ru.Value : "K86 Light · ожидание EN / RU";
    }
    protected override void WndProc(ref Message m)
    {
        // Windows device notifications replace the periodic HID polling.
        if (m.Msg == 0x00ff) 
            InputQuiet.Observe(m.LParam);

        if (m.Msg == 0x0219)
            deviceChanged = true;

        base.WndProc(ref m);
    }
    void ShowWindow() 
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate(); 
    }
    void Save() 
    { 
        settings.Auto = auto.Checked;
        settings.EnglishLayer = (int)en.Value;
        settings.RussianLayer = (int)ru.Value;
        settings.Brightness = (int)brightness.Value; settings.QuietMilliseconds = (int)quiet.Value;

        try 
        {
            File.WriteAllText(Config, JsonSerializer.Serialize(settings));
        }
        catch (Exception e) 
        { 
            Report("Не удалось сохранить настройки: " + e.Message); 
        } 
    }
    void Report(string text) 
    {
        status.Text = text;
        try 
        {
            File.AppendAllText(Path.Combine(Dir, "diagnostics.log"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine); 
        } 
        catch { } 
    }
    async Task Connect()
    {
        if (busy)
            return; 
        busy = true;

        try 
        {
            keyboard ??= await Task.Run(Keyboard.Open);
            Report(keyboard.Description + ". Проверь вариант 1 и вариант 2.");
        }
        catch (Exception e) 
        { 
            Report(e.Message); 
            retryAfter = DateTime.UtcNow.AddSeconds(1);
        }
        finally 
        {
            busy = false; 
        }
    }
    async Task Apply(int layer, int expectedLanguage)
    {
        if (busy) 
            return; 

        busy = true;
        int level = (int)brightness.Value, wait = (int)quiet.Value, request = generation;

        bool CanSend() => Volatile.Read(ref generation) == request &&
            (expectedLanguage == 0 || Native.ActiveLanguage() == expectedLanguage) && InputQuiet.CanSend(wait);
        try {
            if (!CanSend())
                return;

            keyboard ??= await Task.Run(Keyboard.Open);
            bool sent = await Task.Run(() => keyboard.TrySelectLayer(layer, level, CanSend));

            if (!sent)
                return;

            applied.Mark(layer, level);
            if (request == generation && pendingManual == layer) pendingManual = 0;
            retryAfter = DateTime.MinValue;
            Report("Команда подсветки отправлена после паузы: слой " + layer + ". " + keyboard.Description);
        }
        catch (Exception e) 
        {
            keyboard?.Dispose();
            keyboard = null;
            applied.Invalidate();
            retryAfter = DateTime.UtcNow.AddSeconds(1);
            Report("Не удалось применить подсветку: " + e.Message); 
        }
        finally 
        { 
            busy = false; 
        }
    }
}
internal sealed class Keyboard : IDisposable
{
    readonly SafeFileHandle handle;
    readonly int length;
    readonly uint id;
    readonly ReceiverTransport? receiver;
    public bool Wireless => receiver != null;
    public string Path { get; }
    public string Description => "K86, ID " + id + (Wireless ? " · 2,4 ГГц" : " · USB") + " — сохранённые пользовательские слои";
    Keyboard(SafeFileHandle h, int size, uint deviceId, string path, bool wireless) {
        handle = h;
        length = size;
        id = deviceId;
        Path = path;

        if (wireless)
            receiver = new ReceiverTransport(Send, Read);
    }
    public static Keyboard Open()
    {
        var found = new List<Keyboard>(); var errors = new List<string>();
        foreach (string path in Native.HidPaths())
        {
            if (!path.Contains("vid_3151", StringComparison.OrdinalIgnoreCase)) 
                continue;

            var h = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (h.IsInvalid) 
            {
                h.Dispose();
                continue; 
            }
            bool keep = false;
            try
            {
                if (!Native.HidD_GetPreparsedData(h, out var pp)) 
                    continue;

                Native.Caps caps;
                try 
                {
                    if (Native.HidP_GetCaps(pp, out caps) != 0x110000)
                        continue; 
                }
                finally 
                { 
                    Native.HidD_FreePreparsedData(pp);
                }

                if (caps.UsagePage != 0xffff || caps.Usage != 2 || caps.FeatureReportByteLength != 65) 
                    continue;

                bool wireless = path.Contains("pid_4011", StringComparison.OrdinalIgnoreCase);
                var k = new Keyboard(h, caps.FeatureReportByteLength, 0, path, wireless);
                var reply = k.Query(0x8f); uint deviceId = BitConverter.ToUInt32(reply, 2);

                if (deviceId != 1168 && deviceId != 2730 && deviceId != 4094)
                { 
                    errors.Add("Неизвестный ID " + deviceId + ": запись отключена.");
                    continue; 
                }
                found.Add(new Keyboard(h, caps.FeatureReportByteLength, deviceId, path, wireless)); keep = true;
            }
            catch (Exception e) 
            {
                errors.Add(path + ": " + e.Message); 
            }

            finally 
            { 
                if (!keep)
                    h.Dispose();
            }
        }
        var wired = found.Where(k => !k.Wireless).ToList();

        if (wired.Count == 1) 
        {
            foreach (var k in found.Where(k => k.Wireless)) 
                k.Dispose(); 
            return wired[0]; 
        }

        if (found.Count == 1) 
            return found[0];

        foreach (var k in found) 
            k.Dispose();

        if (found.Count > 1)
            throw new Exception("Обнаружено несколько K86. Оставь подключённой одну.");

        throw new Exception("Не удалось открыть поддерживаемый интерфейс K86. " + string.Join("; ", errors.Distinct()));
    }
    byte[] Query(byte op)
    {
        var request = new byte[length]; request[1] = op; request[8] = (byte)(255 - op);

        if (receiver != null)
                return receiver.Query(request);

        Send(request); Thread.Sleep(30);
        var response = Read();

        if (response[1] != op) 
            throw new Exception("Клавиатура вернула неожиданный ответ HID.");

        return response;
    }
    byte[] Read() 
    {
        var b = new byte[length];

        if (!Native.HidD_GetFeature(handle, b, b.Length)) 
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        return b;
    }
    void Send(byte[] b) 
    { 
        if (!Native.HidD_SetFeature(handle, b, b.Length)) 
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); 
    }
    public bool TrySelectLayer(int layer, int brightness, Func<bool> canSend)
    {
        var packet = Protocol.LayerPacket(layer, brightness, id == 1168 ? 5 : 2);
        // SET success is the transport acknowledgement. Firmware GET responses may
        // normalize RGB or return stale data; they must not trigger repeated writes.
        if (receiver != null) 
            return receiver.TryWrite(packet, canSend);

        if (!canSend())
            return false;

        Send(packet); 
        return true;
    }
    public bool IsPresent() {
        try 
        {
            return Native.HidPaths().Contains(Path, StringComparer.OrdinalIgnoreCase); 
        }

        catch 
        { 
            return true; // Enumeration failure is not evidence of removal.
        } 
    }
    public void Dispose() => handle.Dispose();
}
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] 
    public struct InterfaceData { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] 
    public struct Caps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices, NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices, NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }
    [DllImport("hid.dll")] 
    static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint needed, IntPtr info);
    [DllImport("setupapi.dll")]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] 
    public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
    [DllImport("hid.dll")]
    public static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")]
    public static extern int HidP_GetCaps(IntPtr data, out Caps caps);
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] data, int size);
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetFeature(SafeFileHandle h, [In, Out] byte[] data, int size);
    [DllImport("user32.dll")] 
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr process);
    [DllImport("user32.dll")]
    static extern IntPtr GetKeyboardLayout(uint thread);
    [StructLayout(LayoutKind.Sequential)]
    struct GuiThreadInfo
    {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public int CaretLeft, CaretTop, CaretRight, CaretBottom;
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    public static int ActiveLanguage() 
    { 
        var w = GetForegroundWindow();

        if (w == IntPtr.Zero) 
            return 0;

        // The top-level window and its focused editor can belong to different
        // threads (e.g. modern Notepad). Read the actual keyboard input target.
        // Thread 0 here means the foreground queue, not our own UI/worker thread.
        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(0, ref info))
            return 0;

        uint t = GetWindowThreadProcessId(info.Focus != IntPtr.Zero ? info.Focus : w, IntPtr.Zero);

        // Focus may move while querying. Retry on the next tick instead of
        // applying a layout sampled from an outgoing foreground window.
        if (t == 0 || GetForegroundWindow() != w)
            return 0; 
        
        return (int)(GetKeyboardLayout(t).ToInt64() & 0x3ff);
    }
    public static IEnumerable<string> HidPaths()
    {
        HidD_GetHidGuid(out var guid);
        var set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);

        if (set == new IntPtr(-1)) 
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            for (uint i = 0; ; i++)
            {
                var data = new InterfaceData 
                                            {
                                                Size = Marshal.SizeOf<InterfaceData>() 
                                            };

                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data))
                {
                    int error = Marshal.GetLastWin32Error();

                    if (error == 259)
                        break; 
                    
                    throw new System.ComponentModel.Win32Exception(error); 
                }

                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out uint size, IntPtr.Zero);

                if (size < 8) 
                    continue;

                var detail = Marshal.AllocHGlobal((int)size);

                try 
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);

                    if (SetupDiGetDeviceInterfaceDetail(set, ref data, detail, size, out _, IntPtr.Zero)) 
                    { 
                        var p = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));

                        if (p != null) 
                            yield return p; 
                    } 
                }
                finally 
                {
                    Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally 
        { 
            SetupDiDestroyDeviceInfoList(set); 
        }
    }
}
