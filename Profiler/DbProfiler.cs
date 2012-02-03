using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace Monocle.Profiler
{
    public class DbProfiler : IDbProfiler
    {
        private readonly Stopwatch _stopWatch = new Stopwatch();

        public void StartProfiling(SqlCommand command)
        {
            var query = command.CommandText;

            foreach (SqlParameter param in command.Parameters)
            {
                var val = param.Value.ToString();

                if (param.SqlDbType == SqlDbType.UniqueIdentifier || param.SqlDbType == SqlDbType.Text || param.SqlDbType == SqlDbType.DateTime)
                {
                    val = "'" + val + "'";
                }

                query = query.Replace("@" + param.ParameterName, val);
            }

            Results = new DbProfilingResults { Query = query };
            _stopWatch.Start();
        }

        public void EndProfiling()
        {
            _stopWatch.Stop();
            Results.ElapsedMilliseconds = _stopWatch.ElapsedMilliseconds;
        }

        public DbProfilingResults Results { get; private set; }
    }
}
