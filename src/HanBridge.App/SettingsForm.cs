using System.Diagnostics;
using HanBridge.Core;
using HanBridge.Core.Security;
using HanBridge.Core.Storage;
using HanBridge.Core.Translation;

namespace HanBridge.App;

internal sealed class SettingsForm : Form
{
    private readonly AppPaths _paths;
    private readonly SettingsStore _settingsStore;
    private readonly SecretStore _secretStore;
    private readonly TranslationCache _cache;
    private readonly OpenAiCompatibleClient _providerClient = new();

    private readonly ComboBox _providerCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly TextBox _endpointText = new() { Width = 560 };
    private readonly ComboBox _modelPresetCombo = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 300 };
    private readonly TextBox _apiKeyText = new() { Width = 420, UseSystemPasswordChar = true };
    private readonly ComboBox _proxyModeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _proxyUrlText = new() { Width = 420 };
    private readonly CheckBox _translationEnabledCheck = new() { Text = "启用翻译候选", AutoSize = true };
    private readonly Label _cacheSizeLabel = new() { AutoSize = true, Text = "缓存大小：读取中…" };
    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly Button _testConnectionButton = new() { Text = "测试连接", AutoSize = true };
    private readonly Button _clearCacheButton = new() { Text = "清空翻译缓存", AutoSize = true };
    private readonly Button _openDataFolderButton = new() { Text = "打开数据目录", AutoSize = true };

    private int _activeProviderIndex;
    private bool _loading = true;

    public SettingsForm(
        AppPaths paths,
        SettingsStore settingsStore,
        SecretStore secretStore,
        TranslationCache cache)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _secretStore = secretStore;
        _cache = cache;

        Text = "HanBridge 设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 520);
        Size = new Size(760, 560);
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildLayout();
        LoadSettings();

        Shown += async (_, _) => await RefreshCacheSizeAsync();
        FormClosing += (_, _) => SaveSettings(showConfirmation: false);
    }

    private void BuildLayout()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildProviderPage());
        tabs.TabPages.Add(BuildBehaviorPage());
        tabs.TabPages.Add(BuildCachePage());

        Controls.Add(tabs);
    }

    private TabPage BuildProviderPage()
    {
        var page = new TabPage("模型接口") { Padding = new Padding(18) };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, 0, "服务商", _providerCombo);
        AddRow(layout, 1, "Endpoint", _endpointText);
        AddRow(layout, 2, "模型", _modelPresetCombo);
        AddRow(layout, 3, "API Key", _apiKeyText);
        AddRow(layout, 4, "代理模式", _proxyModeCombo);
        AddRow(layout, 5, "代理地址", _proxyUrlText);

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        actions.Controls.Add(_testConnectionButton);
        actions.Controls.Add(_statusLabel);
        AddRow(layout, 6, string.Empty, actions);

        var saveButton = new Button { Text = "保存", AutoSize = true };
        saveButton.Click += (_, _) => SaveSettings(showConfirmation: true);
        AddRow(layout, 7, string.Empty, saveButton);

        _proxyModeCombo.Items.AddRange(["system", "direct", "custom"]);
        _providerCombo.SelectedIndexChanged += (_, _) => OnProviderChanged();
        _testConnectionButton.Click += async (_, _) => await TestConnectionAsync();

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildBehaviorPage()
    {
        var page = new TabPage("输入行为") { Padding = new Padding(18) };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(_translationEnabledCheck);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(660, 0),
            Text = "全局快捷键：Ctrl+Alt+E 切换翻译开启/关闭。"
        });
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(660, 0),
            Text = "翻译开启后，输入暂停 250ms 且首选候选包含至少两个汉字时，后台会把文本发送到当前服务商。关闭后不会发起请求。"
        });
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildCachePage()
    {
        var page = new TabPage("缓存与数据") { Padding = new Padding(18) };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(_cacheSizeLabel);
        _clearCacheButton.Click += async (_, _) =>
        {
            await _cache.ClearAsync(CancellationToken.None);
            await RefreshCacheSizeAsync();
            MessageBox.Show(this, "翻译缓存已清空。", "HanBridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        _openDataFolderButton.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = _paths.Root, UseShellExecute = true });
        panel.Controls.Add(_clearCacheButton);
        panel.Controls.Add(_openDataFolderButton);
        page.Controls.Add(panel);
        return page;
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void LoadSettings()
    {
        _loading = true;
        var settings = _settingsStore.Current;
        _translationEnabledCheck.Checked = settings.TranslationEnabled;
        _proxyModeCombo.SelectedItem = _proxyModeCombo.Items.Contains(settings.ProxyMode)
            ? settings.ProxyMode
            : "system";
        _proxyUrlText.Text = settings.ProxyUrl ?? string.Empty;

        _providerCombo.Items.Clear();
        foreach (var provider in settings.Providers)
        {
            _providerCombo.Items.Add(provider.DisplayName);
        }

        _activeProviderIndex = Math.Max(0, settings.Providers.FindIndex(provider =>
            provider.Id.Equals(settings.ActiveProviderId, StringComparison.OrdinalIgnoreCase)));
        _providerCombo.SelectedIndex = _activeProviderIndex;
        LoadProviderIntoControls(settings.Providers[_activeProviderIndex]);
        _loading = false;
    }

    private void OnProviderChanged()
    {
        if (_loading || _providerCombo.SelectedIndex < 0)
        {
            return;
        }

        SaveProviderFromControls(_settingsStore.Current.Providers[_activeProviderIndex]);
        _activeProviderIndex = _providerCombo.SelectedIndex;
        LoadProviderIntoControls(_settingsStore.Current.Providers[_activeProviderIndex]);
        _statusLabel.Text = string.Empty;
    }

    private void LoadProviderIntoControls(ProviderSettings provider)
    {
        _endpointText.Text = provider.Endpoint;
        _modelPresetCombo.Items.Clear();
        foreach (var model in ProviderPresets.GetModelPresets(provider.Id))
        {
            _modelPresetCombo.Items.Add(model);
        }
        _modelPresetCombo.Text = provider.Model;
        _apiKeyText.Text = _secretStore.GetSecret(provider.Id) ?? string.Empty;
    }

    private void SaveProviderFromControls(ProviderSettings provider)
    {
        provider.Endpoint = _endpointText.Text.Trim();
        provider.Model = _modelPresetCombo.Text.Trim();
        _secretStore.SetSecret(provider.Id, _apiKeyText.Text);
    }

    private void SaveSettings(bool showConfirmation)
    {
        var settings = _settingsStore.Current;
        if (_activeProviderIndex >= 0 && _activeProviderIndex < settings.Providers.Count)
        {
            SaveProviderFromControls(settings.Providers[_activeProviderIndex]);
            settings.ActiveProviderId = settings.Providers[_activeProviderIndex].Id;
        }

        settings.TranslationEnabled = _translationEnabledCheck.Checked;
        settings.ProxyMode = _proxyModeCombo.SelectedItem?.ToString() ?? "system";
        settings.ProxyUrl = string.IsNullOrWhiteSpace(_proxyUrlText.Text) ? null : _proxyUrlText.Text.Trim();
        _settingsStore.Save(settings);

        if (showConfirmation)
        {
            _statusLabel.Text = "已保存";
        }
    }

    private async Task TestConnectionAsync()
    {
        SaveSettings(showConfirmation: false);
        var settings = _settingsStore.Current;
        var provider = settings.Providers[_activeProviderIndex];
        var apiKey = _secretStore.GetSecret(provider.Id);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(this, "请先填写 API Key。", "HanBridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _testConnectionButton.Enabled = false;
        _statusLabel.Text = "测试中…";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));
            var result = await _providerClient.CompleteAsync(provider, apiKey, "你好，这是连接测试。", settings, timeout.Token);
            _statusLabel.Text = result.IsSuccess ? "连接成功" : $"失败：{result.ErrorCode}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"失败：{exception.GetType().Name}";
        }
        finally
        {
            _testConnectionButton.Enabled = true;
        }
    }

    private async Task RefreshCacheSizeAsync()
    {
        try
        {
            var bytes = await _cache.GetSizeBytesAsync(CancellationToken.None);
            _cacheSizeLabel.Text = $"缓存大小：{bytes / 1024.0:F1} KB";
        }
        catch (Exception exception)
        {
            _cacheSizeLabel.Text = $"缓存大小读取失败：{exception.GetType().Name}";
        }
    }
}