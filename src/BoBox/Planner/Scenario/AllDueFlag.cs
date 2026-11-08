using System.Threading;

namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// A flag any thread can raise and one consumer takes. <see cref="Take"/> reads and clears it in one atomic
    /// step, so a <see cref="Set"/> that lands during a Take is never lost: either that Take sees it or the
    /// next one does.
    /// </summary>
    public sealed class AllDueFlag
    {
        private int _isSet;

        /// <summary>Raises the flag. Safe from any thread.</summary>
        public void Set()
        {
            Interlocked.Exchange(ref _isSet, 1);
        }

        /// <summary>True once per <see cref="Set"/>, and clears the flag.</summary>
        public bool Take()
        {
            return Interlocked.Exchange(ref _isSet, 0) == 1;
        }
    }
}
