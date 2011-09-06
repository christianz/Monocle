namespace Monocle
{
    public static class Database
    {
        public static void Initialize(string connectionString)
        {
            MonocleDb.Initialize(connectionString);
        }
    }
}
