// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

using System.Collections.Immutable;

namespace C2CS.Data.CSharp.Model;

public sealed class CSharpMacroObject : CSharpNode
{
    public readonly bool IsConstant;
    public readonly string Type;

    public readonly string Value;

    public CSharpMacroObject(
        ImmutableArray<TargetPlatform> platforms,
        string name,
        string cKind,
        string cCodeLocation,
        int? sizeOf,
        string type,
        string value,
        bool isConstant,
        ImmutableArray<Attribute> attributes)
        : base(platforms, name, cKind, cCodeLocation, sizeOf, attributes)
    {
        Type = type;
        Value = value;
        IsConstant = isConstant;
    }

    public override bool Equals(CSharpNode? other)
    {
        if (!base.Equals(other) || other is not CSharpMacroObject other2)
        {
            return false;
        }

        return Type == other2.Type &&
               Value == other2.Value;
    }

    public override int GetHashCode()
    {
        var baseHashCode = base.GetHashCode();
        var hashCode = HashCode.Combine(baseHashCode, Type, Value);
        return hashCode;
    }
}
