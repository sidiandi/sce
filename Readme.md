# sce - Self Compiling Executable

Creates a self compiling C# executable. Runs with .NET 4.6.1.

Instructions:

* Rename sce.exe to yourExecutableName.exe
* Run yourExecutableName.exe
* The directory yourExecutableName.exe.src will be created, containing the sources for yourExecutableName.exe
* Make modifications to the source code
* Run yourExecutableName.exe again. yourExecutableName.exe will detect the changes made in the source code and re-compile itself

## nuget References

Nuget packages will be automatically downloaded and referenced by adding special comment lines to the top of a *.cs file:

~~~~
// nuget-source: https://api.nuget.org/
// nuget-package: log4net
// nuget-package: sidi-util
// nuget-package: Nunit
~~~~

If no `nuget-source` is mentioned, `https://api.nuget.org/` will be used as default.

## watch Mode

When invoked with
~~~~
sce.exe watch
~~~~
, the executable will constantly monitor the source files and compile and run on any change.

## To Do
* When name ist sce.exe, the program should prompt the user for a script name and generate it.
* support nuget from v2 and file sources
* load nuget dependencies automatically
