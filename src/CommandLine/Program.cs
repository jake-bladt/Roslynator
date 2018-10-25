﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Roslynator.CodeFixes;
using Roslynator.Diagnostics;
using Roslynator.Documentation;
using Roslynator.Documentation.Markdown;
using static Roslynator.CommandLine.CommandLineHelpers;
using static Roslynator.CommandLine.DocumentationHelpers;
using static Roslynator.ConsoleHelpers;

namespace Roslynator.CommandLine
{
    internal static class Program
    {
        private static readonly Encoding _defaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static int Main(string[] args)
        {
            WriteLine($"Roslynator Command Line Tool version {typeof(Program).GetTypeInfo().Assembly.GetName().Version}", Verbosity.Quiet);
            WriteLine("Copyright (c) Josef Pihrt. All rights reserved.", Verbosity.Quiet);
            WriteLine(Verbosity.Quiet);

            try
            {
                ParserResult<object> parserResult = Parser.Default.ParseArguments<FixCommandLineOptions,
                    AnalyzeCommandLineOptions,
                    AnalyzeAssemblyCommandLineOptions,
                    FormatCommandLineOptions,
                    PhysicalLinesOfCodeCommandLineOptions,
                    LogicalLinesOfCodeCommandLineOptions,
                    GenerateDocCommandLineOptions,
                    GenerateDeclarationsCommandLineOptions,
                    GenerateDocRootCommandLineOptions>(args);

                bool verbosityParsed = false;

                parserResult.WithParsed<BaseCommandLineOptions>(options =>
                {
                    Verbosity consoleVerbosity = ConsoleOut.Verbosity;

                    if (options.Verbosity == null
                        || TryParseVerbosity(options.Verbosity, out consoleVerbosity))
                    {
                        ConsoleOut.Verbosity = consoleVerbosity;

                        Verbosity logFileVerbosity = consoleVerbosity;

                        if (options.LogFileVerbosity == null
                            || TryParseVerbosity(options.LogFileVerbosity, out logFileVerbosity))
                        {
                            if (options.LogFile != null)
                            {
                                var fs = new FileStream(options.LogFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                                var sw = new StreamWriter(fs, Encoding.UTF8);
                                LogOut = new LogWriter(sw) { Verbosity = logFileVerbosity };
                            }

                            verbosityParsed = true;
                        }
                    }
                });

                if (!verbosityParsed)
                    return 1;

                return parserResult.MapResult(
                    (FixCommandLineOptions options) => FixAsync(options).Result,
                    (AnalyzeCommandLineOptions options) => AnalyzeAsync(options).Result,
                    (AnalyzeAssemblyCommandLineOptions options) => AnalyzeAssemblyCommandExecutor.Execute(options),
                    (FormatCommandLineOptions options) => FormatAsync(options).Result,
                    (PhysicalLinesOfCodeCommandLineOptions options) => PhysicalLinesOrCodeAsync(options).Result,
                    (LogicalLinesOfCodeCommandLineOptions options) => LogicalLinesOrCodeAsync(options).Result,
                    (GenerateDocCommandLineOptions options) => GenerateDoc(options),
                    (GenerateDeclarationsCommandLineOptions options) => GenerateDeclarations(options),
                    (GenerateDocRootCommandLineOptions options) => GenerateDocRoot(options),
                    _ => 1);
            }
            finally
            {
                LogOut?.Dispose();
                LogOut = null;
            }
        }

        private static async Task<int> FixAsync(FixCommandLineOptions options)
        {
            DiagnosticSeverity minimalSeverity = CodeFixerOptions.Default.MinimalSeverity;

            if (options.MinimalSeverity != null)
            {
                if (!TryParseDiagnosticSeverity(options.MinimalSeverity, out DiagnosticSeverity severity))
                    return 1;

                minimalSeverity = severity;
            }

            var executor = new FixCommandExecutor(options, minimalSeverity);

            CommandResult result = await executor.ExecuteAsync(options.Path, options.MSBuildPath, options.Properties);

            return (result.Success) ? 0 : 1;
        }

        private static async Task<int> AnalyzeAsync(AnalyzeCommandLineOptions options)
        {
            DiagnosticSeverity minimalSeverity = CodeAnalyzerOptions.Default.MinimalSeverity;

            if (options.MinimalSeverity != null)
            {
                if (!TryParseDiagnosticSeverity(options.MinimalSeverity, out DiagnosticSeverity severity))
                    return 1;

                minimalSeverity = severity;
            }

            var executor = new AnalyzeCommandExecutor(options, minimalSeverity);

            CommandResult result = await executor.ExecuteAsync(options.Path, options.MSBuildPath, options.Properties);

            return (result.Success) ? 0 : 1;
        }

        private static async Task<int> FormatAsync(FormatCommandLineOptions options)
        {
            var executor = new FormatCommandExecutor(options);

            IEnumerable<string> properties = options.Properties;

            if (options.RemoveRedundantEmptyLine)
            {
                string ruleSetPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "format.ruleset");

                properties = properties.Concat(new string[] { $"CodeAnalysisRuleSet={ruleSetPath}" });
            }

            CommandResult result = await executor.ExecuteAsync(options.Path, options.MSBuildPath, properties);

            return (result.Success) ? 0 : 1;
        }

