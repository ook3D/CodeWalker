using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CodeWalker.GameFiles
{
    // Evaluate once per closed generic type, outside the resource I/O loops.
    internal static class ResourceStructLayout<T> where T : struct
    {
        public static readonly bool CanCopyBytes =
            !RuntimeHelpers.IsReferenceOrContainsReferences<T>() &&
            HasDirectLayout(typeof(T)) &&
            Marshal.SizeOf<T>() == Unsafe.SizeOf<T>();

        private static bool HasDirectLayout(Type type)
        {
            // These types can have different managed and marshaled representations.
            if (type == typeof(bool) || type == typeof(char) || type == typeof(decimal)) return false;
            if (type.IsEnum) return HasDirectLayout(Enum.GetUnderlyingType(type));
            if (type.IsPrimitive || type == typeof(IntPtr) || type == typeof(UIntPtr)) return true;
            if (!type.IsValueType || (!type.IsLayoutSequential && !type.IsExplicitLayout)) return false;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsDefined(typeof(MarshalAsAttribute), false) || !HasDirectLayout(field.FieldType))
                    return false;
            }
            return true;
        }
    }
}
