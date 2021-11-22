Web Application
===============

> Common utils, types, handlers, ... for a web application.

## Install

Add following into `paket.dependencies`
```
git ssh://git@bitbucket.lmc.cz:7999/archi/nuget-server.git master Packages: /nuget/
# LMC Nuget dependencies:
nuget Lmc.WebApplication
```

Add following into `paket.references`
```
Lmc.WebApplication
```

## Use
[todo]

## Release
1. Increment version in `WebApplication.fsproj`
2. Update `CHANGELOG.md`
3. Commit new version and tag it
4. Run `$ ./build.sh -t release`
5. Go to `nuget-server` repo, run `./build.sh -t copyAll` and push new versions

## Development
### Requirements
- [dotnet core](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial)
- [FAKE](https://fake.build/fake-gettingstarted.html)

### Build
```bash
./build.sh
```
