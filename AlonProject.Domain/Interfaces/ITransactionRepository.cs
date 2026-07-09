using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(int id);
    Task<IEnumerable<Transaction>> GetByItemIdAsync(int itemId);
    Task<IEnumerable<Transaction>> GetAllAsync();
    Task<Transaction> CreateAsync(Transaction entity);
}
