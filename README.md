# CSCI 526 - Paired Prototype

A local two-player, chess-inspired strategy game where players spend a limited budget to change how their pieces move and attack before deploying their armies and capturing the opposing King.

Blue and Red share the same computer. The prototype uses King capture as its win condition, rather than checkmate.

## Open and Run

- **Unity Editor:** `6000.3.22f1`
- **Entry scene:** `Assets/Scenes/SampleScene.unity`

1. Clone the repository:

   ```sh
   git clone https://github.com/happysungb/CSCI526.git
   ```

2. In Unity Hub, select **Add project from disk** and choose the folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Open the project with the Unity version above and wait for imports to finish.
4. Open `SampleScene` and press **Play**. The game begins in Blue's customization phase.

## How to Play

### 1. Customize

Each team starts with **$20**. Blue customizes first, then Red.

- Select a piece from **YOUR PIECES**. The central board previews that piece's movement and attack patterns.
- Click a square immediately adjacent to the preview piece to queue a new one-square direction.
- For a sliding direction, click **BUY ∞ ARROW**, then click one of the eight neighboring squares to choose its direction.
- Buy any available piece abilities in **CUSTOM SHOP**.
- **CONFIRM** applies the queued changes and deducts their cost. **UNDO** removes the latest queued change; **CANCEL** clears all unconfirmed changes.
- Confirm or cancel pending changes before selecting another piece or clicking **FINISH TEAM**. Finishing Blue advances to Red; finishing Red starts deployment.

Purchased direction upgrades apply to both movement and capture. Pawns have separate Movement and Attack preview tabs because their starting patterns differ. Kings cannot be customized; the Queen already has all eight sliding directions and has no shop upgrades.

| Upgrade | Cost | Effect |
| --- | ---: | --- |
| One-square direction | $1 | Adds a new adjacent movement/capture direction. |
| Infinite arrow | $4 | Adds a sliding movement/capture direction, subject to board edges and blocking pieces. |
| Pawn Double Step | $1 | Allows a Pawn to move two squares forward on its first move if the path is clear. |
| Jump Rook | $10 | Allows a Rook to cross one occupied square along a rank or file, but not two. |
| Castle Swap | $5 | Allows a Rook and its friendly King to exchange positions if neither has moved. |

### 2. Deploy

- Blue places first. Teams alternate placing one piece at a time.
- Select a piece from the reserve and click an empty highlighted square in your team's two home rows: bottom rows for Blue, top rows for Red.
- **Right-click** to cancel the selected deployment piece.
- Each team's King becomes available after all its other pieces have been deployed.
- After placing the King, use the team's **FINISH** button. Battle begins once both teams have finished deployment.

### 3. Battle

- Blue moves first; teams alternate turns.
- Left-click one of your pieces to select it and see its legal moves, then click a highlighted square or capturable enemy piece.
- Click the selected piece again to deselect it. Clicking an enemy without making a legal capture lets you inspect it.
- To use Castle Swap, select the upgraded Rook or its friendly King, then click the other piece while both are unmoved.
- **Capture the opposing King to win.**

## Implemented Features

- An 8x8 board with King, Queen, Rook, Bishop, Knight, and Pawn pieces for both teams.
- Budget-based customization with previews, pending purchases, confirmation, undo, and cancellation.
- Alternating deployment with a placement preview and King-last rule.
- Turn-based movement, captures, piece inspection, and a King-capture victory screen.
- Piece-specific abilities: Jump Rook, Pawn Double Step, and Castle Swap.
- An ivory/slate board with a visible border, coordinated team colors, amber movement highlights, and side panels positioned relative to the board.

## Prototype Scope

This is a same-computer prototype with simplified chess rules. It does not implement check/checkmate enforcement, pawn promotion, en passant, an AI opponent, or online multiplayer. Castle Swap is a custom ability, not standard chess castling.

## Team

- Ellie Roh
- Kuan-Yu Chen

## Development Workflow

Create a feature branch before making changes. Share it for review, then merge tested changes into `main`.

To review the current UI work:

```sh
git fetch origin
git switch ui-refresh
git pull --ff-only
```

Commit intended source, scene, prefab, and relevant project-setting changes explicitly. The repository's `.gitignore` excludes Unity-generated folders such as `Library`, `Temp`, `Logs`, and `UserSettings`.

For the course's browser submission, build `SampleScene` for Web (WebGL) and host the output on GitHub Pages. Verify the hosted build in a browser before sharing its link.
