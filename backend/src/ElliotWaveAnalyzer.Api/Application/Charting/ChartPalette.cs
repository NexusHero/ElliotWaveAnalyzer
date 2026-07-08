namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// The set of colours <see cref="AnnotatedChartComposer"/> draws with. Extracted from what were
/// module-level constants (#227 AC2) so a scene can be composed in either <see cref="ChartTheme"/>
/// without the composer's layout logic knowing about themes at all — it just asks for a palette.
/// </summary>
internal sealed record ChartPalette(
    ChartColor Background,
    ChartColor Foreground,
    ChartColor Muted,
    ChartColor Grid,
    ChartColor Bull,
    ChartColor Bear,
    ChartColor BaseChannel,
    ChartColor AccelChannel,
    ChartColor EntryFill,
    ChartColor TargetFill,
    ChartColor Invalidation,
    ChartColor Label)
{
    public static readonly ChartPalette Dark = new(
        Background: new(0x10, 0x14, 0x1A),
        Foreground: new(0xC8, 0xD0, 0xDA),
        Muted: new(0x7A, 0x86, 0x94),
        Grid: new(0x2A, 0x31, 0x3C),
        Bull: new(0x26, 0xA6, 0x9A),
        Bear: new(0xEF, 0x53, 0x50),
        BaseChannel: new(0x42, 0xA5, 0xF5),
        AccelChannel: new(0xAB, 0x47, 0xBC),
        EntryFill: new(0x42, 0xA5, 0xF5, 0x33),
        TargetFill: new(0x66, 0xBB, 0x6A, 0x33),
        Invalidation: new(0xEF, 0x53, 0x50),
        Label: new(0xFF, 0xCA, 0x28));

    public static readonly ChartPalette Light = new(
        Background: new(0xF5, 0xF6, 0xF8),
        Foreground: new(0x1A, 0x1F, 0x28),
        Muted: new(0x6B, 0x74, 0x80),
        Grid: new(0xDD, 0xE1, 0xE6),
        Bull: new(0x1B, 0x8A, 0x7D),
        Bear: new(0xD3, 0x2F, 0x2C),
        BaseChannel: new(0x21, 0x66, 0xC2),
        AccelChannel: new(0x7B, 0x1F, 0xA2),
        EntryFill: new(0x21, 0x66, 0xC2, 0x33),
        TargetFill: new(0x2E, 0x7D, 0x32, 0x33),
        Invalidation: new(0xD3, 0x2F, 0x2C),
        Label: new(0xB8, 0x86, 0x0B));

    public static ChartPalette For(ChartTheme theme) => theme == ChartTheme.Light ? Light : Dark;
}
