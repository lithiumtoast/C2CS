// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using C2CS.Contexts.ReadCodeC.Data.Model;
using static bottlenoselabs.clang;

namespace C2CS.Contexts.ReadCodeC.Domain;

public static unsafe class ClangExtensions
{
    public delegate bool VisitChildPredicate(CXCursor child, CXCursor parent);

    private static VisitChildInstance[] _visitChildInstances = new VisitChildInstance[512];
    private static int _visitChildCount;

    private static readonly CXCursorVisitor VisitorChildren;
    private static readonly CXCursorVisitor VisitorChildrenRecursive;
    private static readonly VisitChildPredicate EmptyPredicate = static (_, _) => true;

    static ClangExtensions()
    {
        VisitorChildren.Pointer = &VisitChild;
        VisitorChildrenRecursive.Pointer = &VisitChildRecursive;
    }

    public static ImmutableArray<CXCursor> GetDescendents(
        this CXCursor cursor, VisitChildPredicate? predicate = null, bool sameFile = true)
    {
        var predicate2 = predicate ?? EmptyPredicate;
        var visitData = new VisitChildInstance(predicate2, sameFile);
        var visitsCount = Interlocked.Increment(ref _visitChildCount);
        if (visitsCount > _visitChildInstances.Length)
        {
            Array.Resize(ref _visitChildInstances, visitsCount * 2);
        }

        _visitChildInstances[visitsCount - 1] = visitData;

        var clientData = default(CXClientData);
        clientData.Data = (void*)_visitChildCount;
        clang_visitChildren(cursor, VisitorChildren, clientData);

        Interlocked.Decrement(ref _visitChildCount);

        var result = visitData.NodeBuilder.ToImmutable();
        visitData.NodeBuilder.Clear();
        return result;
    }

    [UnmanagedCallersOnly]
    private static CXChildVisitResult VisitChild(CXCursor child, CXCursor parent, CXClientData clientData)
    {
        var index = (int)clientData.Data;
        var data = _visitChildInstances[index - 1];

        var name = child.Name();

        if (data.SameFile)
        {
            var location = clang_getCursorLocation(child);
            var isFromMainFile = clang_Location_isFromMainFile(location) > 0;
            if (!isFromMainFile)
            {
                return CXChildVisitResult.CXChildVisit_Continue;
            }
        }

        var result = data.Predicate(child, parent);
        if (!result)
        {
            return CXChildVisitResult.CXChildVisit_Continue;
        }

        data.NodeBuilder.Add(child);

        return CXChildVisitResult.CXChildVisit_Continue;
    }

    public static ImmutableArray<CXCursor> GetDescendentsRecursive(
        this CXCursor cursor, VisitChildPredicate predicate, bool sameFile = true)
    {
        var visitData = new VisitChildInstance(predicate, sameFile);
        var visitsCount = Interlocked.Increment(ref _visitChildCount);
        if (visitsCount > _visitChildInstances.Length)
        {
            Array.Resize(ref _visitChildInstances, visitsCount * 2);
        }

        _visitChildInstances[visitsCount - 1] = visitData;

        var clientData = default(CXClientData);
        clientData.Data = (void*)_visitChildCount;
        clang_visitChildren(cursor, VisitorChildrenRecursive, clientData);

        Interlocked.Decrement(ref _visitChildCount);

        var result = visitData.NodeBuilder.ToImmutable();
        visitData.NodeBuilder.Clear();
        return result;
    }

    [UnmanagedCallersOnly]
    private static CXChildVisitResult VisitChildRecursive(CXCursor child, CXCursor parent, CXClientData clientData)
    {
        var index = (int)clientData.Data;
        var data = _visitChildInstances[index - 1];

        if (data.SameFile)
        {
            var location = clang_getCursorLocation(child);
            var isFromMainFile = clang_Location_isFromMainFile(location) > 0;
            if (isFromMainFile)
            {
                return CXChildVisitResult.CXChildVisit_Continue;
            }
        }

        var result = data.Predicate(child, parent);
        if (!result)
        {
            return CXChildVisitResult.CXChildVisit_Continue;
        }

        clang_visitChildren(child, VisitorChildrenRecursive, clientData);

        data.NodeBuilder.Add(child);

        return CXChildVisitResult.CXChildVisit_Continue;
    }

    public static string String(this CXString cxString)
    {
        var cString = clang_getCString(cxString);
        var result = Marshal.PtrToStringAnsi(cString)!;
        clang_disposeString(cxString);
        return result;
    }

