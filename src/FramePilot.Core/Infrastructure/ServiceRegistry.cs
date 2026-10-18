using System.Reflection;

namespace FramePilot.Core.Infrastructure;

/// <summary>
/// A very small dependency injection container. It supports constructor
/// injection, which is all FramePilot needs - the alternative would be a
/// third party container that has to be shipped with the application.
/// </summary>
public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, Registration> _registrations = new();
    private readonly Dictionary<Type, object> _instances = new();
    private readonly object _gate = new();

    /// <summary>Registers an existing instance as a singleton.</summary>
    public ServiceRegistry RegisterSingleton<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            _registrations[typeof(TService)] = new Registration(_ => instance, isSingleton: true);
            _instances[typeof(TService)] = instance;
        }

        return this;
    }

    /// <summary>
    /// Registers a type as a singleton. The instance is created by the
    /// container, so the constructor dependencies are injected.
    /// </summary>
    public ServiceRegistry RegisterSingleton<TService>()
        where TService : class
    {
        lock (_gate)
        {
            _registrations[typeof(TService)] = new Registration(
                r => r.CreateInstance(typeof(TService)),
                isSingleton: true);
        }

        return this;
    }

    /// <summary>Registers a factory as a singleton.</summary>
    public ServiceRegistry RegisterSingleton<TService>(Func<ServiceRegistry, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (_gate)
        {
            _registrations[typeof(TService)] = new Registration(r => factory(r), isSingleton: true);
        }

        return this;
    }

    /// <summary>Registers a factory that is called for every resolution.</summary>
    public ServiceRegistry RegisterTransient<TService>(Func<ServiceRegistry, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (_gate)
        {
            _registrations[typeof(TService)] = new Registration(r => factory(r), isSingleton: false);
        }

        return this;
    }

    /// <summary>Resolves a service.</summary>
    public TService Resolve<TService>() => (TService)Resolve(typeof(TService));

    /// <summary>Resolves a service.</summary>
    public object Resolve(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        lock (_gate)
        {
            return ResolveCore(serviceType);
        }
    }

    private object ResolveCore(Type serviceType)
    {
        if (_registrations.TryGetValue(serviceType, out var registration))
        {
            if (registration.IsSingleton && _instances.TryGetValue(serviceType, out var existing))
            {
                return existing;
            }

            var created = registration.Create(this);

            if (registration.IsSingleton)
            {
                _instances[serviceType] = created;
            }

            return created;
        }

        if (_instances.TryGetValue(serviceType, out var cached))
        {
            return cached;
        }

        var instance = CreateInstance(serviceType);
        _instances[serviceType] = instance;
        return instance;
    }

    private object CreateInstance(Type type)
    {
        var constructors = type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length);

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var arguments = new object?[parameters.Length];
            var resolved = true;

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                if (parameter.ParameterType == typeof(IServiceProvider))
                {
                    arguments[i] = this;
                    continue;
                }

                if (TryResolve(parameter.ParameterType, out var value))
                {
                    arguments[i] = value;
                    continue;
                }

                if (parameter.HasDefaultValue)
                {
                    arguments[i] = parameter.DefaultValue;
                    continue;
                }

                resolved = false;
                break;
            }

            if (resolved)
            {
                return constructor.Invoke(arguments);
            }
        }

        throw new InvalidOperationException(
            $"'{type.FullName}' could not be resolved. Register it explicitly or make sure that all constructor " +
            "parameters can be resolved.");
    }

    private bool TryResolve(Type type, out object? instance)
    {
        try
        {
            instance = ResolveCore(type);
            return true;
        }
        catch (Exception)
        {
            instance = null;
            return false;
        }
    }

    private sealed class Registration
    {
        public Registration(Func<ServiceRegistry, object> create, bool isSingleton)
        {
            Create = create;
            IsSingleton = isSingleton;
        }

        public Func<ServiceRegistry, object> Create { get; }

        public bool IsSingleton { get; }
    }
}
