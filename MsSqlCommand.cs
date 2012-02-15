using System;
using System.Data;
using System.Data.SqlClient;
using Monocle.Profiler;

namespace Monocle
{
    public class MsSqlCommand : IDisposable
    {
        private IDbProfiler _profiler;
        private readonly SqlConnection _sqlConn;
        private readonly SqlCommand _command;

        public MsSqlCommand(string connString, SqlCommand command)
        {
            _sqlConn = CreateConnection(connString);

            _command = command;
            _command.Connection = _sqlConn;
        }

        public void Profile(IDbProfiler profiler)
        {
            _profiler = profiler;
        }

        private static SqlConnection CreateConnection(string connString)
        {
            return new SqlConnection(connString);
        }

        public IDataReader ExecuteDataTable()
        {
            _sqlConn.Open();

            if (_profiler != null)
            {
                _profiler.StartProfiling(_command);
            }

            var o = _command.ExecuteReader(CommandBehavior.CloseConnection);
            return o;
        }

        public void ExecuteNonCommand()
        {
            _sqlConn.Open();

            if (_profiler != null)
            {
                _profiler.StartProfiling(_command);
            }

            _command.ExecuteNonQuery();

            if (_profiler != null)
            {
                _profiler.EndProfiling();
                DbProfiling.AddResult(_profiler.Results);
            }

            _sqlConn.Close();
        }

        public void Dispose()
        {
            _sqlConn.Dispose();
        }
    }
}
