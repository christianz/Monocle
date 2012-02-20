using System.Collections.Generic;
using System.Linq;

namespace Monocle.Profiler
{
    public static class DbProfiling
    {
        private static readonly List<DbProfilingResults> Results = new List<DbProfilingResults>();
        private static readonly Dictionary<string, QueryRunResult> QueryTimings = new Dictionary<string, QueryRunResult>();

        public static void AddResult(DbProfilingResults result)
        {
            Results.Add(result);

            var query = result.Query;

            if (!QueryTimings.ContainsKey(query))
                QueryTimings[query] = new QueryRunResult();

            QueryTimings[query].AddElapsed(result.ElapsedMilliseconds);
        }

        public static Dictionary<string, QueryRunResult> GetQueryTimings()
        {
            return QueryTimings;
        }
    }

    public class QueryRunResult
    {
        private readonly List<long> _elapsedTicks = new List<long>();

        public void AddElapsed(long elapsed)
        {
            _elapsedTicks.Add(elapsed);
        }

        public double AvgElapsedTicks
        {
            get { return _elapsedTicks.Average(); }
        }

        public int CallCount
        {
            get { return _elapsedTicks.Count; }
        }
    }
}
