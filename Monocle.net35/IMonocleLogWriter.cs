using System;

namespace Monocle
{
    public interface IMonocleLogWriter
    {
        void Write(DateTime timeStamp, string message);
    }
}
