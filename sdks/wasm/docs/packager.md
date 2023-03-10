# WebAssembly packager.exe

`packager.exe` is an interim utility for publishing Mono WebAssembly projects while additional tooling is being developed. 

The command line arguments for Packager.exe can be displayed by using the help command line option.

``` bash
packager.exe -help
```

The command produces the following output:

``` 

Usage: packager.exe <options> <assemblies>
Valid options:
        --help          Show this help message
        --debugrt       Use the debug runtime (default release) - this has nothing to do with C# debugging
        --aot           Enable AOT mode
        --aot-interp    Enable AOT+INTERP mode
        --prefix=x      Set the input assembly prefix to 'x' (default to the current directory)
        --out=x         Set the output directory to 'x' (default to the current directory)
        --mono-sdkdir=x Set the mono sdk directory to 'x'
        --deploy=x      Set the deploy prefix to 'x' (default to 'managed')
        --vfs=x         Set the VFS prefix to 'x' (default to 'managed')
        --template=x    Set the template name to  'x' (default to 'runtime.js')
        --asset=x       Add specified asset 'x' to list of assets to be copied
        --search-path=x Add specified path 'x' to list of paths used to resolve assemblies
        --copy=always|ifnewer        Set the type of copy to perform.
                              'always' overwrites the file if it exists.
                              'ifnewer' copies or overwrites the file if modified or size is different.
        --profile=x     Enable the 'x' mono profiler.
        --aot-assemblies=x List of assemblies to AOT in AOT+INTERP mode.
        --link-mode=sdkonly|all        Set the link type used for AOT. (EXPERIMENTAL)
        --pinvoke-libs=x DllImport libraries used.
                              'sdkonly' only link the Core libraries.
                              'all' link Core and User assemblies. (default)
foo.dll         Include foo.dll as one of the root assemblies

Additional options (--option/--no-option):
  --debug (enable c# debugging)
        type: bool  default: false
  --debugrt (enable debug runtime)
        type: bool  default: false
  --linker (enable the linker)
        type: bool  default: false
  --binding (enable the binding engine)
        type: bool  default: true
  --link-icalls (link away unused icalls)
        type: bool  default: false
  --il-strip (strip IL code from assemblies in AOT mode)
        type: bool  default: true
  --linker-verbose (set verbose option on linker)
        type: bool  default: false
  --zlib (enable the use of zlib for System.IO.Compression support)
        type: bool  default: false
  --enable-fs (enable filesystem support ???through Emscripten's file_packager.py in a later phase)
        type: bool  default: false
  --threads (enable threads)
        type: bool  default: false        

```

## Interpreted:

Steps of execution are:

1. Setup options passed from the command line.
1. Identify the assemblies referenced by the assemblies passed to packager.
1. Write all referenced assemblies to the output folder ???managed??? by default
1. Copy the Mono WebAssembly implementations to the output folder.  Default is `release` but can be controlled by adding the ???debugrt command line option.
1. Generate a runtime.js and mono-config.js which handles the loading of Mono WebAssembly modules.

### Example 1: Using packager.exe with a non AOT project

``` bash
mono packager.exe --copy=always --out=./publish --asset=./sample.html --asset=server.py sample.dll
```

Additional assets can be included with the `???asset` command line option.  These will be copied to the output folder that is created.

```
--asset=./sample.html --asset=server.py
```

The following ouput directory structure is created:

```

         |--- publish               // Output from the `packager.exe` application 
            |--- managed            // Where to find the managed assemblies generated by packager.exe
            |--- mono-config.js     // Configuration file referenced in `sample.html` and `runtime.js`
            |--- dotnet.js          // Mono WebAssembly implementations
            |--- dotnet.wasm        // Mono WebAssembly implementations
            |--- runtime.js         // Handles the loading of the Mono WebAssembly modules
            |--- sample.html        // sample html file
            |--- server.py          // python test server

```

With a non AOT project the resolved assemblies are not as of yet run through the linker but instead copied directly to the managed directory.

### Example 2:  Using packager.exe with a non AOT project

```
mono packager.exe  --copy=ifnewer --out=publish --search-path=./managed/ --asset=index.html --asset=RunClass.txt  ./bin/WasmRoslyn.dll
```

The example here uses the `???search-path` command line option.  The `???search-path` will add another path that will be used to resolve assemblies that are identified in the specified `WasmRoslyn.dll` assembly.

Again, no linking of the the assemblies is as of yet implemented for non AOT???d projects.

## AOT:

AOT support is enabled by passing `--aot` to the packager.  It requires a functional emscripten sdk installation.

Access to emscripten sdk which is controlled by:

```
--emscripten-sdkdir
```

Access to mono sdk which is controlled by:

```
--mono-sdkdir=
```

Steps of execution are:

