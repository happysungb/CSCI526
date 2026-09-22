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

    private GameObject[,] gridArray;
    private ChessPiece selectedPiece;

    private PieceTeam currentTurn = PieceTeam.Player;

    private bool gameOver;
    private string resultMessage = "";

    private void Start()
    {
        gridArray = new GameObject[boardSize, boardSize];

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
                    tileRenderer.color =
                        (x + y) % 2 == 0
                            ? darkTileColor
                            : Color.white;
                }

                gridArray[x, y] = newTile;
            }
        }
    }

    private void SpawnInitialPieces()
    {
        Vector3 blueStartPosition = new Vector3(0f, 0f, -1f);

        CreatePiece(
            "Blue_King",
            PieceTeam.Player,
            blueStartPosition,
            playerColor
        );

        Vector3 redStartPosition = new Vector3(
            boardSize - 1,
            boardSize - 1,
            -1f
        );

        CreatePiece(
            "Red_King",
            PieceTeam.Enemy,
            redStartPosition,
            enemyColor
        );
    }

    private ChessPiece CreatePiece(
        string pieceName,
        PieceTeam team,
        Vector3 position,
        Color color
    )
    {
        GameObject newPieceObject = Instantiate(
            piecePrefab,
            position,
            Quaternion.identity
        );

        newPieceObject.name = pieceName;

        ChessPiece newPiece =
            newPieceObject.GetComponent<ChessPiece>();

        if (newPiece == null)
        {
            newPiece = newPieceObject.AddComponent<ChessPiece>();
        }

        newPiece.Initialize(team, color);

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

        ChessPiece clickedPiece =
            FindClickedPiece(clickedColliders);

        if (clickedPiece != null)
        {
            HandlePieceClick(clickedPiece);
            return;
        }

        GameObject clickedTile =
            FindClickedTile(clickedColliders);

        if (clickedTile != null && selectedPiece != null)
        {
            MoveSelectedPiece(clickedTile.transform.position);
        }
    }

    private ChessPiece FindClickedPiece(
        Collider2D[] clickedColliders
    )
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

    private GameObject FindClickedTile(
        Collider2D[] clickedColliders
    )
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

        if (selectedPiece != null &&
            clickedPiece.Team != currentTurn)
        {
            CapturePiece(clickedPiece);
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
    }

    private void MoveSelectedPiece(Vector3 targetPosition)
    {
        if (selectedPiece == null)
        {
            return;
        }

        targetPosition.z = selectedPiece.transform.position.z;
        selectedPiece.transform.position = targetPosition;

        ClearSelection();
        SwitchTurn();
    }

    private void CapturePiece(ChessPiece capturedPiece)
    {
        if (selectedPiece == null)
        {
            return;
        }

        if (capturedPiece.Team == selectedPiece.Team)
        {
            return;
        }

        Vector3 targetPosition = capturedPiece.transform.position;
        targetPosition.z = selectedPiece.transform.position.z;

        bool capturedKing =
            capturedPiece.gameObject.name.Contains("King");

        PieceTeam attackingTeam = selectedPiece.Team;

        Destroy(capturedPiece.gameObject);

        selectedPiece.transform.position = targetPosition;

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

    private void SwitchTurn()
    {
        if (currentTurn == PieceTeam.Player)
        {
            currentTurn = PieceTeam.Enemy;
        }
        else
        {
            currentTurn = PieceTeam.Player;
        }

        Debug.Log(GetTurnMessage());
    }

    private string GetTurnMessage()
    {
        if (currentTurn == PieceTeam.Player)
        {
            return "Blue Turn";
        }

        return "Red Turn";
    }

    private void EndGame(PieceTeam winningTeam)
    {
        gameOver = true;

        if (winningTeam == PieceTeam.Player)
        {
            resultMessage = "BLUE WINS!";
        }
        else
        {
            resultMessage = "RED WINS!";
        }

        Debug.Log(resultMessage);
    }

    private void ClearSelection()
    {
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

        if (currentTurn == PieceTeam.Player)
        {
            turnStyle.normal.textColor = Color.cyan;
        }
        else
        {
            turnStyle.normal.textColor = Color.red;
        }

        Rect turnArea = new Rect(
            0f,
            15f,
            Screen.width,
            50f
        );

        GUI.Label(
            turnArea,
            GetTurnMessage().ToUpper(),
            turnStyle
        );
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