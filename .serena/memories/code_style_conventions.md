# UtilityBelt Code Style & Conventions

## Code Style (from .editorconfig)

### Indentation & Formatting
- **Indent Style**: Spaces (4 spaces per indent)
- **Tab Width**: 4 spaces
- **End of Line**: CRLF (Windows style)
- **Braces**: Same line (`csharp_new_line_before_open_brace = none`)
- **Final Newline**: Not required

### C# Conventions
- **var Usage**: Explicit types preferred (`csharp_style_var_elsewhere = false`)
- **this/Me Qualification**: Not required (`dotnet_style_qualification_for_* = false`)
- **Expression Bodies**: Used for properties, accessors, indexers, lambdas
- **Braces**: Always required (`csharp_prefer_braces = true`)
- **Pattern Matching**: Preferred over cast checks
- **Null Checking**: Prefer null-conditional operators

### Naming Conventions
- **Interfaces**: Must begin with 'I' (IPascalCase)
- **Types**: PascalCase (classes, structs, enums)
- **Members**: PascalCase (properties, events, methods)
- **Language Keywords**: Preferred over BCL types (`int` vs `Int32`)

### Modifier Order
```csharp
public, private, protected, internal, static, extern, new, virtual, 
abstract, sealed, override, readonly, unsafe, volatile, async
```

## Project-Specific Patterns

### Command Attributes
```csharp
[CommandPattern("commandname", @"^pattern$")]
[Usage("/ub commandname - description")]
[Example("/ub commandname", "Example description")]
[Summary("Brief summary")]
public void CommandMethod(string command, Match args) { }
```

### Expression Method Attributes
```csharp
[ExpressionMethod("methodname")]
[ExpressionParameter(0, typeof(string), "description")]
[ExpressionReturn(typeof(bool), "description")]
public bool ExpressionMethod(string param) { }
```

### Tool Organization
- Each tool is a separate class in the `Tools/` directory
- Tools typically inherit from a base tool class
- Settings are managed through a centralized settings system