# Canvas Menu Setup Instructions

This guide will help you wire up the Canvas GameObject with all the menus to the current game code.

## Overview

The Canvas has been copied from an older repo and contains a complete menu system with:
- Main Menu (Single Player, Multiplayer, Settings, Credits, Quit)
- Multiplayer submenu (Host, Join, Back)
- Session Key input panel
- Game UI (toolbar and debug panel)

## Scripts Updated

The following scripts have been updated to work with the current code:

1. **PanelMainMenuContainerScript.cs** - Main menu controller
2. **SessionKeyScript.cs** - Multiplayer session key input
3. **PanelDebugScript.cs** - Already working correctly
4. **GameMain.cs** - Already has the necessary fields

## Unity Inspector Setup Steps

### Step 1: Canvas GameObject Setup

1. Open your main game scene (likely `Assets/Scenes/Game.unity`)
2. Find the `Canvas` GameObject in the hierarchy
3. Make sure it has a `PanelMainMenuContainerScript` component attached

### Step 2: PanelMainMenuContainerScript Configuration

Select the `Canvas > PanelMainMenuContainer` GameObject and configure the following fields in the Inspector:

#### Game Reference:
- **Game**: Drag the `GameMain` GameObject (or the GameObject that has the GameMain component)

#### Fields - Main Menu:
Find the buttons in the hierarchy under `Canvas > PanelMainMenuContainer > PanelMain`:
- **Button Single Player**: Drag `ButtonSinglePlayer`
- **Button Multi Player**: Drag `ButtonMultiPlayer`
- **Button Settings**: Drag `ButtonSettings`
- **Button Credits**: Drag `ButtonCredits`

#### Fields - Multiplayer:
Find the multiplayer panel and buttons:
- **Panel Multiplayer**: Drag `PanelMultiplayer` GameObject
- **Button Host**: Drag the Host button from `PanelMultiplayer`
- **Button Join**: Drag the Join button from `PanelMultiplayer`
- **Button Back**: Drag the Back button from `PanelMultiplayer`

#### Fields - Panels:
Configure the main panel references:
- **Panel Main**: Drag `PanelMain` (the main menu panel)
- **Panel Settings**: Drag `PanelSettings` (if it exists, otherwise leave empty)
- **Panel Credits**: Drag `PanelCredits` (if it exists, otherwise leave empty)
- **Panel Session Key**: Drag `PanelSessionKey` GameObject

#### Fields - Common:
- **Button Quit**: Drag the Quit button
- **Text Build Number**: Drag the TextMeshProUGUI component that displays the build number

### Step 3: SessionKeyScript Configuration

Select the `Canvas > PanelSessionKey` GameObject and configure:

- **Game**: Drag the `GameMain` GameObject
- **Menu Container**: Drag the `PanelMainMenuContainer` GameObject
- **Button Cancel**: Drag the Cancel button from within PanelSessionKey
- **Button Connect**: Drag the Connect button from within PanelSessionKey

### Step 4: GameMain Configuration

Select the GameObject with the `GameMain` component and verify these fields are set:

- **Canvas**: Drag the main `Canvas` GameObject
- **Panel Main Menu Container**: Drag `Canvas > PanelMainMenuContainer`
- **Panel Game UI Container**: Drag `Canvas > PanelGameUIContainer`
- **Panel Debug Script**: Drag the GameObject with `PanelDebugScript` component
- **Net Manager**: Should already be set to your NETManager
- **Level Draw**: Should already be set to your level generation script
- **Main Camera**: Should already be set to your main camera

### Step 5: Verify Canvas Hierarchy

Make sure your Canvas hierarchy looks like this:

