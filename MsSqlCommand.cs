using System;
using System.Data;
using System.Data.SqlClient;
using Monocle.Profiler;

namespace Monocle
{
    public class MsSqlCommand : IDisposable, IMonocleCommand
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

        public DataTable ExecuteDataTable()
        {
            var dt = new DataTable("table");

            _sqlConn.Open();

            if (_profiler != null)
            {
                _profiler.StartProfiling(_command);
            }

            using (var reader = _command.ExecuteReader())
            {
                if (_profiler != null)
                {
                    _profiler.EndProfiling();
                    DbProfiling.AddResult(_profiler.Results);
                }

                var colNum = reader.FieldCount;

                for (var i = 0; i < colNum; i++)
                {
                    var fldType = reader.GetFieldType(i) ?? typeof(string);

                    dt.Columns.Add(reader.GetName(i), fldType);
                }

                while (reader.Read())
                {
                    var obj = new object[colNum];

                    reader.GetValues(obj);

                    dt.Rows.Add(obj);
                }
            }

            _sqlConn.Close();

            return dt;
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
