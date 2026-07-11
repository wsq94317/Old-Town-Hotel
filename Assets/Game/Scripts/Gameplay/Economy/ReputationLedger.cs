using System.Collections.Generic;

// Rolling hotel reputation: the last N checkout satisfaction multipliers averaged
// and mapped onto a 1.0–5.0 star score. Star brackets drive daily guest volume
// (EconomyConfigSO.GuestsPerDayFor) — success creates the next staffing squeeze.
// Pure C# -> fully unit-testable. (Phase 6, economy-staffing.md §3.10/§4.2.)
public sealed class ReputationLedger
{
    public const float MinSatisfaction = 0.5f;
    public const float MaxSatisfaction = 1.5f;

    private readonly Queue<float> _samples;
    private readonly int _windowSize;
    private float _sum;

    public ReputationLedger(int windowSize)
    {
        _windowSize = System.Math.Max(1, windowSize);
        _samples = new Queue<float>(_windowSize);
    }

    public int SampleCount => _samples.Count;

    // No guests yet -> neutral 1.0 average: a fresh hotel opens at 3.0★.
    public float AverageSatisfaction => _samples.Count == 0 ? 1f : _sum / _samples.Count;

    // avgSat 0.5 -> 1★, 1.0 -> 3★, 1.5 -> 5★ (economy GDD §4.2).
    public float Stars
    {
        get
        {
            float stars = 1f + (AverageSatisfaction - MinSatisfaction) * 4f;
            return stars < 1f ? 1f : (stars > 5f ? 5f : stars);
        }
    }

    public void RecordGuest(float satisfactionMult)
    {
        float s = satisfactionMult < MinSatisfaction ? MinSatisfaction
                : satisfactionMult > MaxSatisfaction ? MaxSatisfaction
                : satisfactionMult;
        _samples.Enqueue(s);
        _sum += s;
        if (_samples.Count > _windowSize) _sum -= _samples.Dequeue();
    }

    // ── Save / load ──────────────────────────────────────────────────────────
    public List<float> ExportSamples() => new List<float>(_samples);

    public void ImportSamples(IEnumerable<float> samples)
    {
        _samples.Clear();
        _sum = 0f;
        if (samples == null) return;
        foreach (float s in samples) RecordGuest(s);
    }
}
