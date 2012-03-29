using System;

namespace Monocle.Utils
{
    internal static class TypeHelper
    {
        internal static object ChangeType(object value, Type type)
        {
            if (value == DBNull.Value)
            {
                if (type == typeof(Guid))
                    return Guid.Empty;

                if (type == typeof(Int32))
                    return 0;

                if (type == typeof(Single))
                    return 0F;

                if (type == typeof(Boolean))
                    return false;

                if (type == typeof(DateTime))
                    return DateTime.MinValue;

                return null;
            }

            if (value == null)
            {
                if (type.IsGenericType)
                    return Activator.CreateInstance(type);

                return null;
            }

            if (type == value.GetType())
                return value;

            if (type.IsEnum)
            {
                if (value is string)
                    return Enum.Parse(type, value as string);

                return Enum.ToObject(type, value);
            }

            if (!type.IsInterface && type.IsGenericType)
            {
                var innerType = type.GetGenericArguments()[0];
                var innerValue = ChangeType(value, innerType);
                return Activator.CreateInstance(type, new[] { innerValue });
            }

            if (!(value is IConvertible))
                return value;

            return Convert.ChangeType(value, type);
        }
    }
}