        private static async Task<int> PhysicalLinesOrCodeAsync(PhysicalLinesOfCodeCommandLineOptions options)
        {
            var executor = new PhysicalLinesOfCodeCommandExecutor(options);

            CommandResult result = await executor.ExecuteAsync(options.Path, options.MSBuildPath, options.Properties);

            return (result.Success) ? 0 : 1;
        }

        private static async Task<int> LogicalLinesOrCodeAsync(LogicalLinesOfCodeCommandLineOptions options)
        {
            var executor = new LogicalLinesOfCodeCommandExecutor(options);

            CommandResult result = await executor.ExecuteAsync(options.Path, options.MSBuildPath, options.Properties);

            return (result.Success) ? 0 : 1;
        }

        private static int GenerateDoc(GenerateDocCommandLineOptions options)
        {
            if (options.MaxDerivedTypes < 0)
            {
                WriteLine("Maximum number of derived items must be equal or greater than 0.", ConsoleColor.Red, Verbosity.Quiet);
                return 1;
            }

            if (!TryParseIgnoredRootParts(options.IgnoredRootParts, out RootDocumentationParts ignoredRootParts))
                return 1;

            if (!TryParseIgnoredNamespaceParts(options.IgnoredNamespaceParts, out NamespaceDocumentationParts ignoredNamespaceParts))
                return 1;

            if (!TryParseIgnoredTypeParts(options.IgnoredTypeParts, out TypeDocumentationParts ignoredTypeParts))
                return 1;

            if (!TryParseIgnoredMemberParts(options.IgnoredMemberParts, out MemberDocumentationParts ignoredMemberParts))
                return 1;

            if (!TryParseOmitContainingNamespaceParts(options.OmitContainingNamespaceParts, out OmitContainingNamespaceParts omitContainingNamespaceParts))
                return 1;

            if (!TryParseVisibility(options.Visibility, out DocumentationVisibility visibility))
                return 1;

            DocumentationModel documentationModel = CreateDocumentationModel(options.References, options.Assemblies, visibility, options.AdditionalXmlDocumentation);

            if (documentationModel == null)
                return 1;

            var documentationOptions = new DocumentationOptions(
                ignoredNames: options.IgnoredNames,
                preferredCultureName: options.PreferredCulture,
                maxDerivedTypes: options.MaxDerivedTypes,
                includeClassHierarchy: !options.NoClassHierarchy,
                placeSystemNamespaceFirst: !options.NoPrecedenceForSystem,
                formatDeclarationBaseList: !options.NoFormatBaseList,
                formatDeclarationConstraints: !options.NoFormatConstraints,
                markObsolete: !options.NoMarkObsolete,
                includeMemberInheritedFrom: !options.OmitMemberInheritedFrom,
                includeMemberOverrides: !options.OmitMemberOverrides,
                includeMemberImplements: !options.OmitMemberImplements,
                includeMemberConstantValue: !options.OmitMemberConstantValue,
                includeInheritedInterfaceMembers: options.IncludeInheritedInterfaceMembers,
                includeAllDerivedTypes: options.IncludeAllDerivedTypes,
                includeAttributeArguments: !options.OmitAttributeArguments,
                includeInheritedAttributes: !options.OmitInheritedAttributes,
                omitIEnumerable: !options.IncludeIEnumerable,
                depth: options.Depth,
                inheritanceStyle: options.InheritanceStyle,
                ignoredRootParts: ignoredRootParts,
                ignoredNamespaceParts: ignoredNamespaceParts,
                ignoredTypeParts: ignoredTypeParts,
                ignoredMemberParts: ignoredMemberParts,
                omitContainingNamespaceParts: omitContainingNamespaceParts,
                scrollToContent: options.ScrollToContent);

            var generator = new MarkdownDocumentationGenerator(documentationModel, WellKnownUrlProviders.GitHub, documentationOptions);

            string directoryPath = options.OutputPath;

            if (!options.NoDelete
                && Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
                catch (IOException ex)
                {
                    WriteLine(ex.ToString(), ConsoleColor.Red, Verbosity.Quiet);
                    return 1;
                }
            }

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            CancellationToken cancellationToken = cts.Token;

            WriteLine($"Documentation is being generated to '{options.OutputPath}'", Verbosity.Minimal);

            foreach (DocumentationGeneratorResult documentationFile in generator.Generate(heading: options.Heading, cancellationToken))
            {
                string path = Path.Combine(directoryPath, documentationFile.FilePath);

                Directory.CreateDirectory(Path.GetDirectoryName(path));

                WriteLine($"  Saving '{path}'", ConsoleColor.DarkGray, Verbosity.Detailed);

                File.WriteAllText(path, documentationFile.Content, _defaultEncoding);
            }

            WriteLine($"Documentation successfully generated to '{options.OutputPath}'.", Verbosity.Minimal);

            return 0;
        }

