namespace SearchConsoleApp.Core.Infrastructure.DependencyManagement;

/// <summary>
/// Singleton ömürlü servisler için marker interface.
/// AppEngine başlangıçta tüm assembly'leri tarar ve bu interface'i implement
/// eden sınıfları otomatik DI'a kaydeder.
/// </summary>
public interface ISingletonService { }

/// <summary>
/// Scoped (request başına) ömürlü servisler için marker. Service katmanı varsayılanı.
/// </summary>
public interface IScopedService { }

/// <summary>
/// Transient (her çağrıda yeni instance) ömürlü servisler için marker.
/// </summary>
public interface ITransientService { }
