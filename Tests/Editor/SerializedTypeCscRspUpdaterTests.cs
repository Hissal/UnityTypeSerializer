using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;

namespace Hissal.UnityTypeSerializer.Editor.Tests {
    internal sealed class SerializedTypeCscRspUpdaterTests {
        string temporaryDirectory = string.Empty;

        [SetUp]
        public void SetUp() {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "UnityTypeSerializerTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown() {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void TargetPathsRespectRootsExclusionsAndFolderBoundaries() {
            var assemblyDefinitionPaths = new[] {
                "Assets/A/First.asmdef",
                "Assets/A/Second.asmdef",
                "Assets/A/Sub/Third.asmdef",
                "Assets/A/B/Excluded.asmdef",
                "Assets/AB/Boundary.asmdef",
                "Packages/com.example/Package.asmdef",
            };

            var targets = SerializedTypeCscRspUpdater.GetTargetResponseFileAssetPaths(
                assemblyDefinitionPaths,
                new[] { "Assets/A", "Assets/A/Sub" },
                new[] { "Assets/A/B" });

            CollectionAssert.AreEqual(
                new[] {
                    "Assets/A/csc.rsp",
                    "Assets/A/Sub/csc.rsp",
                },
                targets);
        }

        [Test]
        public void EmptyRootsSelectNothing() {
            var targets = SerializedTypeCscRspUpdater.GetTargetResponseFileAssetPaths(
                new[] { "Assets/A/First.asmdef" },
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(targets, Is.Empty);
        }

        [Test]
        public void MissingResponseFileIsCreatedWithOnlyCanonicalDirective() {
            var responseFilePath = Path.Combine(temporaryDirectory, "csc.rsp");

            var status = SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFilePath);

            Assert.That(status, Is.EqualTo(SerializedTypeCscRspFileUpdateStatus.Created));
            Assert.That(
                File.ReadAllText(responseFilePath),
                Is.EqualTo(SerializedTypeCscRspUpdater.AdditionalFileDirective + Environment.NewLine));
        }

        [Test]
        public void EmptyResponseFileIsUpdatedWithoutAddingALeadingBlankLine() {
            var responseFilePath = Path.Combine(temporaryDirectory, "csc.rsp");
            File.WriteAllBytes(responseFilePath, Array.Empty<byte>());

            var status = SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFilePath);

            Assert.That(status, Is.EqualTo(SerializedTypeCscRspFileUpdateStatus.Updated));
            Assert.That(
                File.ReadAllText(responseFilePath),
                Is.EqualTo(SerializedTypeCscRspUpdater.AdditionalFileDirective + Environment.NewLine));
        }

