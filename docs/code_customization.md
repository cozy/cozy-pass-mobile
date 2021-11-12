# UI customization

This project is based on [Bitwarden Mobile](https://github.com/bitwarden/mobile) and comes with its specific UI

Some part of this project may have been reworked by Cozy to implement new features or to modify behavior of existing ones

To make those modifications more tracable, a code documentation pattern may be used

## How to comment customization

Always precede customized code with a comment that explains why the customization has been made

Exemple of commented customization in C#:

```cs
// Cozy customization, modify "XXX" functionality
// We did this because of that
SomeCozyOverride();
```

The only mandatory text is `// Cozy Customization`. This would ease future searches in the code in order to find all customizations

:warning: some old customizations do not have this comment format. So searches may not find all historic customizations. If you find those old customization, please fix them by adding the new comment format

## How to comment deleted code

In order to avoid conflicts on future merge upstreams, we should never delete Bitwarden code

Instead we should surround it by block comments tags

Also we should always comment why a specific block has been deactivated

Exemple of deleted code in C#:

```cs
// Cozy customization, disable "XXX" functionality
// We did this because of that
/*
SomeBitwardenMethod();
AnotherBitwardenMethod();
//*/
```

With this way of commenting code has been: the original Bitwarden is kept untouched (indentation should also stay the same) and future merge upstreams will not trigger merge conflicts on those lines

Also it is easy to reactivate a block of code for testing purpose just by adding a `/` before block comment tag

```cs
// Cozy customization, disable "XXX" functionality
// We did this because of that
//*
SomeBitwardenMethod();
AnotherBitwardenMethod();
//*/
```

The same pattern should be used on `xml` resources:

```xml
<!--
Cozy customisation: disable "XXX" functionality
We did this because of that
-->
<!--
<SomeXMLTag>
  <SomeXMLChildTag>SomeText</SomeXMLChildTag>
</SomeXMLTag>
-->
```

However there is no way to easily switch XML blocks comment. Those should be deleted when tests are done

## How to replace code by another

The same way we don't want to edit deleted code, we don't want to edit big chunk of codes too much

When this is needed two option are available:
- edit the code and precede each modified line by a comment
- create a whold new chunk of code will all edits and comment the previous one

Exemple of replaced chunk in C# using code comment switch structure

```cs
// Cozy customization, replace "XXX" functionality
// We did this because of that
//*
SomeCozyOverride();
/*/
SomeBitwardenCode();
//*/
```

With this way of commenting code has been: the original Bitwarden is kept untouched (indentation should also stay the same) and future merge upstreams will not trigger merge conflicts on those lines

Also it is easy to reactivate a block of code for testing purpose just by removing the first `/` from the block comment tag

```cs
// Cozy customization, replace "XXX" functionality
// We did this because of that
/*
SomeCozyOverride();
/*/
SomeBitwardenCode();
//*/
```