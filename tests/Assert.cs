using System;

namespace TVCleaner.Tests
{
    public static class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(string.Format("ожидалось <{0}>, получено <{1}>", expected, actual));
        }

        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Throws<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new Exception("ожидалось исключение " + typeof(TException).Name);
        }
    }
}
