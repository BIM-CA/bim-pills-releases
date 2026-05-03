using System;
using System.Collections.Generic;
using System.IO;
using BIMPills.Core.Seleccionar;
using BIMPills.Infrastructure.Persistence;
using Xunit;

namespace BIMPills.Core.Tests.Seleccionar
{
    public class SelectionFilterTests
    {
        // ── FilterCondition.Evaluate ──────────────────────────────────────────

        [Theory]
        [InlineData("Muro", FilterOperator.Equals,    "Muro",    true)]
        [InlineData("muro", FilterOperator.Equals,    "Muro",    true)]   // case-insensitive
        [InlineData("Muro", FilterOperator.NotEquals, "Columna", true)]
        [InlineData("",     FilterOperator.IsEmpty,   "",        true)]
        [InlineData("X",    FilterOperator.IsEmpty,   "",        false)]
        [InlineData("",     FilterOperator.IsNotEmpty,"",        false)]
        [InlineData("X",    FilterOperator.IsNotEmpty,"",        true)]
        public void Condition_Evaluate_BasicOperators(string paramValue, FilterOperator op, string condValue, bool expected)
        {
            var cond = new FilterCondition { Operator = op, Value = condValue };
            Assert.Equal(expected, cond.Evaluate(paramValue));
        }

        [Theory]
        [InlineData("Muro Interior", FilterOperator.Contains,    "Interior", true)]
        [InlineData("Muro Interior", FilterOperator.NotContains, "Exterior", true)]
        [InlineData("Muro Interior", FilterOperator.StartsWith,  "Muro",     true)]
        [InlineData("Muro Interior", FilterOperator.EndsWith,    "Interior", true)]
        public void Condition_Evaluate_StringOperators(string paramValue, FilterOperator op, string condValue, bool expected)
        {
            var cond = new FilterCondition { Operator = op, Value = condValue };
            Assert.Equal(expected, cond.Evaluate(paramValue));
        }

        [Theory]
        [InlineData("10", FilterOperator.GreaterThan, "5",  true)]
        [InlineData("3",  FilterOperator.LessThan,    "5",  true)]
        [InlineData("5",  FilterOperator.GreaterThan, "10", false)]
        public void Condition_Evaluate_NumericOperators(string paramValue, FilterOperator op, string condValue, bool expected)
        {
            var cond = new FilterCondition { Operator = op, Value = condValue };
            Assert.Equal(expected, cond.Evaluate(paramValue));
        }

        [Fact]
        public void Condition_Evaluate_NullParam_OnlyIsEmptyMatches()
        {
            // null = parámetro no existe en el elemento → solo IsEmpty coincide
            var isEmpty = new FilterCondition { Operator = FilterOperator.IsEmpty };
            Assert.True(isEmpty.Evaluate(null));

            // Equals("") no debe coincidir con un parámetro inexistente (Fix Comment 5)
            var equals = new FilterCondition { Operator = FilterOperator.Equals, Value = "" };
            Assert.False(equals.Evaluate(null));

            // IsNotEmpty tampoco coincide
            var isNotEmpty = new FilterCondition { Operator = FilterOperator.IsNotEmpty };
            Assert.False(isNotEmpty.Evaluate(null));

            // Contains tampoco
            var contains = new FilterCondition { Operator = FilterOperator.Contains, Value = "" };
            Assert.False(contains.Evaluate(null));
        }

        // ── SelectionFilterConfig.Evaluate (AND) ──────────────────────────────

        [Fact]
        public void Filter_And_AllConditionsMet_ReturnsTrue()
        {
            var filter = new SelectionFilterConfig
            {
                Logic = FilterLogic.And,
                Conditions =
                {
                    new FilterCondition { ParameterName = "Categoria", Operator = FilterOperator.Equals,   Value = "Muros" },
                    new FilterCondition { ParameterName = "Nivel",     Operator = FilterOperator.Contains, Value = "N1" }
                }
            };

            var params_ = new Dictionary<string, string>
            {
                ["Categoria"] = "Muros",
                ["Nivel"]     = "N1 — Planta Baja"
            };

            Assert.True(filter.Evaluate(params_));
        }

