
[Cozy] Pass 
=======================


What's Cozy?
------------

![Cozy Logo](https://cdn.rawgit.com/cozy/cozy-guidelines/master/templates/cozy_logo_small.svg)

[Cozy] is a platform that brings all your web services in the same private space.  With it, your webapps and your devices can share data easily, providing you with a new experience. You can install Cozy on your own hardware where no one's tracking you.


# What's Cozy Pass

Cozy Pass remembers and synchronises all your passwords for you. By installing the password manager, your digital life will be more secure and simple.


<a href="https://play.google.com/store/apps/details?id=io.cozy.pass" target="_blank"><img alt="Get it on Google Play" src="https://imgur.com/YQzmZi9.png" width="153" height="46"></a> <a href="https://apps.apple.com/us/app/cozy-pass/id1502262449" target="_blank"><img src="https://imgur.com/GdGqPMY.png" width="135" height="40"></a>


Cozy Pass is built on [Bitwarden](https://github.com/bitwarden/mobile)
# Build/Run

Please refer to the [Mobile section](https://contributing.bitwarden.com/mobile/) of the [Contributing Documentation](https://contributing.bitwarden.com/) for build instructions, recommended tooling, code style tips, and lots of other great information to get you started.

The Cozy Pass mobile application is written in C# with Xamarin Android, Xamarin iOS, and Xamarin Forms.


**Requirements**

- [Visual Studio](https://visualstudio.microsoft.com/)
- [Xamarin](https://docs.microsoft.com/en-us/xamarin/get-started/installation/?pivots=windows)

**Run the app**

- Open the solution file in Visual Studio.
- Restore the nuget packages (Menu Project > Restore NuGet packages).
- Build and run the app.

# Contribute

Code contributions are welcome! Please commit any pull requests against the `master` branch. Learn more about how to contribute by reading the [Contributing Guidelines](https://contributing.bitwarden.com/contributing/). Check out the [Contributing Documentation](https://contributing.bitwarden.com/) for how to get started with your first contribution.

Security audits and feedback are welcome. Please open an issue or email us privately if the report is sensitive in nature. You can read our security policy in the [`SECURITY.md`](SECURITY.md) file.

### Dotnet-format

We recently migrated to using dotnet-format as code formatter. All previous branches will need to updated to avoid large merge conflicts using the following steps:

1. Check out your local Branch
2. Run `git merge e0efcfbe45b2a27c73e9593bfd7a71fad2aa7a35`
3. Resolve any merge conflicts, commit.
4. Run `dotnet tool run dotnet-format`
5. Commit
6. Run `git merge -Xours 04539af2a66668b6e85476d5cf318c9150ec4357`
7. Push
