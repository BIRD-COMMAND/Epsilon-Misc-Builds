using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EpsilonLib.Dialogs;
using TagTool.Bitmaps;
using TagTool.Cache;
using TagTool.Commands.Models;
using TagTool.Commands.Sounds;
using TagTool.Geometry.Ass;
using TagTool.Geometry.Jms;
using TagTool.IO;
using TagTool.Tags.Definitions;

namespace CacheEditor.TagEditing
{
    [Export]
    class TagExtract
    {
        private IShell _shell;

        public TagExtract(IShell shell)
        {
            _shell = shell;
        }

        public void ExtractBitmap(GameCache cache, CachedTag tag)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                var result = dialog.ShowDialog();
                if (result != DialogResult.OK)
                    return;

                using (var stream = cache.OpenCacheRead())
                {
                    var bitmap = cache.Deserialize<Bitmap>(stream, tag);
                    ExtractBitmap(cache, tag, bitmap, dialog.SelectedPath);
                }
            }
        }

        public void ExportJMS(GameCache cache, CachedTag tag, string type)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                var result = dialog.ShowDialog();
                if (result != DialogResult.OK)
                    return;

                var filename = tag.Name.Split('\\').Last() + "_" + type + ".jms";
                var fullpath = Path.Combine(dialog.SelectedPath, tag.Name.Split('\\').Last(), filename);


                using (var stream = cache.OpenCacheRead())
                {
                    var hlmt = cache.Deserialize<Model>(stream, tag);
                    ExportJMSCommand exportJMSCommand = new ExportJMSCommand(cache, hlmt);
                    exportJMSCommand.Execute(new List<string>() { type, fullpath });
                }

                _shell.StatusBar.ShowStatusText($"JMS Exported to \"{fullpath}\"");
            }
        }

        public void ExtractAllModelJms(GameCache cache, CachedTag tag)
        {
            if (cache == null)
            {
                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = Alert.Error,
                    Message = "No cache is loaded.",
                    SubMessage = "Open a cache and try again."
                });
                return;
            }

            if (tag == null || !tag.IsInGroup("hlmt"))
            {
                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = Alert.Warning,
                    Message = "The selected tag is not a model (hlmt).",
                    SubMessage = "Select an hlmt tag to extract render/collision/physics JMS."
                });
                return;
            }

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select output folder for model JMS exports.",
                UseDescriptionForTitle = true,
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                using var progress = _shell.CreateProgressScope();
                progress.Report($"Extracting model JMS for '{tag}'...");

                Model model;
                using (var stream = cache.OpenCacheRead())
                    model = cache.Deserialize<Model>(stream, tag);

                if (model == null)
                    throw new InvalidDataException("Failed to deserialize model (hlmt) tag.");

                string modelOutputDirectory = Path.Combine(dialog.SelectedPath, GetTagLeafName(tag));
                Directory.CreateDirectory(modelOutputDirectory);

                var report = ModelJmsExportService.ExportAllModelJms(cache, tag, model, modelOutputDirectory);

                foreach (var item in report.Items)
                {
                    string reference = item.ReferencedTagPath ?? "<missing>";
                    string output = item.OutputPath ?? "<none>";
                    string dtoUsage = item.Attempted ? (item.UsedDtoPath ? "dto" : "legacy") : "n/a";
                    string status = item.Exported ? "exported" : (item.Skipped ? "skipped" : "failed");
                    string error = item.Exception?.Message ?? item.Message;

                    Console.WriteLine(
                        $"[Epsilon.ExtractAllModels] tag={item.SelectedModelTagPath} kind={GetModelKindLabel(item.Kind)} " +
                        $"reference={reference} output=\"{output}\" status={status} path={dtoUsage} message=\"{error}\"");
                }

                var render = report.Items.First(x => x.Kind == ModelJmsExportKind.Render);
                var collision = report.Items.First(x => x.Kind == ModelJmsExportKind.Collision);
                var physics = report.Items.First(x => x.Kind == ModelJmsExportKind.Physics);

                bool anyFailed = report.Items.Any(x => !x.Exported && !x.Skipped);
                bool anyExported = report.Items.Any(x => x.Exported);
                string outcome = anyFailed ? "completed with errors" : "completed";

                _shell.StatusBar.ShowStatusText($"Extract All Models {outcome} for '{report.SelectedModelTagPath}'.");

                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = anyFailed ? Alert.Warning : (anyExported ? Alert.Success : Alert.Warning),
                    Message = $"Extract All Models {outcome}.",
                    SubMessage =
                        $"Model: {report.SelectedModelTagPath}\n" +
                        $"Output folder: {report.OutputDirectory}\n\n" +
                        $"Render: {FormatModelExportStatus(render)}\n" +
                        $"Collision: {FormatModelExportStatus(collision)}\n" +
                        $"Physics: {FormatModelExportStatus(physics)}\n\n" +
                        $"Duration: {report.Duration.TotalMilliseconds:F0} ms"
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Epsilon.ExtractAllModels] Export failed for {tag}: {ex}");
                _shell.StatusBar.ShowStatusText("Extract All Models failed.");
                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = Alert.Error,
                    Message = $"Failed to extract all model JMS for '{tag}'.",
                    SubMessage = ex.Message
                });
            }
        }

        public void ExtractSound(GameCache cache, CachedTag tag)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                var result = dialog.ShowDialog();
                if (result != DialogResult.OK)
                    return;

                var fullpath = dialog.SelectedPath;

                using (var stream = cache.OpenCacheRead())
                {
                    var sound = cache.Deserialize<Sound>(stream, tag);

                    //if (sound.PitchRanges?[0].Permutations.Count > 1)
                    //    fullpath = Path.Combine(dialog.SelectedPath, tag.Name.Split('\\').Last());

                    //Directory.CreateDirectory(fullpath);

                    ExtractSoundCommand extractSoundCommand = new ExtractSoundCommand(cache, tag, sound);
                    extractSoundCommand.Execute(new List<string>() { fullpath });
                }

                _shell.StatusBar.ShowStatusText($"Sound files extracted to \"{fullpath}\"");
            }
        }

        public void ExportScenarioBspAss(GameCache cache, CachedTag tag)
        {
            if (cache == null)
            {
                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = Alert.Error,
                    Message = "No cache is loaded.",
                    SubMessage = "Open a cache and try again."
                });
                return;
            }

            if (tag == null || !tag.IsInGroup("sbsp"))
            {
                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = Alert.Warning,
                    Message = "The selected tag is not a scenario_structure_bsp.",
                    SubMessage = "Select an sbsp tag to export ASS."
                });
                return;
            }

            string defaultName = GetTagLeafName(tag);
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export ASS",
                FileName = defaultName,
                DefaultExt = ".ass",
                AddExtension = true,
                Filter = "ASS files (*.ass)|*.ass|All files (*.*)|*.*",
                OverwritePrompt = true
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                using var progress = _shell.CreateProgressScope();
                progress.Report($"Exporting ASS for '{tag}'...");

                ScenarioStructureBsp bsp;
                using (var stream = cache.OpenCacheRead())
                    bsp = cache.Deserialize<ScenarioStructureBsp>(stream, tag);

                if (bsp == null)
                    throw new InvalidDataException("Failed to deserialize scenario_structure_bsp.");

                var report = ScenarioBspAssExportService.ExportScenarioBspToAss(cache, tag, bsp, saveDialog.FileName);
                string weatherSkipped = report.WeatherPolyhedraSkipped ? "yes" : "no";

                Console.WriteLine(
                    $"[Epsilon.ExportASS] tag={report.TagPath} output=\"{report.OutputPath}\" " +
                    $"clusters={report.ClusterCount} portals={report.PortalCount} collision_surfaces={report.CollisionSurfaceCount} " +
                    $"instances={report.InstanceCount} materials={report.MaterialCount} objects={report.ObjectCount} " +
                    $"weather_skipped={weatherSkipped} duration_ms={report.Duration.TotalMilliseconds:F0}");

                _shell.StatusBar.ShowStatusText($"ASS exported to \"{report.OutputPath}\"");
                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = Alert.Success,
                    Message = "Scenario BSP exported successfully.",
                    SubMessage =
                        $"Tag: {report.TagPath}\n" +
                        $"Output: {report.OutputPath}\n" +
                        $"Clusters: {report.ClusterCount}, Portals: {report.PortalCount}, Collision surfaces: {report.CollisionSurfaceCount}\n" +
                        $"Instances: {report.InstanceCount}, Materials: {report.MaterialCount}, Objects: {report.ObjectCount}\n" +
                        $"Weather polyhedra skipped: {weatherSkipped}, Duration: {report.Duration.TotalMilliseconds:F0} ms"
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Epsilon.ExportASS] Export failed for {tag}: {ex}");
                _shell.StatusBar.ShowStatusText("ASS export failed.");
                _shell.ShowDialog(new AlertDialogViewModel
                {
                    AlertType = Alert.Error,
                    Message = $"Failed to export ASS for '{tag}'.",
                    SubMessage = ex.Message
                });
            }
        }

        private void ExtractBitmap(GameCache cache, CachedTag tag, Bitmap bitmap, string directory = "bitmaps")
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var ddsOutDir = directory;
            string name;
            if (tag.Name != null)
            {
                var split = tag.Name.Split('\\');
                name = split[split.Length - 1];
            }
            else
                name = tag.Index.ToString("X8");
            if (bitmap.Images.Count > 1)
            {
                ddsOutDir = Path.Combine(directory, name);
                Directory.CreateDirectory(ddsOutDir);
            }

            for (var i = 0; i < bitmap.Images.Count; i++)
            {
                var bitmapName = (bitmap.Images.Count > 1) ? i.ToString() : name;
                bitmapName += ".dds";
                var outPath = Path.Combine(ddsOutDir, bitmapName);

                var ddsFile = BitmapExtractor.ExtractBitmap(cache, bitmap, i, tag.Name);

                if (ddsFile == null)
                    throw new Exception("Invalid bitmap data");

                using (var fileStream = File.Open(outPath, FileMode.Create, FileAccess.Write))
                using (var writer = new EndianWriter(fileStream, EndianFormat.LittleEndian))
                {
                    ddsFile.Write(writer);
                }
            }

            _shell.StatusBar.ShowStatusText("Bitmap Extracted");
        }

        private static string GetTagLeafName(CachedTag tag)
        {
            if (tag?.Name == null)
                return tag?.Index.ToString("X8") ?? "scenario_bsp";

            string leaf = Path.GetFileName(tag.Name);
            return string.IsNullOrWhiteSpace(leaf) ? tag.Index.ToString("X8") : leaf;
        }

        private static string GetModelKindLabel(ModelJmsExportKind kind)
        {
            return kind switch
            {
                ModelJmsExportKind.Render => "render_model",
                ModelJmsExportKind.Collision => "collision_model",
                ModelJmsExportKind.Physics => "physics_model",
                _ => "model"
            };
        }

        private static string FormatModelExportStatus(ModelJmsExportItemReport report)
        {
            if (report.Exported)
            {
                string mode = report.Attempted ? (report.UsedDtoPath ? "new DTO path" : "legacy path") : "nodes-only";
                return $"exported ({mode})";
            }

            if (report.Skipped)
                return $"skipped, {report.Message}";

            return $"failed, {report.Exception?.Message ?? report.Message}";
        }
    }
}
