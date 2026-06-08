using Microsoft.Extensions.DependencyInjection;

namespace SearchConsoleApp.Core.Infrastructure.DependencyManagement;

/// <summary>
/// Marker interface'lerin yetmediği özel kayıtlar için.
/// Plugin'ler ve özel ihtiyaçlar için kullanılır.
/// </summary>
public interface IDependencyRegistrar
{
    /// <summary>Kayıt sırası — küçük olan önce çalışır.</summary>
    int Order { get; }

    void Register(IServiceCollection services, ITypeFinder typeFinder);
}

/// <summary>
/// Yüklü tüm assembly'lerdeki tipleri keşfetmek için.
/// </summary>
public interface ITypeFinder
{
    IEnumerable<Type> FindClassesOfType<T>(bool onlyConcreteClasses = true);
    IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, bool onlyConcreteClasses = true);
}
