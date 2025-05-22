# Nuget ~Preview~ Generator Utility

A utility to help users quickly and easily generate preview NuGet packages and place them in a local repository. This allows for easier debugging when changes need to be made to a NuGet package as part of development in another project.

---

## 🔧 About This Version

This is a customized version of **Nuget Preview Generator**, adapted with the following features:

- ✅ Added configuration option to select build mode.
- ✅ Added error handling to display errors in the log.
- ✅ Added cancel button to allow stopping the process.
- ✅ Added option to enable or disable parallel execution.
- ✅ Added option to build all projects in the solution or individual projects.
- ✅ Support for versions with "Revision" (e.g., `1.2.3.4-test`).
- ✅ Automatically removes previously installed versions of the generated package from the default `/.nuget/packages` folder.
- ❌ Removed feature that modifies the NuGet version — the package is now generated using the version already set.

> This version is intended to provide more flexibility and control for advanced users and teams working with complex solutions.

---

## 🧩 The Problem

When developing projects that depend on internal shared NuGet packages, it can be time-consuming to test changes. Developers often need to:

1. Modify the shared package.
2. Build and generate a new version.
3. Publish the updated package to a local or remote feed.
4. Manually remove the existing version from the NuGet global packages folder (`/.nuget/packages`) to restore the new version.

This process is not only repetitive but error-prone — forgetting to clear out an old version can lead to unexpected behavior during testing.

---

## ✅ The Solution

**Nuget Preview Generator** streamlines this process by letting you:

- Right-click a project in Solution Explorer.
- Generate a **build** of the assembly.
- Deploy it to a **local NuGet repository** on your machine.
- 🔄 Automatically clean out any existing versions of the package from the NuGet global packages cache.

This preview package includes debug symbols and allows you to step into the code during debugging — making development with interdependent NuGet packages faster and easier.

---

## 🚀 Setup

To use the preview generator, you need a local NuGet repository. Don’t worry — it’s just a folder.

### Add a Local NuGet Source

1. Create a folder on your file system (e.g., `C:\LocalNugets`).
2. In **Visual Studio**, go to:
   - `Tools > Options > NuGet Package Manager > Package Sources`
   - Add your folder as a new source.

### Configure NuGet Preview Generator

1. In **Visual Studio**, go to:
   - `Tools > Options > NuGet Package Manager > Nuget Preview Generator`
2. Set the **Local Nuget Repository Folder** to the path you created earlier.

---

## 📦 Generating the Preview

Once setup is complete:

1. Right-click a project in Solution Explorer.
2. Select **Generate Preview Nuget Package**.

> 🔐 Make sure the project has a version defined in the `.csproj` file:

```xml
<PropertyGroup>
  <Version>1.0.1</Version>
  <GeneratePackageOnBuild>True</GeneratePackageOnBuild>
  <TargetFramework>net6.0</TargetFramework>
</PropertyGroup>
```

---

## ⚠️ Deprecated/Modified Features

- ~~Automatic version modification during package generation.~~  
  ✅ Now, the package uses the version already defined in the project.

---

## 🙏 Acknowledgements

This tool is a customized fork of the original project by **voidsoft**, available here:  
🔗 [voidsoft/nugethelper](https://github.com/voidsoft/nugethelper)

Thanks to the original author for providing the foundation and inspiration for this improved version.
