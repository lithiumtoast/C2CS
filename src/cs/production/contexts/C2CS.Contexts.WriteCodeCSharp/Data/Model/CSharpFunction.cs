// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

using System.Collections.Immutable;

namespace C2CS.Contexts.WriteCodeCSharp.Data.Model;

public sealed class CSharpFunction : CSharpNode
{
    public readonly CSharpFunctionCallingConvention CallingConvention;

    public readonly ImmutableArray<CSharpFunctionParameter> Parameters;

    public readonly CSharpType ReturnType;

    public CSharpFunction(
        ImmutableArray<TargetPlatform> platforms,
        string name,
        string codeLocationComment,
        int? sizeOf,
        CSharpFunctionCallingConvention callingConvention,
        CSharpType returnType,
        ImmutableArray<CSharpFunctionParameter> parameters)
        : base(platforms, name, codeLocationComment, sizeOf)
    {
        CallingConvention = callingConvention;
        ReturnType = returnType;
        Parameters = parameters;
    }

    public override bool Equals(CSharpNode? other)
    {
        if (!base.Equals(other) || other is not CSharpFunction other2)
        {
            return false;
        }

        var result = CallingConvention == other2.CallingConvention &&
               ReturnType == other2.ReturnType &&
               Parameters.SequenceEqual(other2.Parameters);

        return result;
    }

    public override int GetHashCode()
    {
        var baseHashCode = base.GetHashCode();
        var parameters = Parameters.GetHashCodeMembers();
        var hashCode = HashCode.Combine(baseHashCode, CallingConvention, ReturnType, parameters);
        return hashCode;
    }
}
