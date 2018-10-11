# mybatisnet-migrations

An dotnet port of the Java mybatis/migrations project written in F#

The project is currently in an early phase of development

## Goals

- provide a library with the functionality of the Java mybatis-migrations project that targets the .NET runtime
- provide a dotnet-cli tool integration of the mybatis command-line application
- target netstandard and netcore runtimes with priority, also provide net-framework-compatible binaries

## Dependencies

- [VDimensions MSBuild SDK](https://github.com/vdimensions/vdimensions_msbuild_sdk) 
  Currently the project requires the `VDimensiosn MSBuild SDK` as a git submodule dependency, so use --recurse-submodules when cloning.  
  The VDimensions MSBuild SDK is a collection of MSBuild imports that aid the support for dotnet-standard/dotnet core by introducing useful preprocessor directives.  
  In addition some F#-specific workarounds are also provided by it, such as support for auto-generated assmebly-level properties on non-msbuild buidl engines (for instance xbuild)
