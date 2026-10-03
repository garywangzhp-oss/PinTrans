namespace HanBridge.Core.Ipc;

public sealed record BridgeStatus(string State, string? Detail = null);