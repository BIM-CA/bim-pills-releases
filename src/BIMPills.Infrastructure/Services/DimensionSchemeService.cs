using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.Persistence;

namespace BIMPills.Infrastructure.Services
{
    /// <summary>
    /// Service for managing custom dimension schemes.
    /// Delegates persistence to <see cref="JsonSchemeRepository"/> and adds validation logic.
    /// </summary>
    public class DimensionSchemeService : IDimensionSchemeService
    {
        private readonly JsonSchemeRepository _repository;

        public DimensionSchemeService(JsonSchemeRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<List<CustomDimensionScheme>> GetAllSchemesAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<CustomDimensionScheme?> GetSchemeByIdAsync(string schemeId)
        {
            if (string.IsNullOrWhiteSpace(schemeId))
                throw new ArgumentException("Scheme ID cannot be null or empty.", nameof(schemeId));

            return await _repository.GetByIdAsync(schemeId);
        }

        public async Task<string> CreateSchemeAsync(CustomDimensionScheme scheme)
        {
            if (scheme == null)
                throw new ArgumentNullException(nameof(scheme));

            var validation = await ValidateSchemeAsync(scheme);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    $"Scheme validation failed: {string.Join("; ", validation.Errors)}");

            return await _repository.CreateAsync(scheme);
        }

        public async Task UpdateSchemeAsync(CustomDimensionScheme scheme)
        {
            if (scheme == null)
                throw new ArgumentNullException(nameof(scheme));

            var validation = await ValidateSchemeAsync(scheme);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    $"Scheme validation failed: {string.Join("; ", validation.Errors)}");

            await _repository.UpdateAsync(scheme);
        }

        public async Task DeleteSchemeAsync(string schemeId)
        {
            if (string.IsNullOrWhiteSpace(schemeId))
                throw new ArgumentException("Scheme ID cannot be null or empty.", nameof(schemeId));

            await _repository.DeleteAsync(schemeId);
        }

        public async Task<ValidationResult> ValidateSchemeAsync(CustomDimensionScheme scheme)
        {
            var result = new ValidationResult { IsValid = true };

            if (scheme == null)
            {
                result.IsValid = false;
                result.Errors.Add("El esquema no puede ser nulo.");
                return result;
            }

            // Name must not be empty
            if (string.IsNullOrWhiteSpace(scheme.Name))
            {
                result.IsValid = false;
                result.Errors.Add("El nombre del esquema es obligatorio.");
            }

            // Must have at least 1 rule
            if (scheme.Rules == null || scheme.Rules.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("El esquema debe tener al menos una regla.");
            }

            // No duplicate names among existing schemes (exclude self when updating)
            var existingSchemes = await _repository.GetAllAsync();
            var duplicate = existingSchemes.Find(s =>
                s.Name != null &&
                s.Name.Equals(scheme.Name, StringComparison.OrdinalIgnoreCase) &&
                s.Id != scheme.Id);

            if (duplicate != null)
            {
                result.IsValid = false;
                result.Errors.Add($"Ya existe un esquema con el nombre '{scheme.Name}'.");
            }

            return result;
        }
    }
}
