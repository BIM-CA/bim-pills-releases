using System.Collections.Generic;

namespace BIMPills.Core.Seleccionar
{
    public interface IFilterPresetRepository
    {
        IReadOnlyList<FilterPreset> LoadAll();
        void Save(FilterPreset preset);
        void Delete(string id);
    }
}
