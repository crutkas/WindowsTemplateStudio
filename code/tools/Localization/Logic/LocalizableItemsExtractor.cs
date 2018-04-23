﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Localization.Extensions;
using Localization.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Localization
{
    internal class LocalizableItemsExtractor
    {

        private RoutesManager _routesManager;
        private ValidateLocalizableExtractor _validator;
        private IEnumerable<string> _cultures;

        internal LocalizableItemsExtractor(string sourceDir, string destDir, ValidateLocalizableExtractor validator, IEnumerable<string> cultures)
        {
            _routesManager = new RoutesManager(sourceDir, destDir);
            _cultures = cultures;
            _validator = validator;
        }

        internal void ExtractVsix()
        {
            if (!_validator.HasVsixChanges())
            {
                 return;
            }

            FileInfo manifiestFile = _routesManager.GetFileFromSource(Routes.VsixRootDirPath, Routes.VsixManifestFile);
            var (name, description) = RoutesExtensions.GetVsixValues(manifiestFile.FullName);

            foreach (var culture in _cultures)
            {
                var vsixDesDirectory = _routesManager.GetOrCreateDestDirectory(Path.Combine(Routes.VsixRootDirPath, culture));
                var langpackFile = new FileInfo(Path.Combine(vsixDesDirectory.FullName, Routes.VsixLangpackFile));

                using (TextWriter writer = langpackFile.CreateText())
                {
                    writer.Write(string.Format(Routes.VsixLangpackContent, name, description, culture));
                }
            }
        }

        internal void ExtractProjectTemplates()
        {
            if (_validator.HasChanges(Routes.ProjectTemplateFileNameValidateCS))
            {
                ExtractProjectTemplatesByLanguage(
                    Routes.ProjectTemplatePathCS,
                    Routes.ProjectTemplateFileCS,
                    Routes.ProjectTemplateFileNamePatternCS);
            }

            if (_validator.HasChanges(Routes.ProjectTemplateFileNameValidateVB))
            {
                ExtractProjectTemplatesByLanguage(
                    Routes.ProjectTemplatePathVB,
                    Routes.ProjectTemplateFileVB,
                    Routes.ProjectTemplateFileNamePatternVB);
            }
        }

        private void ExtractProjectTemplatesByLanguage(string projectTemplatePath, string projectTemplateFile, string projectTemplateFileNamePattern)
        {
            FileInfo srcFile = _routesManager.GetFileFromSource(Path.Combine(projectTemplatePath, projectTemplateFile));
            var desDirectory = _routesManager.GetOrCreateDestDirectory(projectTemplatePath);

            foreach (string culture in _cultures)
            {
                string desFile = Path.Combine(desDirectory.FullName, string.Format(projectTemplateFileNamePattern, culture));
                srcFile.CopyTo(desFile, true);
            }
        }

        internal void ExtractCommandTemplates()
        {
            if (_validator.HasChanges(Routes.RelayCommandFileNameValidate))
            {
                LocalizeFileType(Routes.RelayCommandFileNameValidate, Routes.RelayCommandFileNamePattern);
            }

            if (_validator.HasChanges(Routes.VspackageFileNameValidate))
            {
                LocalizeFileType(Routes.VspackageFileNameValidate, Routes.VspackageFileNamePattern);
            }
        }

        private void LocalizeFileType(string filePath, string searchPattern)
        {
            FileInfo file = _routesManager.GetFileFromSource(filePath);
            DirectoryInfo desDirectory = _routesManager.GetOrCreateDestDirectory(Routes.CommandTemplateRootDirPath);

            foreach (string culture in _cultures)
            {
                file.CopyTo(Path.Combine(desDirectory.FullName, string.Format(searchPattern, culture)), true);
            }
        }

        internal void ExtractTemplatePages()
        {
            ExtractTemplateEngineTemplates(Routes.TemplatesPagesPatternPath);
        }

        internal void ExtractTemplateFeatures()
        {
            ExtractTemplateEngineTemplates(Routes.TemplatesFeaturesPatternPath);
        }

        private void ExtractTemplateEngineTemplates(string patternPath)
        {
            var srcDirectory =_routesManager.GetDirectoryFromSource(Routes.TemplatesRootDirPath);
            var directories = srcDirectory.GetDirectories(patternPath, SearchOption.AllDirectories);
            var templatesDirectories = directories.SelectMany(d => d.GetDirectories().Where(c => !c.Name.EndsWith("VB")));

            foreach (var directory in templatesDirectories)
            {
                var path = GetTemplateRelativePath(directory);
                string pageSrcDirectory = Path.Combine(path, Routes.TemplateConfigDir);

                ExtractTemplateJson(pageSrcDirectory);
                ExtractTemplateDescription(pageSrcDirectory);
            }
        }

        private void ExtractTemplateJson(string srcDirectory)
        {
            string fileJson = Path.Combine(srcDirectory, Routes.TemplateJsonFile);
            FileInfo file = _routesManager.GetFileFromSource(fileJson);

            if (file.Exists && _validator.HasChanges(fileJson))
            {
                var desDirectory = _routesManager.GetOrCreateDestDirectory(srcDirectory);
                var metadata = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(file.FullName));

                foreach (string culture in _cultures)
                {
                    string filePath = Path.Combine(desDirectory.FullName, culture + "." + Routes.TemplateJsonFile);
                    var con = new
                    {
                        author = metadata.GetValue("author", StringComparison.Ordinal).Value<string>(),
                        name = metadata.GetValue("name", StringComparison.Ordinal).Value<string>(),
                        description = metadata.GetValue("description", StringComparison.Ordinal).Value<string>(),
                        identity = metadata.GetValue("identity", StringComparison.Ordinal).Value<string>()
                    };
                    string content = JsonConvert.SerializeObject(con, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(filePath, content, Encoding.UTF8);
                }
            }
        }

        private void ExtractTemplateDescription(string srcDirectory)
        {
            string fileMd = Path.Combine(srcDirectory, Routes.TemplateDescriptionFile);
            FileInfo file = _routesManager.GetFileFromSource(fileMd);

            if (file.Exists && _validator.HasChanges(fileMd))
            {
                var desDirectory = _routesManager.GetOrCreateDestDirectory(srcDirectory);

                foreach (string culture in _cultures)
                {
                    FileInfo desFile = new FileInfo(Path.Combine(desDirectory.FullName, culture + "." + Routes.TemplateDescriptionFile));
                    file.CopyTo(desFile.FullName, true);
                }
            }
        }

        internal void ExtractWtsProjectTypes()
        {
            if (_validator.HasChanges(Routes.WtsProjectTypesValidate))
            {
                ExtractWtsTemplateFiles(Routes.WtsProjectTypes);
            }

            ExtractWtsTemplateSubfolderFiles(Routes.WtsProjectTypes);
        }

        internal void ExtractWtsFrameworks()
        {
            if (_validator.HasChanges(Routes.WtsFrameworksValidate))
            {
                ExtractWtsTemplateFiles(Routes.WtsFrameworks);
            }

            ExtractWtsTemplateSubfolderFiles(Routes.WtsFrameworks);
        }

        private void ExtractWtsTemplateFiles(string routeType)
        {
            var desDirectory = _routesManager.GetOrCreateDestDirectory(Routes.WtsTemplatesRootDirPath);
            var srcFile = _routesManager.GetFileFromSource(Routes.WtsTemplatesRootDirPath, routeType + ".json");

            var fileContent = File.ReadAllText(srcFile.FullName);
            var content = JsonConvert.DeserializeObject<List<JObject>>(fileContent);
            var projects = content.Select(json => new
            {
                name = json.GetValue("name", StringComparison.Ordinal).Value<string>(),
                displayName = json.GetValue("displayName", StringComparison.Ordinal).Value<string>(),
                summary = json.GetValue("summary", StringComparison.Ordinal).Value<string>()
            });

            var data = JsonConvert.SerializeObject(projects, Newtonsoft.Json.Formatting.Indented);

            foreach (string culture in _cultures)
            {
                var desFile = Path.Combine(desDirectory.FullName, culture + "." + srcFile.Name);
                File.WriteAllText(desFile, data, Encoding.UTF8);
            }
        }

        private void ExtractWtsTemplateSubfolderFiles(string routeType)
        {
            var srcFile = _routesManager.GetFileFromSource(Routes.WtsTemplatesRootDirPath, routeType + ".json");
            var fileContent = File.ReadAllText(srcFile.FullName);
            var content = JsonConvert.DeserializeObject<List<JObject>>(fileContent);
            var projectNames = content.Select(json => json.GetValue("name", StringComparison.Ordinal).Value<string>());

            foreach (var name in projectNames)
            {
                var mdFilePath = Path.Combine(Routes.WtsTemplatesRootDirPath, routeType, name + ".md");
                if (_validator.HasChanges(mdFilePath))
                {
                    var mdFile = _routesManager.GetFileFromSource(mdFilePath);
                    var desDirectory = _routesManager.GetOrCreateDestDirectory(Path.Combine(Routes.WtsTemplatesRootDirPath, routeType));

                    foreach (string culture in _cultures)
                    {
                        mdFile.CopyTo(Path.Combine(desDirectory.FullName, culture + "." + name + ".md"), true);
                    }
                }
            }
        }

        internal void ExtractResourceFiles()
        {
            foreach (string directory in Routes.ResoureceDirectories)
            {
                var srcResFile = Path.Combine(directory, Routes.ResourcesFilePath);
                if (_validator.HasChanges(srcResFile))
                {
                    FileInfo resourceFile = _routesManager.GetFileFromSource(srcResFile);
                    var desDirectory = _routesManager.GetOrCreateDestDirectory(directory);

                    foreach (string culture in _cultures)
                    {
                        string destResFile = Path.Combine(desDirectory.FullName, string.Format(Routes.ResourcesFilePathPattern, culture));
                        resourceFile.CopyTo(destResFile, true);
                    }
                }
            }
        }

        private string GetTemplateRelativePath(DirectoryInfo directory)
        {
            if (directory.Name == Routes.TemplatesRootDirPath)
            {
                return directory.Name;
            }

            var path = GetTemplateRelativePath(directory.Parent);
            return Path.Combine(path, directory.Name);
        }
    }
}
