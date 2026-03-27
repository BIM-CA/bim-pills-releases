using System;

namespace BIMPills.Core.Audit
{
    /// <summary>
    /// Puntuación de salud del modelo (0–100) basada en estándares internacionales BIM.
    /// Criterios alineados con ISO 19650-1:2018 (gestión de información BIM),
    /// Autodesk Model Performance Best Practices y BIM Forum LOD Specification.
    /// </summary>
    public sealed class ModelHealthScore
    {
        public int TotalScore { get; }
        public int WarningsScore { get; }
        public int FileSizeScore { get; }
        public int FamilySizeScore { get; }
        public int ElementsScore { get; }
        public int UnplacedViewsScore { get; }
        public int PurgeableScore { get; }
        public HealthLevel Level { get; }

        // Métricas brutas
        public int WarningsCount { get; }
        public double FileSizeMB { get; }
        public double LargestFamilyMB { get; }
        public int TotalElements { get; }
        public int UnplacedViewsCount { get; }
        public int PurgeableCount { get; }

        public ModelHealthScore(
            int warningsCount,
            double fileSizeMB,
            double largestFamilyMB,
            int totalElements,
            int unplacedViewsCount = 0,
            int purgeableCount = 0)
        {
            WarningsCount = warningsCount;
            FileSizeMB = fileSizeMB;
            LargestFamilyMB = largestFamilyMB;
            TotalElements = totalElements;
            UnplacedViewsCount = unplacedViewsCount;
            PurgeableCount = purgeableCount;

            // Advertencias (30 pts) — ISO 19650-1 §11.1: calidad de la información
            // Autodesk recomienda <100 para rendimiento óptimo
            WarningsScore = warningsCount <= 50 ? 30
                          : warningsCount <= 100 ? 24
                          : warningsCount <= 200 ? 18
                          : warningsCount <= 400 ? 10
                          : warningsCount <= 600 ? 5
                          : 0;

            // Tamaño de archivo (20 pts) — Colaboración eficiente (ISO 19650-2 §5.2.2)
            // Modelos >300MB impactan sincronización en entornos colaborativos
            FileSizeScore = fileSizeMB < 150 ? 20
                          : fileSizeMB < 300 ? 15
                          : fileSizeMB < 500 ? 10
                          : fileSizeMB < 1000 ? 5
                          : 0;

            // Familia más pesada (15 pts) — Gestión de contenido BIM
            // Familias >1MB degradan rendimiento de carga y memoria
            FamilySizeScore = largestFamilyMB < 0.5 ? 15
                            : largestFamilyMB < 1 ? 12
                            : largestFamilyMB < 2 ? 6
                            : largestFamilyMB < 5 ? 3
                            : 0;

            // Cantidad de elementos (10 pts) — Complejidad del modelo
            ElementsScore = totalElements < 300_000 ? 10
                          : totalElements < 500_000 ? 8
                          : totalElements < 1_000_000 ? 5
                          : totalElements < 2_000_000 ? 2
                          : 0;

            // Vistas sin colocar (10 pts) — Calidad de documentación
            // Vistas huérfanas consumen memoria y dificultan la navegación
            UnplacedViewsScore = unplacedViewsCount <= 5 ? 10
                               : unplacedViewsCount <= 15 ? 7
                               : unplacedViewsCount <= 30 ? 4
                               : unplacedViewsCount <= 50 ? 2
                               : 0;

            // Elementos purgables (15 pts) — Higiene del modelo (ISO 19650-1 §8.1)
            // Elementos sin uso incrementan tamaño sin aportar valor
            PurgeableScore = purgeableCount <= 10 ? 15
                           : purgeableCount <= 30 ? 11
                           : purgeableCount <= 60 ? 7
                           : purgeableCount <= 100 ? 3
                           : 0;

            TotalScore = Math.Min(100,
                WarningsScore + FileSizeScore + FamilySizeScore +
                ElementsScore + UnplacedViewsScore + PurgeableScore);

            Level = TotalScore >= 80 ? HealthLevel.Excelente
                  : TotalScore >= 60 ? HealthLevel.Bueno
                  : TotalScore >= 40 ? HealthLevel.Regular
                  : HealthLevel.Crítico;
        }

        public string LevelLabel => Level switch
        {
            HealthLevel.Excelente => "Excelente",
            HealthLevel.Bueno => "Bueno",
            HealthLevel.Regular => "Regular",
            HealthLevel.Crítico => "Cr\u00EDtico",
            _ => "Desconocido"
        };

