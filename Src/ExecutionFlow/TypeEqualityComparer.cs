using System;
using System.Collections.Generic;

namespace ExecutionFlow
{
    internal class TypeEqualityComparer : EqualityComparer<Type>
    {
        public override bool Equals(Type x, Type y)
        {
            if (x == y) return true;
            if (x == null || y == null)
                return false;

            return x.FullName.Equals(y.FullName);
        }

        public override int GetHashCode(Type obj)
            => obj?.FullName?.GetHashCode() ?? 0;
    }
}
