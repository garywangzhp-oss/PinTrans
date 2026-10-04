# PinTrans installer

## User installation

Release users should run:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Quick-Install.ps1
```

The script installs Weasel and rime-ice when missing, installs or builds PinTrans, deploys the Lua plugin, and starts the tray app.

## Build

```powershell
.\installer\Build-PinTrans.ps1
```

## Install an existing build

```powershell
.\installer\Install-PinTrans.ps1
```

## Uninstall

```powershell
.\installer\Uninstall-PinTrans.ps1
```

The old `Build-HanBridge.ps1`, `Install-HanBridge.ps1`, and `Uninstall-HanBridge.ps1` names remain as compatibility wrappers.

## Configuration merge

The installer backs up `rime_ice.custom.yaml` and inserts a managed patch block. If a conflicting key already exists, installation stops instead of overwriting user configuration.