        [Test]
        public void ExistingUtf8BomContentIsPreservedAndUpdateIsIdempotent() {
            var responseFilePath = Path.Combine(temporaryDirectory, "csc.rsp");
            File.WriteAllText(
                responseFilePath,
                "-langversion:10\r\n-nullable:enable",
                new UTF8Encoding(true));
            var originalBytes = File.ReadAllBytes(responseFilePath);

            var firstStatus = SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFilePath);
            var firstUpdateBytes = File.ReadAllBytes(responseFilePath);
            var secondStatus = SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFilePath);

            Assert.That(firstStatus, Is.EqualTo(SerializedTypeCscRspFileUpdateStatus.Updated));
            Assert.That(secondStatus, Is.EqualTo(SerializedTypeCscRspFileUpdateStatus.Unchanged));
            CollectionAssert.AreEqual(originalBytes, firstUpdateBytes.Take(originalBytes.Length).ToArray());
            CollectionAssert.AreEqual(firstUpdateBytes, File.ReadAllBytes(responseFilePath));
            Assert.That(
                File.ReadAllText(responseFilePath),
                Does.EndWith("\r\n" + SerializedTypeCscRspUpdater.AdditionalFileDirective + "\r\n"));
        }

        [Test]
        public void ExistingLfNewlineStyleIsPreserved() {
            var responseFilePath = Path.Combine(temporaryDirectory, "csc.rsp");
            File.WriteAllText(responseFilePath, "-langversion:10\n", new UTF8Encoding(false));

            SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFilePath);

            Assert.That(
                File.ReadAllText(responseFilePath),
                Is.EqualTo("-langversion:10\n" + SerializedTypeCscRspUpdater.AdditionalFileDirective + "\n"));
        }

        [Test]
        public void ExistingUtf16ContentIsAppendedWithoutChangingItsPrefix() {
            var responseFilePath = Path.Combine(temporaryDirectory, "csc.rsp");
            File.WriteAllText(
                responseFilePath,
                "-langversion:10\r\n",
                new UnicodeEncoding(false, true));
            var originalBytes = File.ReadAllBytes(responseFilePath);

            var status = SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFilePath);
            var updatedBytes = File.ReadAllBytes(responseFilePath);

            Assert.That(status, Is.EqualTo(SerializedTypeCscRspFileUpdateStatus.Updated));
            CollectionAssert.AreEqual(originalBytes, updatedBytes.Take(originalBytes.Length).ToArray());
            Assert.That(
                File.ReadAllText(responseFilePath),
                Does.EndWith(SerializedTypeCscRspUpdater.AdditionalFileDirective + "\r\n"));
        }

        [Test]
        public void EquivalentDirectiveFormsAreNotDuplicated() {
            var equivalentDirectives = new[] {
                SerializedTypeCscRspUpdater.AdditionalFileDirective,
                "/AdditionalFile : Library/Hissal/UnityTypeSerializer/SerializedTypeUsageManifest.xml",
                "-additionalfile:\"Library\\Hissal\\UnityTypeSerializer\\SerializedTypeUsageManifest.xml\"",
            };

            foreach (var directive in equivalentDirectives) {
                var responseFilePath = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N") + ".rsp");
                File.WriteAllText(responseFilePath, directive + "\n", new UTF8Encoding(false));
                var originalBytes = File.ReadAllBytes(responseFilePath);

                var status = SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFilePath);

                Assert.That(status, Is.EqualTo(SerializedTypeCscRspFileUpdateStatus.Unchanged));
                CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(responseFilePath));
            }
        }

        [Test]
        public void ResponseFileFailuresDoNotPreventOtherTargetsFromUpdating() {
            var successfulPath = Path.Combine(temporaryDirectory, "Good", "csc.rsp");
            Directory.CreateDirectory(Path.GetDirectoryName(successfulPath)!);

            var failingPath = Path.Combine(temporaryDirectory, "Bad", "csc.rsp");
            Directory.CreateDirectory(failingPath);

            var result = SerializedTypeCscRspUpdater.UpdateResponseFiles(new[] {
                failingPath,
                successfulPath,
            });

            Assert.That(result.CreatedCount, Is.EqualTo(1));
            Assert.That(result.FailedCount, Is.EqualTo(1));
            Assert.That(File.Exists(successfulPath), Is.True);
        }

        [Test]
        public void AutomationOnlyTreatsAsmdefAndCscRspAsRelevant() {
            Assert.That(SerializedTypeCscRspAssetPostprocessor.IsRelevantAssetPath("Assets/A/Test.asmdef"), Is.True);
            Assert.That(SerializedTypeCscRspAssetPostprocessor.IsRelevantAssetPath("Assets/A/csc.rsp"), Is.True);
            Assert.That(SerializedTypeCscRspAssetPostprocessor.IsRelevantAssetPath("Assets/A/Test.cs"), Is.False);
            Assert.That(SerializedTypeCscRspAssetPostprocessor.IsRelevantAssetPath("Assets/A/other.rsp"), Is.False);
        }

        [Test]
        public void CreatedResponseFileCanBeImportedWithAMetaFile() {
            var assetDirectoryPath = $"Assets/__UnityTypeSerializerCscRspTest_{Guid.NewGuid():N}";
            var diskDirectoryPath = Path.Combine(
                SerializedTypeUsageManifestPaths.ProjectRootPath,
                assetDirectoryPath);
            var responseFileDiskPath = Path.Combine(diskDirectoryPath, SerializedTypeCscRspUpdater.ResponseFileName);
            var responseFileAssetPath = $"{assetDirectoryPath}/{SerializedTypeCscRspUpdater.ResponseFileName}";

            try {
                Directory.CreateDirectory(diskDirectoryPath);
                SerializedTypeCscRspUpdater.EnsureResponseFileDirective(responseFileDiskPath);

                AssetDatabase.ImportAsset(responseFileAssetPath, ImportAssetOptions.ForceSynchronousImport);

                Assert.That(AssetDatabase.AssetPathToGUID(responseFileAssetPath), Is.Not.Empty);
                Assert.That(File.Exists(responseFileDiskPath + ".meta"), Is.True);
            }
            finally {
                AssetDatabase.DeleteAsset(assetDirectoryPath);
            }
        }
    }
}
