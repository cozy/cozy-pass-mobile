# Building the solution

This project is based on [Bitwarden Mobile](https://github.com/bitwarden/mobile) and comes with its own build configurations (`Debug`, `Release`, `FDroid`, `Ad-Hoc`, `AppStore`)

However some edits have been made by Cozy and so we had to modify Bitwarden's build configurations. We made new build configurations that allow us to know which platform (iOS or ANdroid) we are building for, even in `.Net Standard` libraries like `App.csproj`

Those new build configurations are `CozyDebugIOS`, `CozyDebugAndroid`, `CozyReleaseIOS`, `CozyReleaseAndroid`

Native Bitwarden's build configurations are not maintained by Cozy and may not work correctly

Only Cozy's build configurations should be used in this project

## Debug on iOS

In order to debug on iOS:
- Run `iOS.csproj` using `CozyDebugIOS|iPhoneSimulator` or `CozyDebugIOS|iPhone` regarding if you are using a simulator or a real device

## Release on iOS

In order to release on iOS:
- Run `iOS.csproj` using `CozyReleaseIOS|iPhone`

## Debug on Android

In order to debug on Android:
- Copy the Android's `debug.keystore` from Cozy's password-store into `src/Android/debug.keystore`
  - Run `pass show cozy-pass/androidOK/debug.keystore > src/Android/debug.keystore`
  - If you don't have access to Cozy's password-store, just generate a new `debug.keystore` file
    - Run `keytool -genkey -v -keystore src/Android/debug.keystore -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000`
- Run `Android.csproj` using `CozyDebugAndroid|Any CPU` (or just `CozyReleaseAndroid` on `Visual Studio for Mac`)

## Release on Android

In order to debug on Android:
- Run `Android.csproj` using `CozyReleaseAndroid|Any CPU` (or just `CozyReleaseAndroid` on `Visual Studio for Mac`)

# How to handle reference to `Xamarin.iOS.dll`

In Cozy project, we chose to use a native Safari webview for user registration process

To make this possible, we had to import `Xamarin.iOS.dll` which is a iOS native DLL. Therefore it should not be used in Android builds. This is why we made new build configurations ([Building the solution](DEV.md#building-the-solution))

This DLL is imported into `src\App\App.csproj` by using an absolute path

On OSX, this absolute path is independant of VS version nor OSX version

On Windows, this absolute path is dependant of VS version. So we added a conditional `<Reference>` in MSBuild configuration

```xml
<Reference Include="Xamarin.iOS" Condition=" '$(OS)' == 'Windows_NT' AND '$(VisualStudioVersion)' == '16.0' ">
  <HintPath>C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\ReferenceAssemblies\Microsoft\Framework\Xamarin.iOS\v1.0\Xamarin.iOS.dll</HintPath>
</Reference>
```

Current configuration (11/2020) is made for Visual Studio 2019. To make this work in future Visual Studio releases, please duplicate this conditional `<Reference>` and adapt `$(VisualStudioVersion)` and `<HintPath>` with expected values.