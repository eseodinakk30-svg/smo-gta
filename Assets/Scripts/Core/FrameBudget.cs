using System.Diagnostics;
using UnityEngine;

namespace SanMonica.Core
{
    /// <summary>
    /// Cooperative time slicing helper. Long running generators (world chunks,
    /// navigation graph, texture baking) yield through this so a single frame
    /// never blows past its budget, which is what keeps streaming seamless.
    /// </summary>
    public class FrameBudget
    {
        private readonly Stopwatch _sw = new Stopwatch();
        private double _budgetMs;

        public FrameBudget(double budgetMs = 4.0)
        {
            _budgetMs = budgetMs;
            _sw.Start();
        }

        public double BudgetMs
        {
            get => _budgetMs;
            set => _budgetMs = Mathf.Clamp((float)value, 0.5f, 33f);
        }

        public void Begin()
        {
            _sw.Restart();
        }

        /// <summary>True when the current slice has used up its allowance.</summary>
        public bool Exhausted => _sw.Elapsed.TotalMilliseconds >= _budgetMs;

        public double ElapsedMs => _sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>Round robin scheduler that spreads expensive per-agent updates across frames.</summary>
    public class RoundRobinScheduler
    {
        private int _cursor;

        /// <summary>Returns the slice of indices this frame should process.</summary>
        public void Slice(int total, int perFrame, out int start, out int count)
        {
            if (total <= 0) { start = 0; count = 0; _cursor = 0; return; }
            perFrame = Mathf.Clamp(perFrame, 1, total);
            if (_cursor >= total) _cursor = 0;
            start = _cursor;
            count = Mathf.Min(perFrame, total - _cursor);
            _cursor += count;
            if (_cursor >= total) _cursor = 0;
        }
    }
}
