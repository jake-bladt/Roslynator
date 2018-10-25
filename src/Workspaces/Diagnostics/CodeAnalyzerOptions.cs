﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Roslynator.Diagnostics
{
    public class CodeAnalyzerOptions : CodeAnalysisOptions
    {
        public static CodeAnalyzerOptions Default { get; } = new CodeAnalyzerOptions();

        public CodeAnalyzerOptions(
            bool ignoreAnalyzerReferences = false,
            bool ignoreCompilerDiagnostics = false,
            bool reportFadeDiagnostics = false,
            bool reportSuppressedDiagnostics = false,
            bool executionTime = false,
            DiagnosticSeverity minimalSeverity = DiagnosticSeverity.Info,
            IEnumerable<string> supportedDiagnosticIds = null,
            IEnumerable<string> ignoredDiagnosticIds = null,
            IEnumerable<string> ignoredProjectNames = null,
            string language = null,
            string cultureName = null) : base(minimalSeverity, ignoreAnalyzerReferences, supportedDiagnosticIds, ignoredDiagnosticIds, ignoredProjectNames, language)
        {
            IgnoreCompilerDiagnostics = ignoreCompilerDiagnostics;
            ReportFadeDiagnostics = reportFadeDiagnostics;
            ReportSuppressedDiagnostics = reportSuppressedDiagnostics;
            ExecutionTime = executionTime;
            CultureName = cultureName;
        }

        public bool IgnoreCompilerDiagnostics { get; }

        public bool ReportFadeDiagnostics { get; }

        public bool ReportSuppressedDiagnostics { get; }

        public bool ExecutionTime { get; }

        public string CultureName { get; }
    }
}
