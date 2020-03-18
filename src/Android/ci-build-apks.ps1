﻿$rootPath = $env:APPVEYOR_BUILD_FOLDER;

$androidPath = $($rootPath + "\src\Android\Android.csproj");
$appPath = $($rootPath + "\src\App\App.csproj");

echo "########################################"
echo "##### Increment Version"
echo "########################################"

$androidManifest = $($rootPath + "\src\Android\Properties\AndroidManifest.xml");

$xml=New-Object XML;
$xml.Load($androidManifest);

$node=$xml.SelectNodes("/manifest");
$node.SetAttribute("android:versionCode", $env:APPVEYOR_BUILD_NUMBER);

$xml.Save($androidManifest);

echo "########################################"
echo "##### Decrypt Keystore"
echo "########################################"

$encKeystorePath = $($rootPath + "\src\Android\8bit.keystore.enc");
$secureFilePath = $($rootPath + "\secure-file\tools\secure-file.exe");

Invoke-Expression "& `"$secureFilePath`" -decrypt $($encKeystorePath) -secret $($env:keystore_dec_secret)"

echo "########################################"
echo "##### Sign Release Configuration"
echo "########################################"

msbuild "$($androidPath)" "/t:SignAndroidPackage" "/p:Configuration=Release" "/p:AndroidKeyStore=true" `
    "/p:AndroidSigningKeyAlias=bitwarden" "/p:AndroidSigningKeyPass=$($env:keystore_password)" `
    "/p:AndroidSigningKeyStore=8bit.keystore" "/p:AndroidSigningStorePass=$($env:keystore_password)" "/v:quiet"
	
echo "########################################"
echo "##### Copy Release apk to project root"
echo "########################################"

$signedApkPath = $($rootPath + "\src\Android\bin\Release\io.cozy.pass-Signed.apk");
$signedApkDestPath = $($rootPath + "\io.cozy.pass.apk");

Copy-Item $signedApkPath $signedApkDestPath

echo "########################################"
echo "##### Clean Android and App"
echo "########################################"

msbuild "$($androidPath)" "/t:Clean" "/p:Configuration=FDroid"
msbuild "$($appPath)" "/t:Clean" "/p:Configuration=FDroid"

echo "########################################"
echo "##### Backup project files"
echo "########################################"

Copy-Item $androidManifest $($androidManifest + ".original");
Copy-Item $androidPath $($androidPath + ".original");
Copy-Item $appPath $($appPath + ".original");


echo "########################################"
echo "##### Cleanup Android Manifest"
echo "########################################"

$xml=New-Object XML;
$xml.Load($androidManifest);

$nsAndroid=New-Object System.Xml.XmlNamespaceManager($xml.NameTable);
$nsAndroid.AddNamespace("android", "http://schemas.android.com/apk/res/android");

$firebaseReceiver1=$xml.SelectSingleNode(`
    "/manifest/application/receiver[@android:name='com.google.firebase.iid.FirebaseInstanceIdInternalReceiver']", `
    $nsAndroid);
$firebaseReceiver1.ParentNode.RemoveChild($firebaseReceiver1);

$firebaseReceiver2=$xml.SelectSingleNode(`
    "/manifest/application/receiver[@android:name='com.google.firebase.iid.FirebaseInstanceIdReceiver']", `
    $nsAndroid);
$firebaseReceiver2.ParentNode.RemoveChild($firebaseReceiver2);

$xml.Save($androidManifest);

echo "########################################"
echo "##### Uninstall from Android.csproj"
echo "########################################"

$xml=New-Object XML;
$xml.Load($androidPath);

$ns=New-Object System.Xml.XmlNamespaceManager($xml.NameTable);
$ns.AddNamespace("ns", $xml.DocumentElement.NamespaceURI);

$firebaseNode=$xml.SelectSingleNode(`
    "/ns:Project/ns:ItemGroup/ns:PackageReference[@Include='Xamarin.Firebase.Messaging']", $ns);
$firebaseNode.ParentNode.RemoveChild($firebaseNode);

$safetyNetNode=$xml.SelectSingleNode(`
    "/ns:Project/ns:ItemGroup/ns:PackageReference[@Include='Xamarin.GooglePlayServices.SafetyNet']", $ns);
$safetyNetNode.ParentNode.RemoveChild($safetyNetNode);

$xml.Save($androidPath);

echo "########################################"
echo "##### Uninstall from App.csproj"
echo "########################################"

$xml=New-Object XML;
$xml.Load($appPath);

$hockeyNode=$xml.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='HockeySDK.Xamarin']");
$hockeyNode.ParentNode.RemoveChild($hockeyNode);

$xml.Save($appPath);

echo "########################################"
echo "##### Restore NuGet"
echo "########################################"

Invoke-Expression "& nuget restore"

echo "########################################"
echo "##### Build and Sign FDroid Configuration"
echo "########################################"

msbuild "$($androidPath)" "/logger:C:\Program Files\AppVeyor\BuildAgent\Appveyor.MSBuildLogger.dll" `
    "/p:Configuration=FDroid"
msbuild "$($androidPath)" "/t:SignAndroidPackage" "/p:Configuration=FDroid" "/p:AndroidKeyStore=true" `
    "/p:AndroidSigningKeyAlias=bitwarden" "/p:AndroidSigningKeyPass=$($env:keystore_password)" `
    "/p:AndroidSigningKeyStore=8bit.keystore" "/p:AndroidSigningStorePass=$($env:keystore_password)" "/v:quiet"
	
echo "########################################"
echo "##### Copy FDroid apk to project root"
echo "########################################"

$signedApkPath = $($rootPath + "\src\Android\bin\FDroid\io.cozy.pass-Signed.apk");
$signedApkDestPath = $($rootPath + "\io.cozy.pass-fdroid.apk");

Copy-Item $signedApkPath $signedApkDestPath

echo "########################################"
echo "##### Done"
echo "########################################"
