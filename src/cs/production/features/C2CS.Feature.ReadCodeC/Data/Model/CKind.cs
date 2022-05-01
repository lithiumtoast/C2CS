// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

namespace C2CS.Feature.ReadCodeC.Data.Model;

public enum CKind
{
    Unknown = 0,
    Primitive,
    Pointer,
    Array,
    Function,
    FunctionParameter,
    FunctionPointer,
    FunctionPointerParameter,
    Record,
    RecordField,
    Enum,
    EnumValue,
    OpaqueType,
    Typedef,
    Variable,
    MacroDefinition
}
