using UnityEngine;
using UnityEngine.InputSystem;

public class ChessGameManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject tilePrefab;
    public GameObject piecePrefab;

    [Header("Board Settings")]
    [SerializeField] private int boardSize = 8;

    [Header("Piece Colors")]
    [SerializeField] private Color playerColor = Color.blue;
    [SerializeField] private Color enemyColor = Color.red;

    [Header("Move Highlight")]
    [SerializeField]
    private Color validMoveHighlightColor = new Color(239f / 255f, 1f, 0f, 1f);

    private GameObject[,] gridArray;
    private Color[,] originalTileColors;
    private ChessPiece[,] pieces;
    private ChessPiece selectedPiece;
    private PieceTeam currentTurn = PieceTeam.Player;
    private bool gameOver;
    private string resultMessage = "";

    private void Start()
    {
        gridArray = new GameObject[boardSize, boardSize];
        pieces = new ChessPiece[boardSize, boardSize];
        originalTileColors = new Color[boardSize, boardSize];
        GenerateChessBoard();
        SpawnInitialPieces();
    }

    private void Update()
    {
        if (gameOver)
        {
            return;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleMouseClick();
        }
    }

    private void GenerateChessBoard()
    {
        Color darkTileColor = new Color(0.25f, 0.25f, 0.25f);

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                Vector3 tilePosition = new Vector3(x, y, 0f);
                GameObject newTile = Instantiate(
                    tilePrefab,
                    tilePosition,
                    Quaternion.identity,
                    transform
                );

                newTile.name = $"Tile_{x}_{y}";
                newTile.tag = "Tile";

                SpriteRenderer tileRenderer =
                    newTile.GetComponent<SpriteRenderer>();

                if (tileRenderer != null)
                {
                    Color tileColor =
                        (x + y) % 2 == 0
                            ? darkTileColor
                            : Color.white;

                    tileRenderer.color = tileColor;
                    originalTileColors[x, y] = tileColor;
                }

                gridArray[x, y] = newTile;
            }
        }
    }

    private void SpawnInitialPieces()
    {
        SpawnTeamFormation(
            PieceTeam.Player,
            0,
            1,
            playerColor,
            "Blue"
        );

        SpawnTeamFormation(
            PieceTeam.Enemy,
            boardSize - 1,
            boardSize - 2,
            enemyColor,
            "Red"
        );
    }

    private void SpawnTeamFormation(
        PieceTeam team,
        int backRow,
        int pawnRow,
        Color color,
        string teamName
    )
    {
        CreatePiece(
            $"{teamName}_Rook_Left",
            team,
            PieceType.Rook,
            new Vector2Int(0, backRow),
            color
        );

        CreatePiece(
            $"{teamName}_King",
            team,
            PieceType.King,
            new Vector2Int(boardSize / 2, backRow),
            color
        );

        CreatePiece(
            $"{teamName}_Rook_Right",
            team,
            PieceType.Rook,
            new Vector2Int(boardSize - 1, backRow),
            color
        );

        int centerColumn = boardSize / 2;

        for (int x = centerColumn - 1; x <= centerColumn + 1; x++)
        {
            CreatePiece(
                $"{teamName}_Pawn_{x}",
                team,
                PieceType.Pawn,
                new Vector2Int(x, pawnRow),
                color
            );
        }
    }

    private ChessPiece CreatePiece(
        string pieceName,
        PieceTeam team,
        PieceType type,
        Vector2Int boardPosition,
        Color color
    )
    {
        Vector3 worldPosition = BoardToWorld(boardPosition);
        GameObject newPieceObject = Instantiate(
            piecePrefab,
            worldPosition,
            Quaternion.identity
        );

        newPieceObject.name = pieceName;

        ChessPiece newPiece =
            newPieceObject.GetComponent<ChessPiece>();

        if (newPiece == null)
        {
            newPiece = newPieceObject.AddComponent<ChessPiece>();
        }

        newPiece.Initialize(team, type, boardPosition, color);
        pieces[boardPosition.x, boardPosition.y] = newPiece;

        return newPiece;
    }

    private void HandleMouseClick()
    {
        if (Camera.main == null || Mouse.current == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, 0f)
        );

        Collider2D[] clickedColliders =
            Physics2D.OverlapPointAll(worldPosition);
        ChessPiece clickedPiece = FindClickedPiece(clickedColliders);

        if (clickedPiece != null)
        {
            HandlePieceClick(clickedPiece);
            return;
        }

        GameObject clickedTile = FindClickedTile(clickedColliders);

        if (clickedTile != null && selectedPiece != null)
        {
            Vector2Int targetPosition = WorldToBoard(
                clickedTile.transform.position
            );

            TryMoveSelectedPiece(targetPosition);
        }
    }

    private ChessPiece FindClickedPiece(Collider2D[] clickedColliders)
    {
        foreach (Collider2D clickedCollider in clickedColliders)
        {
            ChessPiece piece =
                clickedCollider.GetComponentInParent<ChessPiece>();

            if (piece != null)
            {
                return piece;
            }
        }

        return null;
    }

    private GameObject FindClickedTile(Collider2D[] clickedColliders)
    {
        foreach (Collider2D clickedCollider in clickedColliders)
        {
            if (clickedCollider.CompareTag("Tile"))
            {
                return clickedCollider.gameObject;
            }
        }

        return null;
    }

    private void HandlePieceClick(ChessPiece clickedPiece)
    {
        if (clickedPiece.Team == currentTurn)
        {
            SelectPiece(clickedPiece);
            return;
        }

        if (selectedPiece != null)
        {
            TryMoveSelectedPiece(clickedPiece.BoardPosition);
        }
    }

    private void SelectPiece(ChessPiece piece)
    {
        if (selectedPiece != null)
        {
            selectedPiece.transform.localScale = Vector3.one;
        }

        selectedPiece = piece;
        selectedPiece.transform.localScale =
            new Vector3(1.2f, 1.2f, 1f);
        HighlightValidMoves(selectedPiece);
    }

    private void HighlightValidMoves(ChessPiece piece)
    {
        ClearMoveHighlights();

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                Vector2Int targetPosition = new Vector2Int(x, y);
                ChessPiece targetPiece = pieces[x, y];

                if (IsLegalMove(piece, targetPosition, targetPiece))
                {
                    SpriteRenderer tileRenderer =
                        gridArray[x, y].GetComponent<SpriteRenderer>();

                    if (tileRenderer != null)
                    {
                        Color baseColor = originalTileColors[x, y];

                        tileRenderer.color = Color.Lerp(
                            baseColor,
                            validMoveHighlightColor,
                            0.4f
                        );
                    }
                }
            }
        }
    }

    private void ClearMoveHighlights()
    {
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                SpriteRenderer tileRenderer =
                    gridArray[x, y].GetComponent<SpriteRenderer>();

                if (tileRenderer != null)
                {
                    tileRenderer.color = originalTileColors[x, y];
                }
            }
        }
    }

    private void TryMoveSelectedPiece(Vector2Int targetPosition)
    {
        if (selectedPiece == null || !IsInsideBoard(targetPosition))
        {
            return;
        }

        ChessPiece targetPiece = pieces[targetPosition.x, targetPosition.y];

        if (!IsLegalMove(selectedPiece, targetPosition, targetPiece))
        {
            Debug.Log("Illegal move.");
            return;
        }

        MovePiece(selectedPiece, targetPosition, targetPiece);
    }

    private bool IsLegalMove(
        ChessPiece piece,
        Vector2Int targetPosition,
        ChessPiece targetPiece
    )
    {
        if (targetPosition == piece.BoardPosition)
        {
            return false;
        }

        if (targetPiece != null && targetPiece.Team == piece.Team)
        {
            return false;
        }

        Vector2Int movement = targetPosition - piece.BoardPosition;

        switch (piece.Type)
        {
            case PieceType.King:
                return IsLegalKingMove(movement);

            case PieceType.Queen:
                // TODO: Implement Queen movement
                return false;

            case PieceType.Rook:
                return IsLegalRookMove(piece.BoardPosition, targetPosition);

            case PieceType.Bishop:
                // TODO: Implement Bishop movement
                return false;

            case PieceType.Knight:
                // TODO: Implement Knight movement
                return false;

            case PieceType.Pawn:
                return IsLegalPawnMove(piece, movement, targetPiece);

            default:
                return false;
        }
    }

    private bool IsLegalKingMove(Vector2Int movement)
    {
        return Mathf.Abs(movement.x) <= 1 &&
               Mathf.Abs(movement.y) <= 1;
    }

    private bool IsLegalPawnMove(
        ChessPiece piece,
        Vector2Int movement,
        ChessPiece targetPiece
    )
    {
        int forwardDirection =
            piece.Team == PieceTeam.Player ? 1 : -1;

        if (targetPiece == null)
        {
            return movement.x == 0 &&
                   movement.y == forwardDirection;
        }

        return Mathf.Abs(movement.x) == 1 &&
               movement.y == forwardDirection;
    }

    private bool IsLegalRookMove(
        Vector2Int startPosition,
        Vector2Int targetPosition
    )
    {
        bool movesInStraightLine =
            startPosition.x == targetPosition.x ||
            startPosition.y == targetPosition.y;

        return movesInStraightLine &&
            CanRookTraverseWithOneJump(startPosition, targetPosition);
    }
    private bool CanRookTraverseWithOneJump(
        Vector2Int startPosition,
        Vector2Int targetPosition
    )
    {
        Vector2Int direction = new Vector2Int(
            System.Math.Sign(targetPosition.x - startPosition.x),
            System.Math.Sign(targetPosition.y - startPosition.y)
        );

        Vector2Int currentPosition = startPosition + direction;
        int blockingPieceCount = 0;

        while (currentPosition != targetPosition)
        {
            if (pieces[currentPosition.x, currentPosition.y] != null)
            {
                blockingPieceCount++;

                if (blockingPieceCount > 1)
                {
                    return false;
                }
            }

            currentPosition += direction;
        }

        return true;
    }

    private bool IsPathClear(
        Vector2Int startPosition,
        Vector2Int targetPosition
    )
    {
        Vector2Int direction = new Vector2Int(
            System.Math.Sign(targetPosition.x - startPosition.x),
            System.Math.Sign(targetPosition.y - startPosition.y)
        );

        Vector2Int currentPosition = startPosition + direction;

        while (currentPosition != targetPosition)
        {
            if (pieces[currentPosition.x, currentPosition.y] != null)
            {
                return false;
            }

            currentPosition += direction;
        }

        return true;
    }

    private void MovePiece(
        ChessPiece movingPiece,
        Vector2Int targetPosition,
        ChessPiece capturedPiece
    )
    {
        Vector2Int startPosition = movingPiece.BoardPosition;
        bool capturedKing =
            capturedPiece != null && capturedPiece.Type == PieceType.King;
        PieceTeam attackingTeam = movingPiece.Team;

        if (capturedPiece != null)
        {
            Destroy(capturedPiece.gameObject);
        }

        pieces[startPosition.x, startPosition.y] = null;
        pieces[targetPosition.x, targetPosition.y] = movingPiece;
        movingPiece.MoveTo(targetPosition, BoardToWorld(targetPosition));
        ClearSelection();

        if (capturedKing)
        {
            EndGame(attackingTeam);
        }
        else
        {
            SwitchTurn();
        }
    }

    private Vector3 BoardToWorld(Vector2Int boardPosition)
    {
        return new Vector3(boardPosition.x, boardPosition.y, -1f);
    }

    private Vector2Int WorldToBoard(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x),
            Mathf.RoundToInt(worldPosition.y)
        );
    }

    private bool IsInsideBoard(Vector2Int boardPosition)
    {
        return boardPosition.x >= 0 &&
               boardPosition.x < boardSize &&
               boardPosition.y >= 0 &&
               boardPosition.y < boardSize;
    }

    private void SwitchTurn()
    {
        currentTurn =
            currentTurn == PieceTeam.Player
                ? PieceTeam.Enemy
                : PieceTeam.Player;

        Debug.Log(GetTurnMessage());
    }

    private string GetTurnMessage()
    {
        return currentTurn == PieceTeam.Player
            ? "Blue Turn"
            : "Red Turn";
    }

    private void EndGame(PieceTeam winningTeam)
    {
        gameOver = true;
        resultMessage =
            winningTeam == PieceTeam.Player
                ? "BLUE WINS!"
                : "RED WINS!";

        Debug.Log(resultMessage);
    }

    private void ClearSelection()
    {
        ClearMoveHighlights();

        if (selectedPiece == null)
        {
            return;
        }

        selectedPiece.transform.localScale = Vector3.one;
        selectedPiece = null;
    }

    private void OnGUI()
    {
        DrawTurnMessage();

        if (gameOver)
        {
            DrawResultMessage();
        }
    }

    private void DrawTurnMessage()
    {
        GUIStyle turnStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };

        turnStyle.normal.textColor =
            currentTurn == PieceTeam.Player ? Color.cyan : Color.red;

        Rect turnArea = new Rect(0f, 15f, Screen.width, 50f);
        GUI.Label(turnArea, GetTurnMessage().ToUpper(), turnStyle);
    }

    private void DrawResultMessage()
    {
        GUIStyle resultStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 60,
            fontStyle = FontStyle.Bold
        };

        resultStyle.normal.textColor = Color.yellow;

        Rect resultArea = new Rect(
            0f,
            Screen.height / 2f - 60f,
            Screen.width,
            120f
        );

        GUI.Label(resultArea, resultMessage, resultStyle);
    }
}
