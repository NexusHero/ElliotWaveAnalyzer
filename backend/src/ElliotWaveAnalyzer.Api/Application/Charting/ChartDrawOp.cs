namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// A single primitive draw instruction in a <see cref="ChartScene"/>. The
/// <see cref="AnnotatedChartComposer"/> emits an ordered list of these in <em>pixel</em> space (the
/// geometry decisions live here, in the pure Application layer, so they are unit-testable) and a
/// rendering backend replays them onto a canvas. The closed set of shapes is <see cref="ChartLineOp"/>,
/// <see cref="ChartRectOp"/> and <see cref="ChartTextOp"/>.
/// </summary>
public abstract record ChartDrawOp;
