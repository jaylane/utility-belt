# UtilityBelt Development Guidelines

## Design Patterns & Architecture

### Plugin Architecture
- Main plugin class (`UtilityBelt.cs`) serves as the entry point
- Tool-based modular design - each feature is a separate tool class
- Event-driven architecture using Decal event system
- Attribute-based command and expression registration

### Command System
- Commands use regex-based argument parsing
- Support for partial verb matching
- Automatic help generation from attributes
- Examples and usage documentation embedded in code

### Expression Language
- Custom expression language for automation scripting
- Type-safe expression methods with parameter validation
- Integration with game state and tool functions

## Key Guidelines

### Tool Development
- Each tool should be self-contained in `Tools/` directory
- Tools should manage their own settings through the settings system
- Follow the established attribute patterns for commands and expressions
- Use proper error handling and logging

### UI Development
- UI definitions stored as XML in `Views/` directory
- Icons and resources embedded in the project
- Follow existing UI patterns for consistency

### Integration Guidelines
- Respect the game's threading model
- Use proper Decal API patterns for game interaction
- Handle game state changes gracefully
- Minimize performance impact on the game

### Code Quality
- Follow the established .editorconfig style
- Use meaningful variable and method names
- Document complex algorithms and game-specific logic
- Handle edge cases and error conditions

## Contribution Process
1. Fork the repository
2. Create feature branch from master
3. Implement changes following guidelines
4. Test with actual game client
5. Submit merge request to master branch
6. Join Discord for discussion of major changes

## Testing & Deployment
- Manual testing required with Asheron's Call client
- Build process creates Windows installer
- Release versions tagged and published through GitLab CI
- Beta versions available through release candidate process