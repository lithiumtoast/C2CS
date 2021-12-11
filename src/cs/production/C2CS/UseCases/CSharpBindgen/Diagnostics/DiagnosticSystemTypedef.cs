// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

namespace C2CS.UseCases.CSharpBindgen;

public class DiagnosticSystemTypedef : Diagnostic
{
    public DiagnosticSystemTypedef(string typeName, ClangLocation loc, string underlyingTypeName)
        : base(DiagnosticSeverity.Warning)
    {
        Summary =
            $"The typedef '{typeName}' at {loc.FilePath}:{loc.LineNumber}:{loc.LineColumn} is a system alias to the system type '{underlyingTypeName}'.";
    }
}
