using System.Data.SqlClient;

namespace Monocle.Profiler
{
    public interface IDbProfiler
    {
        void StartProfiling(SqlCommand command);
        void EndProfiling();
        DbProfilingResults Results { get; }
    }
}
