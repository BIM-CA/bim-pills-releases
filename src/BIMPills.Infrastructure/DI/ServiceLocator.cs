using System;
using System.Collections.Generic;

namespace BIMPills.Infrastructure.DI
{
    /// <summary>
    /// Lightweight service locator. Sufficient for Revit plugins where
    /// there is no standard DI composition root.
    /// Call Reset() in tests to isolate registrations between test runs.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, Func<object>> _registrations
            = new Dictionary<Type, Func<object>>();

        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _registrations[typeof(T)] = () => instance;
        }

        public static void Register<T>(Func<T> factory) where T : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _registrations[typeof(T)] = () => factory()!;
        }

        public static T Get<T>() where T : class
        {
            if (_registrations.TryGetValue(typeof(T), out var factory))
                return (T)factory();

            throw new InvalidOperationException(
                $"Service '{typeof(T).FullName}' is not registered. " +
                "Register it in RevitApplication.OnStartup.");
        }

        public static bool IsRegistered<T>() => _registrations.ContainsKey(typeof(T));

        public static void Reset() => _registrations.Clear();
    }
}
