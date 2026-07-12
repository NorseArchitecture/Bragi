# Norse.DesignSystem.Stories

A content-only Razor Class Library containing BlazingStory component stories for the Norse Design System.

## Usage

Reference this library in your Blazor project via `NorseRef` in your project file, then use the `AssemblyMarker` anchor type to locate this assembly when configuring BlazingStory's component catalog.

```csharp
var assembly = typeof(AssemblyMarker).Assembly;
```
