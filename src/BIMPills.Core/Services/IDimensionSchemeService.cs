using BIMPills.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BIMPills.Core.Services
{
    /// <summary>
    /// Service for managing custom dimension schemes.
    /// Handles CRUD operations and validation.
    /// </summary>
    public interface IDimensionSchemeService
    {
        Task<List<CustomDimensionScheme>> GetAllSchemesAsync();
        Task<CustomDimensionScheme?> GetSchemeByIdAsync(string schemeId);
        Task<string> CreateSchemeAsync(CustomDimensionScheme scheme);
        Task UpdateSchemeAsync(CustomDimensionScheme scheme);
        Task DeleteSchemeAsync(string schemeId);
        Task<ValidationResult> ValidateSchemeAsync(CustomDimensionScheme scheme);
    }

    /// <summary>
    /// Result of a validation operation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
