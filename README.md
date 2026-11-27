# Sig Companion

A .NET Avalonia application that allows you to monitor your Sigenergy battery and connected solar
using data from the cloud API and local Modbus interface.

![Sig Companion image](./WindowsPackaging/StoreImages/Poster916.png)

Store installation links:
 - [Android (Google Play)](https://play.google.com/store/apps/details?id=com.thirtyhippos.sigcompanion)
 - [Windows (Microsoft Store)](https://apps.microsoft.com/detail/9P4P4R8PHNBM)

Other information:
 - [FAQs](https://github.com/tomhollander/SigCompanion/wiki/FAQs)
 - [Privacy Policy](https://github.com/tomhollander/SigCompanion/wiki/Privacy-Policy)

## Running tests

With the .NET 10 SDK, run the library tests from the repository root:

```sh
dotnet test SigCompanion.Lib.Tests/SigCompanion.Lib.Tests.csproj
```

The energy pricing tests use mocked HTTP responses and do not require API credentials or mobile workloads.
