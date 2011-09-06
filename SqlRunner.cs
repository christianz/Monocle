using System;
using System.Data;
using System.Data.SqlClient;

namespace Monocle
{
    public class SqlRunner : IDisposable
    {
        private readonly SqlConnection _sqlConn;
        private readonly SqlCommand _command;

        public SqlRunner(string connString, SqlCommand command)
        {
            _sqlConn = CreateConnection(connString);

            _command = command;
            _command.Connection = _sqlConn;
        }

        private static SqlConnection CreateConnection(string connString)
        {
            return new SqlConnection(connString);
        }

        public DataTable ExecuteDataTable()
        {
            var dt = new DataTable("table");

            _sqlConn.Open();

            using (var reader = _command.ExecuteReader())
            {
                var colNum = reader.FieldCount;

                for (var i = 0; i < colNum; i++)
                {
                    var fldType = reader.GetFieldType(i) ?? typeof (string);

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

            _command.ExecuteNonQuery();

            _sqlConn.Close();
        }

        public void Dispose()
        {
            _sqlConn.Dispose();
        }
    }
}
