# PinTrans

PinTrans 是一款 Windows 中英双语 AI 输入法，基于 Weasel、librime 和 rime-ice。

```text
输入中文拼音
  -> 候选 1：中文
  -> 候选 2：英文翻译
  -> 正常选择中文或英文
```

快捷键：

- `Ctrl+Alt+E`：开启或关闭翻译。
- `Ctrl+Alt+Enter`：翻译当前高亮的中文候选。
- `Ctrl+Alt+P`：当 Rime 没有生成想要的中文候选时，直接根据拼音推断并翻译成英文。

PinTrans 使用 OpenAI-compatible Chat Completions API，内置 DeepSeek、OpenCode Go 和自定义接口预设。

## 普通用户安装

1. 打开 [Releases](https://github.com/garywangzhp-oss/PinTrans/releases/latest)。
2. 下载 `PinTrans-win-x64.zip`。
3. 解压压缩包。
4. 在解压目录打开 PowerShell，运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Quick-Install.ps1
```

安装脚本会自动：

- 检查是否为 Windows x64；
- 通过 winget 安装 Weasel 0.17.4；
- 在需要时安装 rime-ice 到 `%APPDATA%\Rime`；
- 部署 PinTrans Lua 插件；
- 安装并启动 PinTrans 托盘程序；
- 注册当前用户自启动；
- 触发 Weasel 重新部署。

安装 Weasel 时，Windows 可能请求管理员权限。

安装完成后：

1. 打开 PinTrans 托盘图标；
2. 选择“设置”；
3. 选择 DeepSeek、OpenCode Go 或自定义 OpenAI-compatible 接口；
4. 填入自己的 API Key；
5. 点击“测试连接”；
6. 点击“保存”。

## 从源码安装

要求：Windows 10 22H2 或 Windows 11 x64、Git、PowerShell。

```powershell
git clone https://github.com/garywangzhp-oss/PinTrans.git
cd PinTrans
powershell -ExecutionPolicy Bypass -File .\installer\Quick-Install.ps1
```

如果本地还没有 `PinTrans.exe`，脚本会在需要时安装 .NET 8 SDK 并自动构建。

## 服务商预设

DeepSeek：

- Endpoint：`https://api.deepseek.com/chat/completions`
- Model：`deepseek-flash`

OpenCode Go：

- Endpoint：`https://opencode.ai/zen/go/v1/chat/completions`
- Model 预设：`deepseek-v4.1-flash`、`glm-5.3-flash`、`mimo-v2.6-flash`、`kimi-k3`、`longcat-2.0`

自定义：

- 支持任意 OpenAI-compatible Chat Completions 接口。

OpenCode Go 返回结果会被限制为 JSON，PinTrans 只读取其中的 `translation` 字段，避免模型解释内容进入英文候选。

## 构建

```powershell
.\installer\Build-PinTrans.ps1
```

自包含的 Windows x64 程序会输出到：

```text
artifacts\publish\win-x64\PinTrans.exe
```

## 使用已有构建安装

```powershell
.\installer\Install-PinTrans.ps1
```

安装器会备份 `rime_ice.custom.yaml`，插入一个幂等的托管配置块，部署 Lua 插件，安装托盘程序，并注册自启动。如果发现冲突配置，安装会停止，不会覆盖用户已有设置。

## 卸载

```powershell
.\installer\Uninstall-PinTrans.ps1
```

默认会保留用户 Rime 数据。可以使用以下参数显式删除：

- `-RemoveApiKeys`：删除 API Key；
- `-RemoveCache`：删除翻译缓存；
- `-RemoveAllData`：删除全部 PinTrans 数据。

## 隐私与安全

- API Key 使用 Windows DPAPI 加密。
- SQLite 翻译缓存按行使用 DPAPI 加密。
- IPC 文件保存在当前用户私有目录。
- 日志不记录 API Key、完整输入正文或完整模型输出。
- 默认没有遥测，也没有中转服务器。
- 对密码框进行尽力检测，识别到标准密码编辑框时不会发起翻译。
- 关闭翻译后，不会发送模型请求。

## 当前限制

- 仅支持 Windows 10/11 x64。
- 依赖 Weasel 0.17.x 和 rime-ice。
- 中文输入质量取决于 rime-ice，不等同于微信输入法的整体水平。
- 云端模型首次翻译通常需要 1～4 秒。
- 拼音兜底推断对歧义输入可能产生不同解释。
- 暂无账号系统、云同步、自动更新和移动端。

## 开发

```powershell
dotnet test HanBridge.sln -c Release
dotnet build HanBridge.sln -c Release
```

架构与范围文档：

- `docs\architecture.md`
- `docs\v1-scope.md`
- `docs\comparison.md`

## 内部兼容名称

内部 .NET 命名空间、配置键、缓存路径和用户数据目录仍使用 `HanBridge`，用于确保已有安装升级到 PinTrans 后不会丢失设置和翻译缓存。普通用户无需直接操作这些名称。