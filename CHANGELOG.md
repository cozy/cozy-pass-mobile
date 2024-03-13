# 3.0.6

## ✨ Features


## 🐛 Bug Fixes


## 🔧 Tech


# 3.0.5

## ✨ Features

* Set OAuth client name in the form of `Cozy Pass (device name)`` to ease identification in cozy-settings ([PR #89](https://github.com/cozy/cozy-pass-mobile/pull/89))

## 🐛 Bug Fixes

* Correctly remove the app from cozy-settings' connected devices list on logout ([PR #89](https://github.com/cozy/cozy-pass-mobile/pull/89) and [PR #92](https://github.com/cozy/cozy-pass-mobile/pull/92))

## 🔧 Tech

* Implement ClouderyEnv override using links ([PR #90](https://github.com/cozy/cozy-pass-mobile/pull/90))
* Add universal links support to Android and iOS ([PR #91](https://github.com/cozy/cozy-pass-mobile/pull/91))

# 3.0.4

## ✨ Features

* Rework of the login UI ([PR #86](https://github.com/cozy/cozy-pass-mobile/pull/86))

## 🐛 Bug Fixes

* Prevent app crash when scrolling in cipers and settings pages in iOS 15+ ([PR #77](https://github.com/cozy/cozy-pass-mobile/pull/77))
* Disable the Register button on HomePage ([PR #84](https://github.com/cozy/cozy-pass-mobile/pull/84))

## 🔧 Tech

* Upgrade Android SDK to 12.1 ([PR #87](https://github.com/cozy/cozy-pass-mobile/pull/87))

# 3.0.3

## ✨ Features

## 🐛 Bug Fixes

* Prevent app crash when scrolling in cipers and settings pages in iOS 15+ ([PR #77](https://github.com/cozy/cozy-pass-mobile/pull/77))
* Disable the Register button on HomePage ([PR #84](https://github.com/cozy/cozy-pass-mobile/pull/84))

## 🔧 Tech

# 3.0.2

## ✨ Features

## 🐛 Bug Fixes

* Prevent app crash when opened from URL scheme in iOS 15+ ([PR #75](https://github.com/cozy/cozy-pass-mobile/pull/75))

## 🔧 Tech

# 3.0.1

## ✨ Features

## 🐛 Bug Fixes

* Force image crop on iOS on `unlock` screen ([PR #71](https://github.com/cozy/cozy-pass-mobile/pull/71))
* Fix iOS `autofill` theme ([PR #73](https://github.com/cozy/cozy-pass-mobile/pull/73))

## 🔧 Tech

# 3.0.0

## ✨ Features

* Rework of the `onboarding` screen ([PR #54](https://github.com/cozy/cozy-pass-mobile/pull/54))
* Rework of the `login` screen ([PR #54](https://github.com/cozy/cozy-pass-mobile/pull/54) and [PR #59](https://github.com/cozy/cozy-pass-mobile/pull/59))
* Rework of the `unlock` screen ([PR #54](https://github.com/cozy/cozy-pass-mobile/pull/54), [PR #57](https://github.com/cozy/cozy-pass-mobile/pull/57) and [PR #60](https://github.com/cozy/cozy-pass-mobile/pull/60))
* Improve login form with specific error when user misspells `Cozy` with `Cosy`, with automatic handling of `https` and default `mycozy.cloud` domain  ([PR #52](https://github.com/cozy/cozy-pass-mobile/pull/52))
* Add ability to move a cipher into a folder ([PR #50](https://github.com/cozy/cozy-pass-mobile/pull/50), [PR #56](https://github.com/cozy/cozy-pass-mobile/pull/56), [PR #61](https://github.com/cozy/cozy-pass-mobile/pull/61), [PR #63](https://github.com/cozy/cozy-pass-mobile/pull/63) and [PR #67](https://github.com/cozy/cozy-pass-mobile/pull/67))
* Retrieves latest features from Bitwarden's project from 05/11/2020 to 22/10/2021 ([PR #45](https://github.com/cozy/cozy-pass-mobile/pull/45), [PR #48](https://github.com/cozy/cozy-pass-mobile/pull/48), [PR #55](https://github.com/cozy/cozy-pass-mobile/pull/55), [PR #58](https://github.com/cozy/cozy-pass-mobile/pull/58) and [PR #66](https://github.com/cozy/cozy-pass-mobile/pull/66))
* Some rewording ([PR #49](https://github.com/cozy/cozy-pass-mobile/pull/49))
* Add a `Description` field on Cipher's edition view (a.k.a `Notes`) ([PR #68](https://github.com/cozy/cozy-pass-mobile/pull/68))

## 🐛 Bug Fixes

* Fix `Import` button from setting's screen ([PR #47](https://github.com/cozy/cozy-pass-mobile/pull/47) and [PR #65](https://github.com/cozy/cozy-pass-mobile/pull/65))
* Fix incorrect truncation on Cipher's description ([PR #53](https://github.com/cozy/cozy-pass-mobile/pull/53))
* Fix incorrect truncation on Setting's view entries ([PR #64](https://github.com/cozy/cozy-pass-mobile/pull/64))

## 🔧 Tech

* Add technical documentation ([PR #46](https://github.com/cozy/cozy-pass-mobile/pull/46))
* Resolve some compilation warnings ([PR #51](https://github.com/cozy/cozy-pass-mobile/pull/51))
