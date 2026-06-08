using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.Events;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Web.Framework.Infrastructure;

/// <summary>
/// Tek bir extension method: tüm marker interface'leri tarar ve DI'a kaydeder.
///
/// Program.cs'te:
///   builder.Services.AddSearchConsoleAppServices(typeof(CustomerService).Assembly, typeof(JwtIssuer).Assembly);
///
/// Tarama sırası ÖNEMLİ:
/// - IDependencyRegistrar implementasyonları önce çalışır (Order'a göre)
/// - Sonra marker interface'ler (IScopedService, vb.) toplu kayıt
/// - Sonra IConsumer<T> implementasyonları
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSearchConsoleAppServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            throw new ArgumentException("En az bir assembly gerekli.", nameof(assemblies));

        // 1. IDependencyRegistrar implementasyonları
        RegisterDependencyRegistrars(services, assemblies);

        // 2. Marker interface'ler (Scrutor ile)
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(c => c.AssignableTo<ISingletonService>())
                .AsImplementedInterfaces()
                .WithSingletonLifetime()
            .AddClasses(c => c.AssignableTo<IScopedService>())
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo<ITransientService>())
                .AsImplementedInterfaces()
                .WithTransientLifetime()
        );

        // 3. IConsumer<T> implementasyonları — auto-register
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(c => c.AssignableTo(typeof(IConsumer<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
        );

        return services;
    }

    private static void RegisterDependencyRegistrars(IServiceCollection services, Assembly[] assemblies)
    {
        var registrarType = typeof(IDependencyRegistrar);
        var registrarTypes = assemblies
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false } && registrarType.IsAssignableFrom(t));

        var registrars = registrarTypes
            .Select(t => (IDependencyRegistrar)Activator.CreateInstance(t)!)
            .OrderBy(r => r.Order)
            .ToList();

        var typeFinder = new AssemblyTypeFinder(assemblies);
        foreach (var registrar in registrars)
            registrar.Register(services, typeFinder);
    }
}

/// <summary>ITypeFinder somut impl, IDependencyRegistrar'a verilir.</summary>
internal class AssemblyTypeFinder : ITypeFinder
{
    private readonly Assembly[] _assemblies;
    public AssemblyTypeFinder(Assembly[] assemblies) => _assemblies = assemblies;

    public IEnumerable<Type> FindClassesOfType<T>(bool onlyConcreteClasses = true)
        => FindClassesOfType(typeof(T), onlyConcreteClasses);

    public IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, bool onlyConcreteClasses = true)
    {
        return _assemblies
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
            })
            .Where(t =>
                t != null &&
                assignTypeFrom.IsAssignableFrom(t) &&
                (!onlyConcreteClasses || (t.IsClass && !t.IsAbstract)));
    }
}
