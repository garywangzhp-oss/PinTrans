using System.Diagnostics;
using System.Runtime.InteropServices;
using HanBridge.Core;
using HanBridge.Core.Ipc;
using HanBridge.Core.Logging;
using HanBridge.Core.Security;
using HanBridge.Core.Storage;
using HanBridge.Core.Translation;

namespace HanBridge.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppPaths _paths;
    private readonly SettingsStore _settingsStore;
    private readonly SecretStore _secretStore;
    private readonly TranslationCache _cache;
    private readonly FileIpcServer _ipcServer;
    private readonly SafeLogger _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleTranslationItem;
    private readonly ToolStripMenuItem _statusItem;
    private readonly Icon _icon;
    private readonly HotKeyWindow _hotKeyWindow;
    private bool _hotKeyRegistered;

    private SettingsForm? _settingsForm;

    public TrayApplicationContext(
        AppPaths paths,
        SettingsStore settingsStore,
        SecretStore secretStore,
        TranslationCache cache,
        FileIpcServer ipcServer,
        SafeLogger logger)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _secretStore = secretStore;
        _cache = cache;
        _ipcServer = ipcServer;
        _logger = logger;

        _icon = SystemIcons.Information;
        _statusItem = new ToolStripMenuItem("状态：启动中") { Enabled = false };
        _toggleTranslationItem = new ToolStripMenuItem("翻译已开启")
        {
            CheckOnClick = true,
            Checked = settingsStore.Current.TranslationEnabled
        };
        _toggleTranslationItem.Click += (_, _) => ToggleTranslation();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleTranslationItem);
        menu.Items.Add("设置", null, (_, _) => ShowSettings());
        menu.Items.Add("打开日志目录", null, (_, _) => OpenFolder(_paths.LogDirectory));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "HanBridge",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _hotKeyWindow = new HotKeyWindow();
        _hotKeyWindow.HotKeyPressed += ToggleTranslationFromHotKey;
        _hotKeyRegistered = NativeMethods.RegisterHotKey(
            _hotKeyWindow.Handle,
            NativeMethods.HotKeyId,
            NativeMethods.ModControl | NativeMethods.ModAlt | NativeMethods.ModNoRepeat,
            NativeMethods.KeyE);
        if (!_hotKeyRegistered)
        {
            _logger.Warn("global-hotkey-registration-failed key=Control+Alt+E");
        }

        _ipcServer.StatusChanged += OnBridgeStatusChanged;
        _ = _ipcServer.RunAsync(_shutdown.Token);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shutdown.Cancel();
            _ipcServer.CancelActiveRequest();
            if (_hotKeyRegistered)
            {
                NativeMethods.UnregisterHotKey(_hotKeyWindow.Handle, NativeMethods.HotKeyId);
            }

            _hotKeyWindow.DestroyHandle();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _shutdown.Dispose();
            _settingsForm?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ToggleTranslation()
    {
        ApplyTranslationEnabled(_toggleTranslationItem.Checked, showNotification: true);
    }

    private void ToggleTranslationFromHotKey()
    {
        ApplyTranslationEnabled(!_settingsStore.Current.TranslationEnabled, showNotification: true);
    }

    private void ApplyTranslationEnabled(bool enabled, bool showNotification)
    {
        var settings = _settingsStore.Current;
        settings.TranslationEnabled = enabled;
        _settingsStore.Save(settings);
        _toggleTranslationItem.Checked = enabled;
        _toggleTranslationItem.Text = enabled ? "翻译已开启" : "翻译已关闭";
        SetStatus(new BridgeStatus(enabled ? "idle" : "disabled", null));

        if (showNotification)
        {
            _notifyIcon.ShowBalloonTip(
                1500,
                "HanBridge",
                enabled ? "翻译已开启" : "翻译已关闭",
                ToolTipIcon.Info);
        }
    }

    internal void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Show();
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_paths, _settingsStore, _secretStore, _cache);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            _logger.Error("open-folder-failed", exception);
        }
    }

    private void OnBridgeStatusChanged(BridgeStatus status)
    {
        if (_notifyIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _notifyIcon.ContextMenuStrip.BeginInvoke(() => SetStatus(status));
            return;
        }

        SetStatus(status);
    }

    private void SetStatus(BridgeStatus status)
    {
        var label = status.State switch
        {
            "not_configured" => "未配置 API Key",
            "disabled" => "翻译已关闭",
            "debouncing" => "等待输入完成",
            "translating" => "翻译中",
            "ready" => "翻译就绪",
            "paused" => "连续失败，已暂停",
            "error" => "翻译错误",
            "stopped" => "已停止",
            _ => "空闲"
        };

        _statusItem.Text = $"状态：{label}";
        _notifyIcon.Text = $"HanBridge · {label}";
        _toggleTranslationItem.Checked = _settingsStore.Current.TranslationEnabled;
        _toggleTranslationItem.Text = _toggleTranslationItem.Checked ? "翻译已开启" : "翻译已关闭";
    }

    private sealed class HotKeyWindow : NativeWindow
    {
        public event Action? HotKeyPressed;

        public HotKeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WmHotKey)
            {
                HotKeyPressed?.Invoke();
                return;
            }

            base.WndProc(ref message);
        }
    }

    private static class NativeMethods
    {
        public const int HotKeyId = 0x4842;
        public const int WmHotKey = 0x0312;
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModNoRepeat = 0x4000;
        public const uint KeyE = 0x45;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
    }
}