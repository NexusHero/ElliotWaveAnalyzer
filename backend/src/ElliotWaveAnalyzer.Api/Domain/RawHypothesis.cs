namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A structure hypothesis exactly as the LLM proposed it — an unvalidated structure name plus a
/// one-line reason. The name is a free string here on purpose: it is mapped against the known Elliott
/// vocabulary (<see cref="Application.StructureVocabulary"/>) and an out-of-vocabulary suggestion is
/// dropped <b>before</b> it can reach the deterministic engine (the LLM proposes; the engine decides).
/// </summary>
public sealed record RawHypothesis(string Structure, string Reason);
