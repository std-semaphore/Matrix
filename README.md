# Matrix

[![CI & Release](https://github.com/std-semaphore/Matrix/actions/workflows/ci.yml/badge.svg)](https://github.com/std-semaphore/Matrix/actions/workflows/ci.yml)
[![NuGet Package Registry](https://img.shields.io/badge/registry-GitHub_Packages-blue.svg)](https://github.com/orgs/std-semaphore/packages?repo_name=Matrix)

A modular library assisting with the creation of PIS information boards. 

## Installation

To consume the packages from the `std-semaphore` organization registry, add a `nuget.config` file to your target project:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="GitHub" value="https://nuget.pkg.github.com/std-semaphore/index.json" />
  </packageSources>
</configuration>
```

Authenticate using a GitHub Personal Access Token (PAT) with `read:packages` permissions.