```
Canvas
├── PanelMainMenuContainer
│   ├── PanelMain (Main menu with 5 buttons)
│   │   ├── ButtonSinglePlayer
│   │   ├── ButtonMultiPlayer
│   │   ├── ButtonSettings
│   │   ├── ButtonCredits
│   │   └── ButtonQuit
│   ├── PanelMultiplayer (Multiplayer submenu)
│   │   ├── ButtonHost
│   │   ├── ButtonJoin
│   │   └── ButtonBack
│   ├── PanelSettings (optional)
│   ├── PanelCredits (optional)
│   └── TextBuildNumber (TextMeshProUGUI)
├── PanelSessionKey
│   ├── InputFieldSessionKey
│   ├── ButtonCancel
│   └── ButtonConnect
├── PanelGameUIContainer
│   ├── PanelGameToolBar
│   └── PanelDebug
│       ├── TextSessionGUID
│       └── TextClientGUID
└── SplashScreen (inactive)
```

## How It Works

### Menu Flow

1. **Main Menu** → Shows on game start
   - Single Player (S key) → Calls `GameMain.GameStart_SinglePlayer()`
   - Multiplayer (M key) → Shows multiplayer submenu
   - Settings (T key) → Shows settings panel (needs implementation)
   - Credits (C key) → Shows credits panel (needs implementation)
   - Quit (Q key) → Calls `GameMain.GameQuit()`

2. **Multiplayer Submenu**
   - Host (H key) → Calls `GameMain.GameStart_Multiplayer()` with new session
   - Join (J key) → Shows session key input panel
   - Back (B key) → Returns to main menu

3. **Session Key Panel**
   - Enter session key → Press Enter or click Connect
   - Cancel → Press Escape or click Cancel
   - Connects to game with `GameMain.GameStart_Multiplayer(sessionKey)`

### Pause/Menu Toggle

- **Escape** → In single player: pauses game and shows menu
- **P** → Pauses/unpauses game without showing menu
- **Escape** (in multiplayer) → Shows menu without pausing (only host can pause)

## Testing Checklist

After setup, test the following:

- [ ] Game starts with main menu visible
- [ ] Single Player button starts single player game
- [ ] Multiplayer button shows multiplayer submenu
- [ ] Host button starts multiplayer as host
- [ ] Join button shows session key panel
- [ ] Session key panel accepts input and connects
- [ ] Cancel button on session key panel returns to multiplayer menu
- [ ] Back button returns to main menu
- [ ] Quit button exits the game
- [ ] Keyboard shortcuts work (S, M, T, C, Q, H, J, B)
- [ ] Escape key toggles pause/menu appropriately
- [ ] Game UI shows during gameplay
- [ ] Debug panel shows Session GUID and Client GUID in multiplayer

## Troubleshooting

### Buttons Don't Work
- Make sure all button references are set in the Inspector
- Check that `PanelMainMenuContainerScript` has all fields assigned
- Verify that the `GameMain` reference is set correctly

### Session Key Panel Doesn't Show
- Check that `panelSessionKey` field is assigned in `PanelMainMenuContainerScript`
- Verify the panel GameObject exists in the hierarchy

### Menu Doesn't Hide During Gameplay
- Check that `GameMain.GamePause()` is being called correctly
- Verify that `panelMainMenuContainer` and `panelGameUIContainer` are properly assigned in GameMain

### Debug Info Not Showing
- Make sure `PanelDebugScript` is attached to the correct GameObject
- Verify that `TextSessionGUID` and `TextClientGUID` are children of the PanelDebug GameObject
- Check that `panelDebugScript` is assigned in GameMain

## Next Steps

After basic setup works:

1. Implement Settings panel functionality
2. Implement Credits panel functionality
3. Add visual feedback for button hovers
4. Add sound effects for button clicks
5. Add transitions between menu states

## Additional Notes

- The menu system uses a state machine pattern (`MenuState` enum)
- All button listeners are set up in code (no need to configure in Inspector)
- The system supports both mouse and keyboard input
- Build number is automatically loaded from `Resources/BuildNumber.txt`
- Session keys are saved to PlayerPrefs for convenience