        /// <summary>
        /// Retorna texto con la metodología de evaluación y referencias bibliográficas.
        /// </summary>
        public string GetMethodologyText()
        {
            return
                "METODOLOG\u00CDA DE EVALUACI\u00D3N DE SALUD DEL MODELO\n" +
                "=============================================\n\n" +
                "La puntuaci\u00F3n de salud (0\u2013100) eval\u00FAa 6 criterios ponderados:\n\n" +
                $"1. Advertencias: {WarningsScore}/30 pts\n" +
                $"   ({WarningsCount} advertencias detectadas)\n" +
                "   \u2022 \u226450: 30 pts  \u2022 \u2264100: 24 pts  \u2022 \u2264200: 18 pts\n" +
                "   \u2022 \u2264400: 10 pts  \u2022 \u2264600: 5 pts  \u2022 >600: 0 pts\n\n" +
                $"2. Tama\u00F1o del archivo: {FileSizeScore}/20 pts\n" +
                $"   ({FileSizeMB:F1} MB)\n" +
                "   \u2022 <150 MB: 20 pts  \u2022 <300 MB: 15 pts  \u2022 <500 MB: 10 pts\n" +
                "   \u2022 <1 GB: 5 pts  \u2022 \u22651 GB: 0 pts\n\n" +
                $"3. Familia m\u00E1s pesada: {FamilySizeScore}/15 pts\n" +
                $"   ({LargestFamilyMB:F2} MB)\n" +
                "   \u2022 <0.5 MB: 15 pts  \u2022 <1 MB: 12 pts  \u2022 <2 MB: 6 pts\n" +
                "   \u2022 <5 MB: 3 pts  \u2022 \u22655 MB: 0 pts\n\n" +
                $"4. Cantidad de elementos: {ElementsScore}/10 pts\n" +
                $"   ({TotalElements:N0} elementos)\n" +
                "   \u2022 <300K: 10 pts  \u2022 <500K: 8 pts  \u2022 <1M: 5 pts\n" +
                "   \u2022 <2M: 2 pts  \u2022 \u22652M: 0 pts\n\n" +
                $"5. Vistas sin colocar: {UnplacedViewsScore}/10 pts\n" +
                $"   ({UnplacedViewsCount} vistas)\n" +
                "   \u2022 \u22645: 10 pts  \u2022 \u226415: 7 pts  \u2022 \u226430: 4 pts\n" +
                "   \u2022 \u226450: 2 pts  \u2022 >50: 0 pts\n\n" +
                $"6. Elementos purgables: {PurgeableScore}/15 pts\n" +
                $"   ({PurgeableCount} elementos)\n" +
                "   \u2022 \u226410: 15 pts  \u2022 \u226430: 11 pts  \u2022 \u226460: 7 pts\n" +
                "   \u2022 \u2264100: 3 pts  \u2022 >100: 0 pts\n\n" +
                "NIVELES DE SALUD\n" +
                "\u2022 80\u2013100: Excelente  \u2022 60\u201379: Bueno\n" +
                "\u2022 40\u201359: Regular    \u2022 0\u201339: Cr\u00EDtico\n\n" +
                "REFERENCIAS\n" +
                "\u2022 ISO 19650-1:2018 \u2014 Conceptos y principios de\n" +
                "  gesti\u00F3n de informaci\u00F3n BIM (\u00A711.1, \u00A78.1)\n" +
                "\u2022 ISO 19650-2:2018 \u2014 Fase de desarrollo de activos\n" +
                "  (\u00A75.2.2 Intercambio de informaci\u00F3n)\n" +
                "\u2022 Autodesk AU IT20549 \u2014 Health Check for Your\n" +
                "  Revit Project Models (umbrales de advertencias)\n" +
                "\u2022 Autodesk Knowledge Network \u2014 Model Performance\n" +
                "  Best Practices & File Maintenance\n" +
                "\u2022 AEC (UK) BIM Protocol v2.0 \u2014 Model Validation\n" +
                "  Checklist (purga, vistas, limpieza de worksets)\n" +
                "\u2022 BIM Forum \u2014 LOD Specification (gesti\u00F3n de\n" +
                "  contenido y peso de familias)";
        }
    }

    public enum HealthLevel
    {
        Excelente,
        Bueno,
        Regular,
        Cr\u00EDtico
    }
}
