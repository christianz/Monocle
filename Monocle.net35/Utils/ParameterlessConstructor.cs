using System;
using System.Linq.Expressions;

namespace Monocle.Utils
{
    internal static class ParameterlessConstructor<T> where T : new()
    {
        internal static T Create()
        {
            return Func();
        }

        private static Func<T> CreateFunc()
        {
            return Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();
        }

        private static readonly Func<T> Func = CreateFunc();
    }
}
