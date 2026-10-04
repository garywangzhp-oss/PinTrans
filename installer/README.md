# PinTrans installer

## Build

```powershell
.\installer\Build-HanBridge.ps1
```

## Install

```powershell
.\installer\Install-HanBridge.ps1
```

The installer expects Weasel 0.17.x and rime-ice to be installed already. It backs up `rime_ice.custom.yaml`, inserts a managed patch block, copies the Lua plugin, publishes the tray app to `%LOCALAPPDATA%\HanBridge\app`, and registers HKCU autostart.

If `rime_ice.custom.yaml` already has one of the managed keys, installation stops instead of overwriting user configuration.

## Uninstall

```powershell
.\installer\Uninstall-HanBridge.ps1
```

User data is preserved by default. Add `-RemoveApiKeys`, `-RemoveCache`, or `-RemoveAllData` to remove selected data.