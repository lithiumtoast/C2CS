# C2CS

C to C# dynamic library bindings code generator. In go `.h` file, out come `.cs`.

## Install

### Latest stable release

See [latest releases](https://github.com/lithiumtoast/c2cs/releases).

### Latest nightly builds

|[Windows (x64)](https://nightly.link/lithiumtoast/c2cs/workflows/build-test-deploy/develop/win-x64.zip)|[macOS](https://nightly.link/lithiumtoast/c2cs/workflows/build-test-deploy/develop/osx-x64.zip)|[Ubuntu 20.04](https://nightly.link/lithiumtoast/c2cs/workflows/build-test-deploy/develop/ubuntu.20.04-x64.zip)|
|---|---|---|

## Documentation

For documentation, including how to use `C2CS` and various examples, see the [docs/README.md](docs/README.md)

## Background: Why?

### Problem

<details>
  <summary>Click to expand!</summary>
<br/>

When creating applications with C# (especially games), it's sometimes necessary to dip down into C/C++ for better raw performance and overall better portability of various different low-level APIs accross various platforms. (This is what FNA does today and what [MonoGame will be doing in the future](https://github.com/MonoGame/MonoGame/issues/7523#issuecomment-865808668).) However, the problem is that maintaining the C# bindings becomes time consuming, error-prone, and in some cases quite tricky.

If you are not familiar already with interoperability of C/C++ with C#, it's assumed that you have read and understood the following relatively short readings:
- [P/Invoke: Introduction](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke).
- [Marshalling: Introduction](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/type-marshaling)
- [Marshalling: Default behaviour for value types in .NET](https://docs.microsoft.com/en-us/dotnet/framework/interop/default-marshaling-behavior#default-marshaling-for-value-types)
</details>

### Solution

<details>
  <summary>Click to expand!</summary>
<br/>

Automatically generate the bindings by compiling/parsing a C `.h` file. Essentially, the C public API for the target operating system + architecture is transpiled to C#.  

This includes all C extern functions and variables which are transpiled to `static` methods or properties respecitively in C#. Also includes transpiling all the C types to C# which are found through transitive property to the extern functions or extern variables, such as: `struct`s, `enum`s, and `const`s. C# `struct`s are generated instead of `class`es on purpose to achieve 1-1 bit-representation of C to C# types called *blittable* types. The reason for blittable types is to achieve pass-through marshalling and active avoidance of the Garbage Collector in C# for best possible runtime performance and portability when doing interoperability.

This is all accomplished by using [libclang](https://clang.llvm.org/docs/Tooling.html) for parsing C and [Roslyn](https://github.com/dotnet/roslyn) for generating C#. All naming is left as found in the header file of the C code.

</details>

### Limitations

<details>
  <summary>Click to expand!</summary>
<br/>

This solution does not work for every C library. This is due to some technical limitations where some C libraries are not "bindgen-friendly".

#### What does it mean for a C library to be bindgen-friendly?

Everything in the [**external linkage**](https://stackoverflow.com/questions/1358400/what-is-external-linkage-and-internal-linkage) of the C API is subject to the following list. Note that the internal guts of the C library is irrelevant and to which this list does not apply. In this sense it is then possible to use C++ internally but then only expose a C interface for interoperability with C#. This list may be updated as more things are discovered/standardized.

|Supported|Description|
|:-:|-|
|:white_check_mark:|Function externs.|
|:white_check_mark:|Variable externs.|
|:white_check_mark:|Function prototypes. (a.k.a., function pointers.)|
|:white_check_mark:|Enums.|
|:white_check_mark:|Structs.<sup>1</sup>|
|:white_check_mark:|Unions.<sup>2</sup>|
|:white_check_mark:|Opaque types.<sup>3</sup>|
|:white_check_mark:|Typedefs. (a.k.a, type aliases.)|
|:o:|Function-like macros.<sup>4</sup>|
|:o:|Object-like macros.<sup>5</sup>|
|:x:|C++.|
|:x:|Objective-C.|
|:x:|Implicit types.<sup>6</sup>|
|:x:|`va_list`.<sup>7</sup>|

<sup>1</sup>: For structs (and unions within structs), distinguishing between public/private fields is not possible automatically. If the record is transtive to a function extern or variable extern then it will be transpiled as if all the fields were public. In some cases this may not be appropriate to which there is the following options. Either, (1) use proper information hiding with C headers so the private fields are not in transtive property to a public function extern or variable extern, or (2) use pointers to access the struct and manually specify the struct as an opaque type for input to `C2CS`. Option 2 is the approach taken for generating bindings for https://github.com/libuv/libuv because `libuv` makes use of mixing public/private struct fields and struct inheritance.

<sup>2</sup>: C# allows for unions using explicit layout of struct fields. Anonymous unions are transpiled to a struct which is nested inside the parent struct.

<sup>3</sup>: For opaque types, if the C header file has direct knowledge of the actual implementation, then they will be by default transpiled as if they were not opaque types. To overcome this, the opaque types in question will need to be manually specified for input to `C2CS`. This a common scenario for single file header libraries such as https://github.com/nothings/stb.

<sup>4</sup>: Function-like macros are only possible if the parameters' types can be inferred 100% of the time during preprocessor; otherwise, not possible. **Not yet implemented**.

<sup>5</sup>: Object-like macros are only possible if the type can inferred 100% of the time during preprocessor; otherwise, not possible. **Not yet implemented**.

<sup>6</sup>: Types must be explicit so they can be found. E.g., it is not possible to transpile an enum if it is never discovered through transitive property of function extern or variable extern.

<sup>7</sup>: For support with `va_list` see https://github.com/lithiumtoast/c2cs/issues/15.

#### What do I do if I want to generate bindings for a non bindgen-friendly C library?

Options:

1. Change the library so that the **external linkage** becomes bindgen-friendly. E.g. removing C++, removing macros, etc.
2. Use https://github.com/InfectedLibraries/Biohazrd as a framework to generate bindings; requires more setup and is not as straightforward, but it works for C++.

</details>

### Other similar projects

<details>
  <summary>Click to expand!</summary>
<br/>

Mentioned here for completeness. I do believe you should be aware of other approaches to this problem and see if they make more sense to you.

- https://github.com/dotnet/runtimelab/tree/feature/DllImportGenerator
- https://github.com/microsoft/ClangSharp
- https://github.com/SharpGenTools/SharpGenTools
- https://github.com/xoofx/CppAst.NET
- https://github.com/rds1983/Sichem

</details>

## Lessons learned

<details>
  <summary>Click to expand!</summary>

### Marshalling

There exist hidden costs for interoperability in C#. How to avoid them?

For C#, the Common Language Runtime (CLR) marshals data between managed and unmanaged contexts (forwards and possibly backwards). In layman's terms, marshalling is transforming the bit representation of a data structure to be correct for the target programming language. For best performance, at worse, marshalling should be minimal, and at best, marshalling should be pass-through. Pass through is the ideal situation when considering performance because both languages agree on the bit representation of data structures without any further processing. C# calls such data structures "blittable". (The sense of the word "blit" means the rapid copying of a block of memory; the word comes from the [bit-block transfer (bit-blit) data operation commonly found in computer graphics](https://en.wikipedia.org/wiki/Bit_blit).) However, to achieve blittable data structures in C#, the garbage collector (GC) is avoided. Why? Because class instances in C# are objects which the allocation of bits can't be controlled precisely by the developer; it's an "implementation detail."

### The garbage collector is a software industry hack

The software industry's attitude, especially business-developers and web-developers, to say that memory is an "implementation detail" and then ignore memory is often justified without knowing or caring for the consequences; it becomes ultimately dangerous.

A function call that changes the state of the system is a side effect. Humans are imperfect at reasoning about side effects, to reason about non-linear systems. An example of a side effect is calling `fopen` in C because it leaves a file in an open state. `malloc` in C is another example of a side effect because it leaves a block of memory allocated. Notice that side effects come in pairs. To close a file, `fclose` is called. To deallocate a block of memory, `free` is called. Other languages have their versions of such function pairs. Some languages went as far as inventing language-specific features, some of which become part of our software programs, so we humans don't have to deal with such pairs of functions. In theory, this is a great idea. And thus, for the specific case of `malloc` and `free`, we invented garbage collection to take us to the promised land of never having to deal with these specific pair of functions.

In practice, using garbage collection to manage your memory automatically turns out to be a horrible idea. This becomes evident if you ever worked on an extensive enough system with the need for real-time responsiveness. In fairness, most applications don't require real-time responsiveness, and it is a lot easier to write safe programs with a garbage collector. However, this is where I think the problem starts. The problem is that developers have become ignorant of why good memory management is essential. This "Oh, the system will take care of it, don't worry." attitude is like a disease that spreads like wild-fire in the industry. The reason is simple: it lowers the bar of experience + knowledge + time required to write safe software. The consequence is that a large number of developers have learned to use a [Golden Hammer](https://en.wikipedia.org/wiki/Law_of_the_instrument#Computer_programming). (The world of finance [also has a definition for Golden Hammer](https://www.investopedia.com/terms/g/golden-hammer.asp) which is relatable.)

Developers have learned to ignore how the hardware operates when solving problems with software, even up to the extreme point that they deny that the hardware even exists. Optimizing code for performance has become an exercise of stumbling around in the pitch-black dark until you find something of interest; it's an afterthought. Even if the developer does find something of interest, it likely opposes his/her worldview of understandable code because they have lost touch with the hardware, lost touch with reality. C# is a useful tool, but you and I have to admit that people mostly use it as Golden Hammer. Just inspect the source code that this tool generates for native bindings as proof of this fact. From my experience, a fair amount of C# developers don't spend their time with such code, don't know how to use structs properly, or even know what blittable data structures are. C# developers (including myself) may need to take a hard look in the mirror, especially if we are open to critizing developers to other programming languages or other fields of business with their own Golden Hammers such as Java, JavaScript, or Electron (:scream:).

</details>

## License

`C2CS` is licensed under the MIT License (`MIT`).

<details>
  <summary>Click to expand!</summary>
<br/>

There are a few exceptions to this detailed below. See the [LICENSE](LICENSE) file for more details on this main product's license.

`C2CS` uses `libclang` which the header files are included as part of the repository under [`ext/clang`](./ext/clang). This is because `C2CS` generates bindings for `libclang` to which `C2CS` generates bindings for `libclang` and other C libraries. The C header files for `libclang` are included for convience of a source-of-truth for re-generating the bindings. These files are licensed under the Apache License v2.0 with LLVM Exceptions; see the [ext/clang/LICENSE.txt](./ext/clang/LICENSE.txt) for more details. The packaged binaries for `libclang` are used from and maintained by https://github.com/microsoft/ClangSharp.

`C2CS` has Git submodules to various C libraries which are included as part of this repository for purposes of testing and demonstrating by examples. These Git submodules can be considered ["vendoring"](https://stackoverflow.com/questions/26217488/what-is-vendoring). The source code for these projects can be found under the `ext` folder. Each of these libraries have their own license and they are not used by `C2CS` directly for purposes of the tool.

</details>


