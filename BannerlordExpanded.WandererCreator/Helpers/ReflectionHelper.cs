using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace BannerlordExpanded.WandererCreator.Helpers
{
    /// <summary>
    /// Centralized reflection helper with caching and multiple field name fallbacks
    /// for improved game version compatibility.
    /// </summary>
    public static class ReflectionHelper
    {
        // Cache for reflection lookups
        private static readonly ConcurrentDictionary<string, FieldInfo?> FieldCache = new();
        private static readonly ConcurrentDictionary<string, PropertyInfo?> PropertyCache = new();
        private static readonly ConcurrentDictionary<string, MethodInfo?> MethodCache = new();

        /// <summary>
        /// Tries to get a field value, checking multiple possible field names.
        /// </summary>
        public static bool TryGetField<T>(object target, string[] fieldNames, out T? value)
        {
            value = default;
            if (target == null) return false;

            var type = target.GetType();
            foreach (var fieldName in fieldNames)
            {
                var cacheKey = $"{type.FullName}.{fieldName}";

                if (!FieldCache.TryGetValue(cacheKey, out var field))
                {
                    field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    FieldCache[cacheKey] = field;
                }

                if (field != null)
                {
                    try
                    {
                        value = (T?)field.GetValue(target);
                        return true;
                    }
                    catch { continue; }
                }
            }

            FileLogger.Log($"[ReflectionHelper] Failed to get field. Tried: {string.Join(", ", fieldNames)} on {type.Name}");
            return false;
        }

        /// <summary>
        /// Tries to set a field value, checking multiple possible field names.
        /// </summary>
        public static bool TrySetField<T>(object target, string[] fieldNames, T value)
        {
            if (target == null) return false;

            var type = target.GetType();
            foreach (var fieldName in fieldNames)
            {
                var cacheKey = $"{type.FullName}.{fieldName}";

                if (!FieldCache.TryGetValue(cacheKey, out var field))
                {
                    field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    FieldCache[cacheKey] = field;
                }

                if (field != null)
                {
                    try
                    {
                        field.SetValue(target, value);
                        return true;
                    }
                    catch { continue; }
                }
            }

            FileLogger.Log($"[ReflectionHelper] Failed to set field. Tried: {string.Join(", ", fieldNames)} on {type.Name}");
            return false;
        }

        /// <summary>
        /// Tries to get a property value with fallback names.
        /// Uses GetProperties + LINQ to avoid AmbiguousMatchException for overridden properties.
        /// </summary>
        public static bool TryGetProperty<T>(object target, string[] propertyNames, out T? value)
        {
            value = default;
            if (target == null) return false;

            var type = target.GetType();
            foreach (var propName in propertyNames)
            {
                var cacheKey = $"{type.FullName}.{propName}";

                if (!PropertyCache.TryGetValue(cacheKey, out var prop))
                {
                    // Use GetProperties + FirstOrDefault to avoid AmbiguousMatchException
                    prop = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                               .FirstOrDefault(p => p.Name == propName);
                    PropertyCache[cacheKey] = prop;
                }

                if (prop != null && prop.CanRead)
                {
                    try
                    {
                        value = (T?)prop.GetValue(target);
                        return true;
                    }
                    catch { continue; }
                }
            }

            FileLogger.Log($"[ReflectionHelper] Failed to get property. Tried: {string.Join(", ", propertyNames)} on {type.Name}");
            return false;
        }

        /// <summary>
        /// Tries to set a property value with fallback names.
        /// Uses GetProperties + LINQ to avoid AmbiguousMatchException for overridden properties.
        /// </summary>
        public static bool TrySetProperty<T>(object target, string[] propertyNames, T value)
        {
            if (target == null) return false;

            var type = target.GetType();
            foreach (var propName in propertyNames)
            {
                var cacheKey = $"{type.FullName}.{propName}";

                if (!PropertyCache.TryGetValue(cacheKey, out var prop))
                {
                    // Use GetProperties + FirstOrDefault to avoid AmbiguousMatchException
                    prop = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                               .FirstOrDefault(p => p.Name == propName);
                    PropertyCache[cacheKey] = prop;
                }

                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(target, value);
                        return true;
                    }
                    catch { continue; }
                }
            }

            FileLogger.Log($"[ReflectionHelper] Failed to set property. Tried: {string.Join(", ", propertyNames)} on {type.Name}");
            return false;
        }

        /// <summary>
        /// Tries to invoke a method with fallback names.
        /// </summary>
        public static bool TryInvokeMethod(object target, string[] methodNames, out object? result, params object?[] args)
        {
            result = null;
            if (target == null) return false;

            var type = target.GetType();
            foreach (var methodName in methodNames)
            {
                var cacheKey = $"{type.FullName}.{methodName}";

                if (!MethodCache.TryGetValue(cacheKey, out var method))
                {
                    method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodCache[cacheKey] = method;
                }

                if (method != null)
                {
                    try
                    {
                        result = method.Invoke(target, args);
                        return true;
                    }
                    catch { continue; }
                }
            }

            FileLogger.Log($"[ReflectionHelper] Failed to invoke method. Tried: {string.Join(", ", methodNames)} on {type.Name}");
            return false;
        }

        /// <summary>
        /// Gets a static field from a type.
        /// </summary>
        public static bool TryGetStaticField<T>(Type type, string[] fieldNames, out T? value)
        {
            value = default;

            foreach (var fieldName in fieldNames)
            {
                var cacheKey = $"static:{type.FullName}.{fieldName}";

                if (!FieldCache.TryGetValue(cacheKey, out var field))
                {
                    field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    FieldCache[cacheKey] = field;
                }

                if (field != null)
                {
                    try
                    {
                        value = (T?)field.GetValue(null);
                        return true;
                    }
                    catch { continue; }
                }
            }

            FileLogger.Log($"[ReflectionHelper] Failed to get static field. Tried: {string.Join(", ", fieldNames)} on {type.Name}");
            return false;
        }
    }
}
