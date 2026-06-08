using SearchConsoleApp.Core.Domain.Customers;

namespace SearchConsoleApp.Services.Customers;

/// <summary>
/// ÖRNEK service interface. Tüm method'lar Async suffix'li.
/// </summary>
public interface ICustomerService
{
    Task<Customer?> GetCustomerByIdAsync(long customerId);
    Task<Customer?> GetCustomerByEntityIdAsync(Guid entityId);
    Task<Customer?> GetCustomerByEmailAsync(string email);
    Task<IList<Customer>> GetAllCustomersAsync(bool onlyActive = true);
    Task InsertCustomerAsync(Customer customer);
    Task UpdateCustomerAsync(Customer customer);
    Task DeleteCustomerAsync(Customer customer);
}