    public static CLocation Location(
        this CXCursor cursor,
        CXType? type,
        ImmutableDictionary<string, string>? linkedPaths,
        ImmutableArray<string>? userIncludeDirectories)
    {
        if (cursor.kind == CXCursorKind.CXCursor_TranslationUnit)
        {
            return CLocation.NoLocation;
        }

        if (type != null)
        {
            if (cursor.kind != CXCursorKind.CXCursor_FunctionDecl &&
                type.Value.kind is CXTypeKind.CXType_FunctionProto or CXTypeKind.CXType_FunctionNoProto)
            {
                return CLocation.NoLocation;
            }

            if (type.Value.kind is
                CXTypeKind.CXType_Pointer or
                CXTypeKind.CXType_ConstantArray or
                CXTypeKind.CXType_IncompleteArray)
            {
                return CLocation.NoLocation;
            }

            if (type.Value.IsPrimitive())
            {
                return CLocation.NoLocation;
            }
        }

        if (cursor.kind == CXCursorKind.CXCursor_NoDeclFound)
        {
            var up = new InvalidOperationException("Expected a valid cursor when getting the location.");
            throw up;
        }

        var locationSource = clang_getCursorLocation(cursor);
        var translationUnit = clang_Cursor_getTranslationUnit(cursor);
        var location = GetLocation(locationSource, translationUnit, linkedPaths, userIncludeDirectories);
        return location;
    }

    private static CLocation GetLocation(
        CXSourceLocation locationSource,
        CXTranslationUnit? translationUnit = null,
        ImmutableDictionary<string, string>? linkedPaths = null,
        ImmutableArray<string>? userIncludeDirectories = null)
    {
        CXFile file;
        uint lineNumber;
        uint columnNumber;
        uint offset;

        clang_getFileLocation(locationSource, &file, &lineNumber, &columnNumber, &offset);

        var handle = (IntPtr)file.Data;
        if (handle == IntPtr.Zero)
        {
            if (!translationUnit.HasValue)
            {
                return CLocation.NoLocation;
            }

            return LocationInTranslationUnit(translationUnit.Value, (int)lineNumber, (int)columnNumber);
        }

        var fileNamePath = clang_getFileName(file).String();
        var fileName = Path.GetFileName(fileNamePath);
        var fullFilePath = string.IsNullOrEmpty(fileNamePath) ? string.Empty : Path.GetFullPath(fileNamePath);

        var location = new CLocation
        {
            FileName = fileName,
            FilePath = fullFilePath,
            FullFilePath = fullFilePath,
            LineNumber = (int)lineNumber,
            LineColumn = (int)columnNumber
        };

        if (string.IsNullOrEmpty(location.FilePath))
        {
            return location;
        }

        if (linkedPaths != null)
        {
            foreach (var (linkedDirectory, targetDirectory) in linkedPaths)
            {
                if (location.FilePath.Contains(linkedDirectory, StringComparison.InvariantCulture))
                {
                    location.FilePath = location.FilePath
                        .Replace(linkedDirectory, targetDirectory, StringComparison.InvariantCulture).Trim('/', '\\');
                    break;
                }
            }
        }

        if (userIncludeDirectories != null)
        {
            foreach (var directory in userIncludeDirectories)
            {
                if (location.FilePath.Contains(directory, StringComparison.InvariantCulture))
                {
                    location.FilePath = location.FilePath
                        .Replace(directory, string.Empty, StringComparison.InvariantCulture).Trim('/', '\\');
                    break;
                }
            }
        }

        return location;
    }

