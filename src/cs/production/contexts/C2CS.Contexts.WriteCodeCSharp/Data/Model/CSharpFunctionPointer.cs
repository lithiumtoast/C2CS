// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

using System.Collections.Immutable;

namespace C2CS.Contexts.WriteCodeCSharp.Data.Model;

public sealed class CSharpFunctionPointer : CSharpNode
{
    public readonly ImmutableArray<CSharpFunctionPointerParameter> Parameters;

    public readonly CSharpType ReturnType;

    public CSharpFunctionPointer(
        ImmutableArray<TargetPlatform> platforms,
        string name,
        string codeLocationComment,
        int? sizeOf,
        CSharpType returnType,
        ImmutableArray<CSharpFunctionPointerParameter> parameters)
        : base(platforms, name, codeLocationComment, sizeOf)
    {
        Parameters = parameters;
        ReturnType = returnType;
    }

    public override bool Equals(CSharpNode? other)
    {
        if (!base.Equals(other) || other is not CSharpFunctionPointer other2)
        {
            return false;
        }

        return ReturnType == other2.ReturnType &&
               Parameters.SequenceEqual(other2.Parameters);
    }

    public override int GetHashCode()
    {
        var baseHashCode = base.GetHashCode();
        var parametersHashCode = Parameters.GetHashCodeMembers();
        var hashCode = HashCode.Combine(baseHashCode, ReturnType, parametersHashCode);
        return hashCode;
    }
}
