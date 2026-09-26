namespace SaveState.Core.Models;

/// <summary>
/// One app the user chose to remember. Serialized into the <c>backups.apps</c> jsonb column
/// as <c>{ name, publisher, version, note }</c>.
/// </summary>
public sealed record SavedApp(string Name, string? Publisher, string? Version, string? Note);