    private static CLocation LocationInTranslationUnit(
        CXTranslationUnit translationUnit,
        int lineNumber,
        int columnNumber)
    {
        var cursor = clang_getTranslationUnitCursor(translationUnit);
        var filePath = clang_getCursorSpelling(cursor).String();
        return new CLocation
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            LineNumber = lineNumber,
            LineColumn = columnNumber
        };
    }

    public static string Name(this CXCursor clangCursor)
    {
        var result = clang_getCursorSpelling(clangCursor).String();
        return SanitizeClangName(result);
    }

    private static string SanitizeClangName(string result)
    {
        if (string.IsNullOrEmpty(result))
        {
            return string.Empty;
        }

        if (result.Contains("struct ", StringComparison.InvariantCulture))
        {
            result = result.Replace("struct ", string.Empty, StringComparison.InvariantCulture);
        }

        if (result.Contains("union ", StringComparison.InvariantCulture))
        {
            result = result.Replace("union ", string.Empty, StringComparison.InvariantCulture);
        }

        if (result.Contains("enum ", StringComparison.InvariantCulture))
        {
            result = result.Replace("enum ", string.Empty, StringComparison.InvariantCulture);
        }

        if (result.Contains("const ", StringComparison.InvariantCulture))
        {
            result = result.Replace("const ", string.Empty, StringComparison.InvariantCulture);
        }

        if (result.Contains("*const", StringComparison.InvariantCulture))
        {
            result = result.Replace("*const", "*", StringComparison.InvariantCulture);
        }

        return result;
    }

    public static bool IsPrimitive(this CXType type)
    {
        return type.kind switch
        {
            CXTypeKind.CXType_Void => true,
            CXTypeKind.CXType_Bool => true,
            CXTypeKind.CXType_Char_S => true,
            CXTypeKind.CXType_SChar => true,
            CXTypeKind.CXType_Char_U => true,
            CXTypeKind.CXType_UChar => true,
            CXTypeKind.CXType_UShort => true,
            CXTypeKind.CXType_UInt => true,
            CXTypeKind.CXType_ULong => true,
            CXTypeKind.CXType_ULongLong => true,
            CXTypeKind.CXType_Short => true,
            CXTypeKind.CXType_Int => true,
            CXTypeKind.CXType_Long => true,
            CXTypeKind.CXType_LongLong => true,
            CXTypeKind.CXType_Float => true,
            CXTypeKind.CXType_Double => true,
            CXTypeKind.CXType_LongDouble => true,
            _ => false
        };
    }

    public static bool IsSignedPrimitive(this CXType type)
    {
        if (!IsPrimitive(type))
        {
            return false;
        }

        return type.kind switch
        {
            CXTypeKind.CXType_Char_S => true,
            CXTypeKind.CXType_SChar => true,
            CXTypeKind.CXType_Char_U => true,
            CXTypeKind.CXType_Short => true,
            CXTypeKind.CXType_Int => true,
            CXTypeKind.CXType_Long => true,
            CXTypeKind.CXType_LongLong => true,
            _ => false
        };
    }

    public static bool IsUnsignedPrimitive(this CXType type)
    {
        if (!IsPrimitive(type))
        {
            return false;
        }

        return type.kind switch
        {
            CXTypeKind.CXType_Char_U => true,
            CXTypeKind.CXType_UChar => true,
            CXTypeKind.CXType_UShort => true,
            CXTypeKind.CXType_UInt => true,
            CXTypeKind.CXType_ULong => true,
            CXTypeKind.CXType_ULongLong => true,
            _ => false
        };
    }

    public static string Name(this CXType type, CXCursor? cursor = null)
    {
        var spelling = clang_getTypeSpelling(type).String();
        if (string.IsNullOrEmpty(spelling))
        {
            return string.Empty;
        }

        string result;

        var isPrimitive = type.IsPrimitive();
        var isFunctionPointer = type.kind == CXTypeKind.CXType_FunctionProto;
        if (isPrimitive || isFunctionPointer)
        {
            result = spelling;
        }
        else
        {
            var cursorType = cursor ?? clang_getTypeDeclaration(type);

            switch (type.kind)
            {
                case CXTypeKind.CXType_Pointer:
                    var pointeeType = clang_getPointeeType(type);
                    if (pointeeType.kind == CXTypeKind.CXType_Attributed)
                    {
                        pointeeType = clang_Type_getModifiedType(pointeeType);
                    }

                    var pointeeCursor = clang_getTypeDeclaration(pointeeType);
                    if (pointeeCursor.kind == CXCursorKind.CXCursor_NoDeclFound &&
                        pointeeType.kind == CXTypeKind.CXType_FunctionProto)
                    {
                        // Function pointer without a declaration, this can happen when the type is field or a param
                        var functionProtoSpelling = clang_getTypeSpelling(pointeeType).String();
                        result = functionProtoSpelling;
                    }
                    else
                    {
                        // Pointer to some type
                        var pointeeTypeName = Name(pointeeType, pointeeCursor);
                        result = $"{pointeeTypeName}*";
                    }

                    break;
                case CXTypeKind.CXType_Typedef:
                    // typedef always has a declaration
                    var typedef = clang_getTypeDeclaration(type);
                    result = typedef.Name();
                    break;
                case CXTypeKind.CXType_Record:
                    result = type.NameInternal();
                    break;
                case CXTypeKind.CXType_Enum:
                    result = cursorType.Name();
                    if (string.IsNullOrEmpty(result))
                    {
                        result = type.NameInternal();
                    }

                    break;
                case CXTypeKind.CXType_ConstantArray:
                    var elementTypeConstantArray = clang_getArrayElementType(type);
                    result = Name(elementTypeConstantArray, cursorType);
                    break;
                case CXTypeKind.CXType_IncompleteArray:
                    var elementTypeIncompleteArray = clang_getArrayElementType(type);
                    var elementTypeName = Name(elementTypeIncompleteArray, cursorType);
                    result = $"{elementTypeName}*";
                    break;
                case CXTypeKind.CXType_Elaborated:
                    // type has a modifier prefixed such as "struct MyStruct" or "union ABC",
                    // drill down to the type and cursor with just the name
                    var namedTyped = clang_Type_getNamedType(type);
                    var namedCursor = clang_getTypeDeclaration(namedTyped);
                    result = Name(namedTyped, namedCursor);
                    break;
                case CXTypeKind.CXType_Attributed:
                    var modifiedType = clang_Type_getModifiedType(type);
                    result = Name(modifiedType, cursorType);
                    break;
                default:
                    return string.Empty;
            }
        }

        result = SanitizeClangName(result);

        return result;
    }

    private static string NameInternal(this CXType clangType)
    {
        var result = clang_getTypeSpelling(clangType).String();
        if (string.IsNullOrEmpty(result))
        {
            return string.Empty;
        }

        result = SanitizeClangName(result);
        return result;
    }

    private readonly struct VisitChildInstance
    {
        public readonly VisitChildPredicate Predicate;
        public readonly ImmutableArray<CXCursor>.Builder NodeBuilder;
        public readonly bool SameFile;

        public VisitChildInstance(VisitChildPredicate predicate, bool sameFile)
        {
            Predicate = predicate;
            NodeBuilder = ImmutableArray.CreateBuilder<CXCursor>();
            SameFile = sameFile;
        }
    }
}