        private static int GenerateDeclarations(GenerateDeclarationsCommandLineOptions options)
        {
            if (!TryParseIgnoredDeclarationListParts(options.IgnoredParts, out DeclarationListParts ignoredParts))
                return 1;

            if (!TryParseVisibility(options.Visibility, out DocumentationVisibility visibility))
                return 1;

            DocumentationModel documentationModel = CreateDocumentationModel(options.References, options.Assemblies, visibility, options.AdditionalXmlDocumentation);

            if (documentationModel == null)
                return 1;

            var declarationListOptions = new DeclarationListOptions(
                ignoredNames: options.IgnoredNames,
                indent: !options.NoIndent,
                indentChars: options.IndentChars,
                nestNamespaces: options.NestNamespaces,
                newLineBeforeOpenBrace: !options.NoNewLineBeforeOpenBrace,
                emptyLineBetweenMembers: options.EmptyLineBetweenMembers,
                formatBaseList: options.FormatBaseList,
                formatConstraints: options.FormatConstraints,
                formatParameters: options.FormatParameters,
                splitAttributes: !options.MergeAttributes,
                includeAttributeArguments: !options.OmitAttributeArguments,
                omitIEnumerable: !options.IncludeIEnumerable,
                useDefaultLiteral: !options.NoDefaultLiteral,
                fullyQualifiedNames: options.FullyQualifiedNames,
                depth: options.Depth,
                ignoredParts: ignoredParts);

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            CancellationToken cancellationToken = cts.Token;

            WriteLine($"Declaration list is being generated to '{options.OutputPath}'.", Verbosity.Minimal);

            Task<string> task = DeclarationListGenerator.GenerateAsync(
                documentationModel,
                declarationListOptions,
                namespaceComparer: NamespaceSymbolComparer.GetInstance(systemNamespaceFirst: !options.NoPrecedenceForSystem),
                cancellationToken: cancellationToken);

            string content = task.Result;

            string path = options.OutputPath;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, Encoding.UTF8);

            WriteLine($"Declaration list successfully generated to '{options.OutputPath}'.", Verbosity.Minimal);

            return 0;
        }

        private static int GenerateDocRoot(GenerateDocRootCommandLineOptions options)
        {
            if (!TryParseVisibility(options.Visibility, out DocumentationVisibility visibility))
                return 1;

            DocumentationModel documentationModel = CreateDocumentationModel(options.References, options.Assemblies, visibility);

            if (documentationModel == null)
                return 1;

            if (!TryParseIgnoredRootParts(options.Parts, out RootDocumentationParts ignoredParts))
                return 1;

            var documentationOptions = new DocumentationOptions(
                ignoredNames: options.IgnoredNames,
                rootDirectoryUrl: options.RootDirectoryUrl,
                includeClassHierarchy: !options.NoClassHierarchy,
                placeSystemNamespaceFirst: !options.NoPrecedenceForSystem,
                markObsolete: !options.NoMarkObsolete,
                depth: options.Depth,
                ignoredRootParts: ignoredParts,
                omitContainingNamespaceParts: (options.OmitContainingNamespace) ? OmitContainingNamespaceParts.Root : OmitContainingNamespaceParts.None,
                scrollToContent: options.ScrollToContent);

            var generator = new MarkdownDocumentationGenerator(documentationModel, WellKnownUrlProviders.GitHub, documentationOptions);

            string path = options.OutputPath;

            WriteLine($"Documentation root is being generated to '{path}'.", Verbosity.Minimal);

            string heading = options.Heading;

            if (string.IsNullOrEmpty(heading))
            {
                string fileName = Path.GetFileName(options.OutputPath);

                heading = (fileName.EndsWith(".dll", StringComparison.Ordinal))
                    ? Path.GetFileNameWithoutExtension(fileName)
                    : fileName;
            }

            DocumentationGeneratorResult result = generator.GenerateRoot(heading);

            File.WriteAllText(path, result.Content, _defaultEncoding);

            WriteLine($"Documentation root successfully generated to '{path}'.", Verbosity.Minimal);

            return 0;
        }
    }
}
