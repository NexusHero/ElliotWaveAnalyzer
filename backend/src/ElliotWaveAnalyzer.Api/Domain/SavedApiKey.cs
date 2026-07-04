namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The safe-to-return view of a stored API key: which provider it is for, the last four
/// characters (for the user to recognize it), and whether it is the active default. The key
/// itself is never included.
/// </summary>
public sealed record SavedApiKey(string Provider, string Last4, bool IsDefault);
