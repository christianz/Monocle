using System.Data.SqlClient;

namespace Monocle
{
    public class Parameter
    {
        private readonly SqlParameter _internalParameter;

        public Parameter(string name, object value)
        {
            _internalParameter = new SqlParameter(name, value);
        }

        public string Name
        {
            get { return _internalParameter.ParameterName; }
        }

        public object Value
        {
            get
            {
                return _internalParameter.Value;
            }
        }
    }
}
