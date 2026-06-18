using System.Collections.Generic;

// Tracks active renovation jobs; advancing a day returns the ones that finished
// (so the caller can flip those rooms to their new tier). Pure C#. (Phase 5.)
public sealed class RenovationQueue
{
    private readonly List<RenovationJob> _jobs = new List<RenovationJob>();

    public IReadOnlyList<RenovationJob> Active => _jobs;
    public int ActiveCount => _jobs.Count;

    public void Start(RenovationJob job)
    {
        if (job != null && !job.IsComplete) _jobs.Add(job);
    }

    // Advance all jobs one day; remove and return those now complete.
    public List<RenovationJob> TickDay()
    {
        var completed = new List<RenovationJob>();
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            _jobs[i].TickDay();
            if (_jobs[i].IsComplete)
            {
                completed.Add(_jobs[i]);
                _jobs.RemoveAt(i);
            }
        }
        return completed;
    }
}
