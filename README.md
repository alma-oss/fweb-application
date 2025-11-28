Web Application
===============

[![NuGet](https://img.shields.io/nuget/v/Alma.WebApplication.svg)](https://www.nuget.org/packages/Alma.WebApplication)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Alma.WebApplication.svg)](https://www.nuget.org/packages/Alma.WebApplication)
[![Tests](https://github.com/alma-oss/fweb-application/actions/workflows/tests.yaml/badge.svg)](https://github.com/alma-oss/fweb-application/actions/workflows/tests.yaml)

> Common utils, types, handlers, ... for a web application.

## Install

Add following into `paket.references`
```
Alma.WebApplication
```

## Release
1. Increment version in `WebApplication.fsproj`
2. Update `CHANGELOG.md`
3. Commit new version and tag it

## Development
### Requirements
- [dotnet core](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial)

### Build
```bash
./build.sh build
```

### Tests
```bash
./build.sh -t tests
```
