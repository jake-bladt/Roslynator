﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.Metrics;
using static Roslynator.ConsoleHelpers;

namespace Roslynator.CommandLine
{
    internal class PhysicalLinesOfCodeCommandExecutor : AbstractLinesOfCodeCommandExecutor
    {
        public PhysicalLinesOfCodeCommandExecutor(PhysicalLinesOfCodeCommandLineOptions options)
        {
            Options = options;
        }

        public PhysicalLinesOfCodeCommandLineOptions Options { get; }

        public override async Task<CommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
        {
            var codeMetricsOptions = new CodeMetricsOptions(
                includeGenerated: Options.IncludeGenerated,
                includeWhiteSpace: Options.IncludeWhiteSpace,
                includeComments: Options.IncludeComments,
                includePreprocessorDirectives: Options.IncludePreprocessorDirectives,
                ignoreBlockBoundary: Options.IgnoreBlockBoundary);

            if (projectOrSolution.IsProject)
            {
                Project project = projectOrSolution.AsProject();

                CodeMetricsCounter counter = CodeMetricsCounters.GetPhysicalLinesCounter(project.Language);

                if (counter != null)
                {
                    WriteLine($"Count lines for '{project.FilePath}'", ConsoleColor.Cyan, Verbosity.Minimal);

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    CodeMetrics metrics = await counter.CountLinesAsync(project, codeMetricsOptions, cancellationToken);

                    stopwatch.Stop();

                    WriteLine(Verbosity.Minimal);

                    WriteMetrics(
                        metrics.CodeLineCount,
                        metrics.BlockBoundaryLineCount,
                        metrics.WhiteSpaceLineCount,
                        metrics.CommentLineCount,
                        metrics.PreprocessorDirectiveLineCount,
                        metrics.TotalLineCount);

                    WriteLine(Verbosity.Minimal);
                    WriteLine($"Done counting lines for '{project.FilePath}' {stopwatch.Elapsed:mm\\:ss\\.ff}", ConsoleColor.Green, Verbosity.Normal);
                }
                else
                {
                    WriteLine($"Cannot count lines for '{project.FilePath}' language '{project.Language}' is not supported", ConsoleColor.Yellow, Verbosity.Minimal);
                }
            }
            else
            {
                Solution solution = projectOrSolution.AsSolution();

                WriteLine($"Count lines for solution '{solution.FilePath}'", ConsoleColor.Cyan, Verbosity.Minimal);

                IEnumerable<Project> projects = FilterProjects(solution, Options.IgnoredProjects, Options.Language);

                Stopwatch stopwatch = Stopwatch.StartNew();

                ImmutableDictionary<ProjectId, CodeMetrics> projectsMetrics = CodeMetricsCounter.CountPhysicalLinesInParallel(projects, codeMetricsOptions, cancellationToken);

                stopwatch.Stop();

                if (projectsMetrics.Count > 0)
                {
                    WriteLine(Verbosity.Normal);
                    WriteLine("Lines of code by project:", Verbosity.Normal);

                    WriteLinesOfCode(solution, projectsMetrics);
                }

                WriteLine(Verbosity.Minimal);
                WriteLine("Summary:", Verbosity.Minimal);

                WriteMetrics(
                    projectsMetrics.Sum(f => f.Value.CodeLineCount),
                    projectsMetrics.Sum(f => f.Value.BlockBoundaryLineCount),
                    projectsMetrics.Sum(f => f.Value.WhiteSpaceLineCount),
                    projectsMetrics.Sum(f => f.Value.CommentLineCount),
                    projectsMetrics.Sum(f => f.Value.PreprocessorDirectiveLineCount),
                    projectsMetrics.Sum(f => f.Value.TotalLineCount));

                WriteLine(Verbosity.Minimal);
                WriteLine($"Done counting lines for solution '{solution.FilePath}' {stopwatch.Elapsed:mm\\:ss\\.ff}", ConsoleColor.Green, Verbosity.Minimal);
            }

            return new CommandResult(true);
        }

        private void WriteMetrics(int totalCodeLineCount, int totalBlockBoundaryLineCount, int totalWhiteSpaceLineCount, int totalCommentLineCount, int totalPreprocessorDirectiveLineCount, int totalLineCount)
        {
            string totalCodeLines = totalCodeLineCount.ToString("n0");
            string totalBlockBoundaryLines = totalBlockBoundaryLineCount.ToString("n0");
            string totalWhiteSpaceLines = totalWhiteSpaceLineCount.ToString("n0");
            string totalCommentLines = totalCommentLineCount.ToString("n0");
            string totalPreprocessorDirectiveLines = totalPreprocessorDirectiveLineCount.ToString("n0");
            string totalLines = totalLineCount.ToString("n0");

            int maxDigits = Math.Max(totalCodeLines.Length,
                Math.Max(totalBlockBoundaryLines.Length,
                    Math.Max(totalWhiteSpaceLines.Length,
                        Math.Max(totalCommentLines.Length,
                            Math.Max(totalPreprocessorDirectiveLines.Length, totalLines.Length)))));

            if (Options.IgnoreBlockBoundary
                || !Options.IncludeWhiteSpace
                || !Options.IncludeComments
                || !Options.IncludePreprocessorDirectives)
            {
                WriteLine($"{totalCodeLines.PadLeft(maxDigits)} {totalCodeLineCount / (double)totalLineCount,4:P0} lines of code", Verbosity.Minimal);
            }
            else
            {
                WriteLine($"{totalCodeLines.PadLeft(maxDigits)} lines of code", Verbosity.Minimal);
            }

            if (Options.IgnoreBlockBoundary)
                WriteLine($"{totalBlockBoundaryLines.PadLeft(maxDigits)} {totalBlockBoundaryLineCount / (double)totalLineCount,4:P0} block boundary lines", Verbosity.Minimal);

            if (!Options.IncludeWhiteSpace)
                WriteLine($"{totalWhiteSpaceLines.PadLeft(maxDigits)} {totalWhiteSpaceLineCount / (double)totalLineCount,4:P0} white-space lines", Verbosity.Minimal);

            if (!Options.IncludeComments)
                WriteLine($"{totalCommentLines.PadLeft(maxDigits)} {totalCommentLineCount / (double)totalLineCount,4:P0} comment lines", Verbosity.Minimal);

            if (!Options.IncludePreprocessorDirectives)
                WriteLine($"{totalPreprocessorDirectiveLines.PadLeft(maxDigits)} {totalPreprocessorDirectiveLineCount / (double)totalLineCount,4:P0} preprocessor directive lines", Verbosity.Minimal);

            WriteLine($"{totalLines.PadLeft(maxDigits)} {totalLineCount / (double)totalLineCount,4:P0} total lines", Verbosity.Minimal);
        }

        protected override void OperationCanceled(OperationCanceledException ex)
        {
            WriteLine("Lines counting was canceled.", Verbosity.Quiet);
        }
    }
}
