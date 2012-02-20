using System;
using System.Data;
using System.Data.SqlClient;
using Monocle.Profiler;

namespace Monocle
{
    internal class MsSqlCommand : IDisposable
    {
        private IDbProfiler _profiler;
        private bool _isProfiling;
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

            if (_profiler != null)
                _isProfiling = true;
        }

        private static SqlConnection CreateConnection(string connString)
        {
            return new SqlConnection(connString);
        }

        public DataTable ExecuteDataTable()
        {
            _sqlConn.Open();

            if (_isProfiling)
                _profiler.StartProfiling(_command);

            var dt = new DataTable("table");

            var da = new SqlDataAdapter(_command);
            
            da.Fill(dt);

            _sqlConn.Close();

            return dt;
        }

        public IDataReader ExecuteReader()
        {
            _sqlConn.Open();

            if (_isProfiling)
                _profiler.StartProfiling(_command);

            return _command.ExecuteReader(CommandBehavior.CloseConnection);
        }

        public void ExecuteNonCommand()
        {
            _sqlConn.Open();

            if (_isProfiling)
                _profiler.StartProfiling(_command);

            _command.ExecuteNonQuery();

            if (_isProfiling)
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
