using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.Licensing;
using System;
using System.IO;
using Xunit;

namespace BIMPills.Core.Tests.Licensing
{
    public class LicenseServiceTests
    {
        [Fact]
        public void LicenseInfo_DefaultValues_AreEmpty()
        {
            var info = new LicenseInfo();

            Assert.Equal("", info.LicenseKey);
            Assert.Equal("", info.Software);
            Assert.Equal("", info.Plan);
            Assert.Equal("", info.Status);
            Assert.Null(info.ExpiresAt);
            Assert.Equal("", info.MachineId);
            Assert.Equal("", info.HolderName);
        }

        [Fact]
        public void LicenseCache_SaveAndLoad_Roundtrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                var license = new LicenseInfo
                {
                    LicenseKey = "TEST-KEY-123",
                    Software = "BIM PILLS",
                    Plan = "Pro Anual",
                    Status = "Activo",
                    ExpiresAt = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                    MachineId = "ABC123",
                    HolderName = "Test User",
                    ValidatedAt = DateTime.UtcNow
                };

                cache.Save(license);

                // Force re-read from disk
                var cache2 = new LicenseCache(tempDir);
                var loaded = cache2.Load();

                Assert.NotNull(loaded);
                Assert.Equal("TEST-KEY-123", loaded!.LicenseKey);
                Assert.Equal("Pro Anual", loaded.Plan);
                Assert.Equal("Activo", loaded.Status);
                Assert.Equal("Test User", loaded.HolderName);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LicenseCache_IsCacheFresh_TrueWhenRecent()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                cache.Save(new LicenseInfo
                {
                    LicenseKey = "KEY",
                    Status = "Activo",
                    ValidatedAt = DateTime.UtcNow
                });

                Assert.True(cache.IsCacheFresh());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LicenseCache_IsCacheFresh_FalseWhenOld()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                cache.Save(new LicenseInfo
                {
                    LicenseKey = "KEY",
                    Status = "Activo",
                    ValidatedAt = DateTime.UtcNow.AddHours(-25)
                });

                Assert.False(cache.IsCacheFresh());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LicenseCache_Clear_RemovesData()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                cache.Save(new LicenseInfo { LicenseKey = "KEY", Status = "Activo", ValidatedAt = DateTime.UtcNow });

                cache.Clear();

                Assert.Null(cache.Load());
                Assert.False(cache.IsCacheFresh());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LicenseCache_Load_ReturnsNullForMissingFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            var cache = new LicenseCache(tempDir);

            Assert.Null(cache.Load());

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        [Fact]
        public void AirtableLicenseService_IsValid_FalseWithNoCache()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                var service = new AirtableLicenseService("fake-key", cache);

                // No cache = never activated, not "expired"
                Assert.False(service.IsValid);
                Assert.False(service.IsActivated);
                Assert.False(service.IsExpired);
                Assert.False(service.IsGracePeriod);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AirtableLicenseService_IsValid_TrueWithActiveLicense()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                cache.Save(new LicenseInfo
                {
                    LicenseKey = "KEY",
                    Status = "Activo",
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    ValidatedAt = DateTime.UtcNow
                });

                var service = new AirtableLicenseService("fake-key", cache);

                Assert.True(service.IsValid);
                Assert.False(service.IsExpired);
                Assert.False(service.IsGracePeriod);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AirtableLicenseService_IsGracePeriod_TrueWithinSevenDays()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                cache.Save(new LicenseInfo
                {
                    LicenseKey = "KEY",
                    Status = "Expirado",
                    ExpiresAt = DateTime.UtcNow.AddDays(-3), // Expired 3 days ago
                    ValidatedAt = DateTime.UtcNow
                });

                var service = new AirtableLicenseService("fake-key", cache);

                Assert.True(service.IsValid);      // Still valid (grace period)
                Assert.False(service.IsExpired);
                Assert.True(service.IsGracePeriod);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AirtableLicenseService_IsExpired_TrueAfterGracePeriod()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BIMPills_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LicenseCache(tempDir);
                cache.Save(new LicenseInfo
                {
                    LicenseKey = "KEY",
                    Status = "Expirado",
                    ExpiresAt = DateTime.UtcNow.AddDays(-10), // Expired 10 days ago
                    ValidatedAt = DateTime.UtcNow
                });

                var service = new AirtableLicenseService("fake-key", cache);

                Assert.False(service.IsValid);
                Assert.True(service.IsExpired);
                Assert.False(service.IsGracePeriod);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void MachineIdProvider_GeneratesDeterministicId()
        {
            var id1 = MachineIdProvider.GetMachineId();
            var id2 = MachineIdProvider.GetMachineId();

            Assert.Equal(id1, id2);
            Assert.Equal(32, id1.Length); // SHA-256 truncated to 32 hex chars
        }
    }
}
