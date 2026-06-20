using System.Collections.Generic;
using System.Threading.Tasks;
using KusinaFlows.Models;

namespace KusinaFlows.Repositories
{
    public interface IStaffRepository
    {
        Task<List<StaffDto>> GetAllAsync();
        Task<StaffDto?> FindByUsernameAsync(string username);
        Task<bool> UsernameTakenAsync(string username);
        Task<string?> GetPositionAsync(int scId);
        Task<int> CreateAsync(StaffDto payload, string hashedPassword);
        Task<bool> UpdateAsync(int scId, StaffDto payload, string? hashedPassword);
        Task UpdatePasswordAsync(int scId, string hashedPassword);
        Task<bool> UpdateLastLoginAsync(string username, string timestamp);
    }
}
