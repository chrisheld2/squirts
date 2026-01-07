# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 2D multiplayer game project called "Squirts2D" (mentioned in README.md). The game features:

- **Unity Version**: 6000.1.3f1 (Unity 6)
- **Rendering Pipeline**: Universal Render Pipeline (URP)
- **Target Platforms**: Desktop, WebGL, and Steam
- **Game Type**: 2D maze/adventure game with networking capabilities

## Build and Development Commands

This is a Unity project, so development is primarily done through the Unity Editor:

- **Open Project**: Open the project folder in Unity Hub/Unity Editor
- **Build for WebGL**: Use Unity's Build Settings (File > Build Settings) and select WebGL platform
- **Build for Desktop**: Use Unity's Build Settings and select your target platform (Windows, macOS, Linux)

**Testing**: Unity Test Framework is included (`com.unity.test-framework: 1.5.1` in manifest.json)

**WebGL Builds**: Pre-built WebGL version available in `WebGL Builds/` directory

## Project Architecture

### Core Systems

1. **Game Management (`GAME/Game.cs`)**
   - Main game controller managing states: MENU, PLAYSINGLE, PLAYMULTI, etc.
   - Handles single-player and multiplayer game modes
   - Manages pause/unpause, game initialization, and scene management

2. **Networking System (`NET/` folder)**
   - **NETManager.cs**: Central networking manager with WebSocket support
   - **NETGameObject.cs**: Component for networked game objects
   - Cross-platform networking (Desktop, WebGL, Steam)
   - Custom multiplayer protocol with TCP/UDP messaging
   - Session-based multiplayer with host/guest architecture

3. **Level Generation (`LEVEL/` folder)**
   - **LevelGenerator.cs**: Core level generation system
   - **MazeGenerator.cs**: Maze generation algorithms
   - **MazeCaveGenerator2D.cs**: Cave-style level generation
   - Procedural 2D level creation with various generation methods

4. **Entity System (`Entities/` folder)**
   - Player controller, enemies (Spider, Ghost, Beholder)
   - Interactive objects (Switch, Barrel, Door, Flashlight)
   - Particle effects and environmental elements

5. **Resource Management**
   - **Pooling System**: `GAME/PoolScript.cs` for object pooling
   - **Resources Folder**: Contains prefabs for blocks, foliage, UI elements
   - **Atlas-based Sprites**: Sprite atlas system for optimized rendering

### Key Directories Structure

- `Assets/Scripts/` - All C# scripts organized by system
- `Assets/Resources/` - Runtime-loaded assets (prefabs, configs, build info)
- `Assets/Media/` - Art assets (sprites, audio, fonts)
- `Assets/Materials/` - Unity materials and physics materials
- `Assets/Scenes/` - Unity scene files
- `Assets/Settings/` - URP renderer and pipeline settings

### Configuration and Settings

- **Settings**: JSON-based configuration in `Resources/config.json`
- **Build Configuration**: Build number tracking in `Resources/BuildNumber.txt`
- **Network Settings**: Configurable server addresses and ports for multiplayer
- **Platform Detection**: Automatic platform detection for networking (WebGL vs Desktop vs Steam)

### Development Patterns

- **Singleton Pattern**: Used for managers (Game, NETManager)
- **Component-based Architecture**: Heavy use of Unity's component system
- **Event-driven Communication**: Custom delegates for networking events
- **Object Pooling**: Implemented for performance optimization
- **Cross-platform Networking**: Platform-specific networking implementations

### Important Constants and Enums

- Game states defined in `Game.GAMESTATES` enum
- Network message constants in `NETManager` (TCP/UDP protocol definitions)
- Block types and entity types for level generation

### Key Features

- **Multiplayer Support**: Session-based multiplayer with WebSocket networking
- **Procedural Generation**: Multiple maze and cave generation algorithms
- **Cross-platform**: Supports Desktop, WebGL, and Steam deployment
- **Object Pooling**: Performance optimization for dynamic objects
- **Settings System**: JSON-based configuration management
- **Debug System**: Custom debug logging with color-coded messages (`DL` class)

This codebase follows Unity best practices with clear separation of concerns between game logic, networking, and level generation systems.