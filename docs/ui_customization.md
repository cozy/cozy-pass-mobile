# UI customization

This project is based on [Bitwarden Mobile](https://github.com/bitwarden/mobile) and comes with its specific UI

This project has been rebranded using Cozy's colors, logos and texts

This document explains how maintain those modifications

## Cozy Theme

Cozy theme is handled by two files:
- `src\App\Utilities\ThemeManager.cs`
- `src\Android\Utilities\ThemeHelpers.cs`
- `src\App\Styles\Cozy.xaml`
- `src\Android\Resources\values\colors.xml`
- `src\Android\Resources\values\styles.xml`

This list may change in the future, consider it as a hint but not as a single source of trust

## Vector Drawables

Many images and logos are embedded in the projet as `Vector Drawables` instead of classic `png` files

`Vector Drawable` is an Android specific file format that allows to describe pictures as vector graphics. Its extension is `.xml`

This format is similar to SVG so you can easily edit a `Vector Drawable` if you have SVG knowledge

Visual Studio does not support previewing `Vector Drawables`, so Android Studio may be used instead

Another option is to load them into [ShapeShifter](https://shapeshifter.design/) online tool

In order to export pictures from Cozy specifications, two options are available:
- Install [Android Vector Drawable plugin](https://www.figma.com/community/plugin/797369763563831541/Android-Vector-Drawable) on Figma and use it to export pictures
- Export picutres as usual using SVG, import them in [ShapeShifter](https://shapeshifter.design/) and export them as `Vector Drawable`

## Folder `cozy-customization`

`cozy-customization` contains some tools to ease Cozy customization

Those tools are not used anymore