        [Fact]
        public void Filter_And_OneConditionFails_ReturnsFalse()
        {
            var filter = new SelectionFilterConfig
            {
                Logic = FilterLogic.And,
                Conditions =
                {
                    new FilterCondition { ParameterName = "Categoria", Operator = FilterOperator.Equals, Value = "Muros" },
                    new FilterCondition { ParameterName = "Nivel",     Operator = FilterOperator.Equals, Value = "N2" }
                }
            };

            var params_ = new Dictionary<string, string>
            {
                ["Categoria"] = "Muros",
                ["Nivel"]     = "N1"
            };

            Assert.False(filter.Evaluate(params_));
        }

        // ── SelectionFilterConfig.Evaluate (OR) ───────────────────────────────

        [Fact]
        public void Filter_Or_OneConditionMet_ReturnsTrue()
        {
            var filter = new SelectionFilterConfig
            {
                Logic = FilterLogic.Or,
                Conditions =
                {
                    new FilterCondition { ParameterName = "Nivel", Operator = FilterOperator.Equals, Value = "N1" },
                    new FilterCondition { ParameterName = "Nivel", Operator = FilterOperator.Equals, Value = "N2" }
                }
            };

            var params_ = new Dictionary<string, string> { ["Nivel"] = "N2" };
            Assert.True(filter.Evaluate(params_));
        }

        [Fact]
        public void Filter_Or_NoConditionMet_ReturnsFalse()
        {
            var filter = new SelectionFilterConfig
            {
                Logic = FilterLogic.Or,
                Conditions =
                {
                    new FilterCondition { ParameterName = "Nivel", Operator = FilterOperator.Equals, Value = "N1" },
                    new FilterCondition { ParameterName = "Nivel", Operator = FilterOperator.Equals, Value = "N2" }
                }
            };

            var params_ = new Dictionary<string, string> { ["Nivel"] = "N3" };
            Assert.False(filter.Evaluate(params_));
        }

        [Fact]
        public void Filter_NoConditions_AlwaysTrue()
        {
            var filter = new SelectionFilterConfig { Logic = FilterLogic.And };
            Assert.True(filter.Evaluate(new Dictionary<string, string>()));
        }

        // ── JsonFilterPresetRepository ────────────────────────────────────────

        private static FilterPreset SamplePreset(string name = "Test") => new FilterPreset
        {
            Name = name,
            Filter = new SelectionFilterConfig
            {
                CategoryName = "Muros",
                Logic = FilterLogic.And,
                Conditions =
                {
                    new FilterCondition { ParameterName = "Nivel", Operator = FilterOperator.Contains, Value = "N1" }
                }
            }
        };

        [Fact]
        public void Repository_SaveAndLoad_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BPSelTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var repo = new JsonFilterPresetRepository(tempDir);
                var preset = SamplePreset("Filtro Muros N1");

                repo.Save(preset);
                var all = repo.LoadAll();

                Assert.Single(all);
                Assert.False(string.IsNullOrEmpty(all[0].Id));
                Assert.Equal("Filtro Muros N1", all[0].Name);
                Assert.Equal("Muros", all[0].Filter.CategoryName);
                Assert.Single(all[0].Filter.Conditions);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void Repository_Update_ModifiesExisting()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BPSelTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var repo = new JsonFilterPresetRepository(tempDir);
                var preset = SamplePreset("Original");
                repo.Save(preset);

                preset.Name = "Actualizado";
                repo.Save(preset);

                var all = repo.LoadAll();
                Assert.Single(all);
                Assert.Equal("Actualizado", all[0].Name);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void Repository_Delete_RemovesPreset()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BPSelTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var repo = new JsonFilterPresetRepository(tempDir);
                var preset = SamplePreset();
                repo.Save(preset);
                repo.Delete(preset.Id);

                Assert.Empty(repo.LoadAll());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