1. Setup options passed from the command line.
1. Make deploy directory --deploy=x      Set the deploy prefix to 'x' (default to 'managed')
1. Generate runtime.js
1. Generate mono-config.js
1. Create driver.o from driver.c by using emcc from Emscripten:
   ```
   emcc -Oz -g -s EMULATED_FUNCTION_POINTERS=1 -s DISABLE_EXCEPTION_CATCHING=0 -s ASSERTIONS=1 -s WASM=1 -s ALLOW_MEMORY_GROWTH=1 -s BINARYEN=1 -s "BINARYEN_TRAP_MODE='clamp'" -s TOTAL_MEMORY=134217728 -s ALIASING_FUNCTION_POINTERS=0 -s NO_EXIT_RUNTIME=1 -s ERROR_ON_UNDEFINED_SYMBOLS=1 -s "EXTRA_EXPORTED_RUNTIME_METHODS=['ccall', 'cwrap', 'setValue', 'getValue', 'UTF8ToString']" -s "EXPORTED_FUNCTIONS=['___cxa_is_pointer_type', '___cxa_can_catch']" -DENABLE_AOT=1 -DDRIVER_GEN=1 -Imono/sdks/out/wasm-runtime-release/include/mono-2.0 -c -o driver.o driver.c
   ```
1. Generate driver-gen.c.in which handles registering aot modules.
1. Run monolinker.exe on the assemblies
   - Can be controlled by the command line option.
   ```
      --link-mode=sdkonly|all        Set the link type used for AOT. (EXPERIMENTAL)
                              'sdkonly' only link the Core libraries.
                              'all' link Core and User assemblies. (default)

   ```
1. Mono Ahead of Time (AOT) compiler is then executed over each assembly output by linker.

   - Example:
   ``` bash
   MONO_PATH=./linker-out mono/sdks/out/wasm-cross-release/bin/wasm32-unknown-none-mono-sgen --debug  --aot=dedup-skip,llvmonly,interp,asmonly,no-opt,static,direct-icalls,llvm-outfile=./System.Numerics.dll.bc ./linker-out/System.Numerics.dll
   ```
   This pass generates LLVM bitcode for each AOT???d assembly in a .dll.bc which will later be fed into emcc as part of the last compilation  step
    
1. Mono CIL Stripper is executed over each assembly

   ``` bash
   Assembly ilstrip-out/System.Xml.dll stripped
   [27/46] cp linker-out/System.Numerics.dll ilstrip-out/System.Numerics.dll; mono-cil-strip ilstrip-out/System.Numerics.dll
   Mono CIL Stripper
   ```
1. The WebAssembly modules mono.js, mono.wasm etc are then generated.
   ``` bash
   emcc -Oz -g -s EMULATED_FUNCTION_POINTERS=1 -s DISABLE_EXCEPTION_CATCHING=0 -s ASSERTIONS=1 -s WASM=1 -s ALLOW_MEMORY_GROWTH=1 -s BINARYEN=1 -s "BINARYEN_TRAP_MODE='clamp'" -s TOTAL_MEMORY=134217728 -s ALIASING_FUNCTION_POINTERS=0 -s NO_EXIT_RUNTIME=1 -s ERROR_ON_UNDEFINED_SYMBOLS=1 -s "EXTRA_EXPORTED_RUNTIME_METHODS=['ccall', 'cwrap', 'setValue', 'getValue', 'UTF8ToString']" -s "EXPORTED_FUNCTIONS=['___cxa_is_pointer_type', '___cxa_can_catch']" -o bin/aot-mini/mono.js --js-library sdks/wasm/library_mono.js --js-library sdks/wasm/binding_support.js --js-library sdks/wasm/dotnet_support.js driver.o mini_tests.dll.bc mscorlib.dll.bc System.dll.bc Mono.Security.dll.bc System.Xml.dll.bc System.Numerics.dll.bc System.Core.dll.bc nunitlite.dll.bc aot-dummy.dll.bc sdks/out/wasm-runtime-release/lib/libmonosgen-2.0.a sdks/out/wasm-runtime-release/lib/libmono-native.a' 
   ```
### Example 1:
``` bash
mono packager.exe --emscripten-sdkdir=$(EMSCRIPTEN_SDKDIR) --mono-sdkdir=$(TOP)/sdks/out -appdir=bin/aot-mini --nobinding --builddir=obj/aot-mini --aot --template=runtime-tests.js mini_tests.dll
ninja -v -C obj/aot-mini
```

### Example 2:
``` bash
mono packager.exe --emscripten-sdkdir=$(EMSCRIPTEN_SDKDIR) --mono-sdkdir=$(TOP)/sdks/out -appdir=bin/aot-bindings-sample --builddir=obj/aot-bindings-sample --aot --template=runtime.js --link-mode=SdkOnly --asset=sample.html sample.dll
ninja -v -C obj/aot-bindings-sample
```

## AOT Mixed Mode usage: (Experimental)

Same as AOT usage but additional command line options are used.

``` bash
--aot-interp --aot-assemblies=x List of assemblies to AOT in AOT+INTERP mode.
```

