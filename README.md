# CSCI 526 - Paired Prototype

## Project Overview
This project is a chess-inspired strategy prototype developed for CSCI 526.

The main twist is that players can modify or enhance the movement rules of chess pieces before the battle phase.

Our current custom mechanic is the **Jump Rook**, which can jump over one occupied square while moving horizontally or vertically.

## Game Phases

1. **Edit Phase**
   - Players customize their pieces' movement and attack abilities.

2. **Deploy Phase**
   - Players deploy their pieces within the bottom two rows of their side of the board.

3. **Battle Phase**
   - Players take turns moving and attacking.
   - The current prototype uses capturing the opponent's King as the win condition.

## Current Features

- 8x8 chess board
- Player and enemy turns
- Basic movement and capture system
- King, Rook, and Pawn
- Jump Rook mechanic
- Valid move highlighting
- King capture win condition

## In Progress

- Bishop
- Knight
- Queen
- Edit phase
- Deployment system
- Additional player feedback and UI
- WebGL deployment

## Jump Rook

The Jump Rook moves horizontally or vertically like a normal Rook, but it can jump over one occupied square.

- Can jump over one piece
- Must continue in the same row or column
- Cannot jump over two or more pieces

## Team

- Ellie Roh
- Kuan-Yu Chen

## Development

Built with Unity `6000.3.22f1`.

### Opening the Project

1. Clone this repository.
2. Open Unity Hub.
3. Select **Add project from disk**.
4. Select the cloned repository folder.
5. Open the project using Unity `6000.3.22f1`.
6. Open `Assets/Scenes/SampleScene.unity`.

## Branch Workflow

Please create a separate branch for each feature before making changes.

Examples:

- `ellie_jump_rook`
- `ellie_basic_pieces`

Merge completed and tested features into `main`.