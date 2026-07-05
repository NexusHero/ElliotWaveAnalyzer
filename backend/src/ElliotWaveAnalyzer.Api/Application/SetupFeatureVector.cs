using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Encodes a setup's <see cref="SetupFeatures"/> into the normalised numeric vector the analog search
/// compares, and scores similarity between two setups by cosine. Deterministic and pure: the same
/// features always encode to the same vector, so retrieval is reproducible. The structure and
/// direction are weighted above the numeric texture — a Zigzag is never a good analog for an Impulse —
/// while the score/confluence/reward/distance/momentum fields discriminate <em>within</em> a shape.
/// All components are non-negative, so cosine lands in [0, 1] (1 = identical fingerprint).
/// </summary>
public static class SetupFeatureVector
{
    // A wrong pattern family or direction should dominate the distance; the finer numeric texture
    // then ranks the same-shape candidates. These weights are the retrieval seam's one tuning knob.
    private const double StructureWeight = 2.0;
    private const double DirectionWeight = 1.5;

    // Squash scales: the value that maps to 0.5 on the 0..1 curve x/(x+scale).
    private const double RewardToRiskHalf = 2.0; // R:R of 2 ⇒ 0.5
    private const double DistanceHalf = 0.10; // a 10% stop distance ⇒ 0.5

    private static readonly StructureKind[] Structures =
    [
        StructureKind.Impulse,
        StructureKind.Diagonal,
        StructureKind.Zigzag,
        StructureKind.Flat,
        StructureKind.Triangle,
    ];

    /// <summary>Encodes features into the weighted, normalised vector used for cosine similarity.</summary>
    public static double[] Encode(SetupFeatures f)
    {
        var vector = new double[Structures.Length + 7];
        for (var i = 0; i < Structures.Length; i++)
        {
            vector[i] = f.Structure == Structures[i] ? StructureWeight : 0.0;
        }

        var n = Structures.Length;
        vector[n] = (f.Bullish ? 1.0 : 0.0) * DirectionWeight;
        vector[n + 1] = Clamp01(f.Score);
        vector[n + 2] = Clamp01(f.ConfluenceStrength);
        vector[n + 3] = Squash(f.RewardToRisk, RewardToRiskHalf);
        vector[n + 4] = Squash(f.DistanceToInvalidationPct, DistanceHalf);
        vector[n + 5] = Clamp01(f.RsiRegime);
        vector[n + 6] = Clamp01(f.MacdRegime);
        return vector;
    }

    /// <summary>Cosine similarity of two encoded vectors, in [0, 1]; 0 if either has no magnitude.</summary>
    public static double Cosine(double[] a, double[] b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vectors must have equal length.");
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na <= 0 || nb <= 0) return 0.0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    /// <summary>Convenience: similarity of two setups' features, in [0, 1].</summary>
    public static double Similarity(SetupFeatures a, SetupFeatures b) => Cosine(Encode(a), Encode(b));

    private static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

    // A monotone squash of a non-negative unbounded quantity into [0, 1): value/(value + scale).
    private static double Squash(double value, double scale) =>
        value <= 0 ? 0 : value / (value + scale);
}
