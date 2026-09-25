using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GamePhase
{
    Customization,
    Deployment,
    Battle,
    GameOver
}

public enum CustomizationTab
{
    Movement,
    Attack,
    Ability
}

public class ChessGameManager : MonoBehaviour
{
    private enum CustomizationChangeType
    {
        MoveCell,
        AttackCell,
        InfiniteMove,
        InfiniteAttack,
        PawnDoubleStep,
        JumpRook,
        CastleSwap
    }

    private sealed class PendingCustomizationChange
    {
        public CustomizationChangeType Type { get; }
        public Vector2Int Direction { get; }
        public int Cost { get; }

        public PendingCustomizationChange(
            CustomizationChangeType type,
            Vector2Int direction,
            int cost
        )
        {
            Type = type;
            Direction = direction;
            Cost = cost;
        }
    }

    private static readonly string[] DirectionNames =
    {
        "Forward",
        "Forward Right",
        "Right",
        "Back Right",
        "Back",
        "Back Left",
        "Left",
        "Forward Left"
    };

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1)
    };

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
    private Color validMoveHighlightColor =
        new Color(239f / 255f, 1f, 0f, 1f);
    [SerializeField]
    private Color basePatternColor = new Color(0.2f, 0.65f, 1f, 1f);
    [SerializeField]
    private Color ownedPatternColor = new Color(0.2f, 1f, 0.65f, 1f);
    [SerializeField]
    private Color pendingPatternColor = new Color(1f, 0.75f, 0.1f, 1f);
    [SerializeField]
    private Color movementPatternColor = new Color(0.15f, 0.7f, 1f, 1f);
    [SerializeField]
    private Color attackPatternColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField]
    private Color jumpPatternColor = new Color(0.8f, 0.35f, 1f, 1f);
    [SerializeField]
    private Color jumpArrowColor = new Color(0.25f, 1f, 0.75f, 1f);
    [SerializeField]
    private Color movementArrowColor = new Color(0.02f, 0.1f, 0.35f, 1f);
    [SerializeField]
    private Color attackArrowColor = new Color(0.85f, 0.05f, 0.08f, 1f);

    [Header("Customization Costs")]
    [SerializeField] private int startingMoney = 20;
    [SerializeField] private int singleSquareCost = 1;
    [SerializeField] private int infiniteRangeCost = 4;
    [SerializeField] private int pawnDoubleStepCost = 1;
    [SerializeField] private int jumpRookCost = 10;
    [SerializeField] private int castleSwapCost = 5;

    private readonly List<PieceConfiguration> pieceConfigurations =
        new List<PieceConfiguration>();
    private readonly List<PendingCustomizationChange> pendingChanges =
        new List<PendingCustomizationChange>();
    private readonly List<GameObject> customizationArrowObjects =
        new List<GameObject>();
    private readonly List<GameObject> battleEffectObjects =
        new List<GameObject>();

    private GameObject[,] gridArray;
    private Color[,] originalTileColors;
    private ChessPiece[,] pieces;
    private ChessPiece selectedPiece;
    private ChessPiece inspectedPiece;
    private PieceConfiguration selectedCustomization;
    private PieceConfiguration selectedDeploymentPiece;
    private ChessPiece customizationPreviewPiece;
    private CustomizationTab customizationTab = CustomizationTab.Movement;
    private GamePhase currentPhase = GamePhase.Customization;
    private PieceTeam customizationTeam = PieceTeam.Player;
    private PieceTeam deploymentTeam = PieceTeam.Player;
    private PieceTeam currentTurn = PieceTeam.Player;
    private PieceTeam deploymentFinishTeam = PieceTeam.Player;
    private int playerMoney;
    private int enemyMoney;
    private bool isPlacingInfiniteArrow;
    private bool deploymentAwaitingFinish;
    private bool playerDeploymentFinished;
    private bool enemyDeploymentFinished;
    private readonly Texture2D[] arrowCursorTextures =
        new Texture2D[Directions.Length];
    private GameObject deploymentGhostPiece;
    private int activeCursorDirectionIndex = -1;
    private string centeredAlertMessage = "";
    private float centeredAlertEndTime;
    private string resultMessage = "";
    private Vector2 customizationScrollPosition;
    private string customizationMessage =
        "Choose a piece, then click a highlighted board direction.";

    private Vector2Int CustomizationCenter =>
        new Vector2Int(
            boardSize / 2 - 1,
            boardSize / 2 - 1 +
            (customizationTeam == PieceTeam.Enemy ? 1 : 0)
        );

    private void Start()
    {
        gridArray = new GameObject[boardSize, boardSize];
        pieces = new ChessPiece[boardSize, boardSize];
        originalTileColors = new Color[boardSize, boardSize];
        playerMoney = startingMoney;
        enemyMoney = startingMoney;

        for (int index = 0; index < Directions.Length; index++)
        {
            arrowCursorTextures[index] =
                CreateArrowCursorTexture(Directions[index]);
        }
        ConfigureCameraForInterface();
        GenerateChessBoard();
        CreatePieceConfigurations();
        selectedCustomization = GetFirstCustomizablePiece(customizationTeam);
        CreateCustomizationPreview();
        RefreshCustomizationBoard();
    }

    private void OnDestroy()
    {
        ClearDeploymentGhost();

        foreach (Texture2D cursorTexture in arrowCursorTextures)
        {
            if (cursorTexture != null)
            {
                Destroy(cursorTexture);
            }
        }
    }

    private Texture2D CreateArrowCursorTexture(Vector2Int direction)
    {
        const int cursorSize = 32;
        Texture2D texture = new Texture2D(
            cursorSize,
            cursorSize,
            TextureFormat.RGBA32,
            false
        );
        Color[] pixels = new Color[cursorSize * cursorSize];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = Color.clear;
        }

        Vector2 arrowDirection = ((Vector2)direction).normalized;
        Vector2 perpendicular = new Vector2(
            -arrowDirection.y,
            arrowDirection.x
        );
        Vector2 center = new Vector2(15.5f, 15.5f);
        Vector2 tail = center - arrowDirection * 10f;
        Vector2 tip = center + arrowDirection * 12f;
        Vector2 arrowHeadBase = tip - arrowDirection * 8f;
        Vector2 leftWing = arrowHeadBase + perpendicular * 5f;
        Vector2 rightWing = arrowHeadBase - perpendicular * 5f;
        Color outlineColor = new Color(0.08f, 0.08f, 0.12f, 1f);
        Color cursorColor = new Color(1f, 0.78f, 0.08f, 1f);

        DrawCursorLine(pixels, cursorSize, tail, tip, outlineColor, 2);
        DrawCursorLine(pixels, cursorSize, leftWing, tip, outlineColor, 2);
        DrawCursorLine(pixels, cursorSize, rightWing, tip, outlineColor, 2);
        DrawCursorLine(pixels, cursorSize, tail, tip, cursorColor, 1);
        DrawCursorLine(pixels, cursorSize, leftWing, tip, cursorColor, 1);
        DrawCursorLine(pixels, cursorSize, rightWing, tip, cursorColor, 1);

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private void SetDeploymentGhost(PieceConfiguration configuration)
    {
        ClearDeploymentGhost();

        if (configuration == null || piecePrefab == null)
        {
            return;
        }

        deploymentGhostPiece = Instantiate(
            piecePrefab,
            Vector3.zero,
            Quaternion.identity
        );
        deploymentGhostPiece.name = "Deployment_Ghost";

        Collider2D[] ghostColliders =
            deploymentGhostPiece.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D ghostCollider in ghostColliders)
        {
            ghostCollider.enabled = false;
        }

        ChessPiece ghostChessPiece =
            deploymentGhostPiece.GetComponent<ChessPiece>();

        if (ghostChessPiece == null)
        {
            ghostChessPiece = deploymentGhostPiece.AddComponent<ChessPiece>();
        }

        Color teamColor =
            configuration.Team == PieceTeam.Player
                ? playerColor
                : enemyColor;

        ghostChessPiece.Initialize(
            configuration,
            Vector2Int.zero,
            teamColor
        );

        SpriteRenderer[] ghostRenderers =
            deploymentGhostPiece.GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer ghostRenderer in ghostRenderers)
        {
            Color ghostColor = ghostRenderer.color;
            ghostColor.a = 0.72f;
            ghostRenderer.color = ghostColor;
            ghostRenderer.sortingOrder = 10;
        }

        TextMesh[] ghostLabels =
            deploymentGhostPiece.GetComponentsInChildren<TextMesh>();

        foreach (TextMesh ghostLabel in ghostLabels)
        {
            Color labelColor = ghostLabel.color;
            labelColor.a = 0.9f;
            ghostLabel.color = labelColor;

            MeshRenderer labelRenderer =
                ghostLabel.GetComponent<MeshRenderer>();

            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 11;
            }
        }

        deploymentGhostPiece.transform.localScale =
            new Vector3(0.9f, 0.9f, 1f);

        Cursor.visible = false;

        if (Mouse.current != null)
        {
            UpdateDeploymentGhost(Mouse.current.position.ReadValue());
        }
    }

    private void UpdateDeploymentGhost(Vector2 screenPosition)
    {
        if (deploymentGhostPiece == null || Camera.main == null)
        {
            return;
        }

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, 0f)
        );

        deploymentGhostPiece.transform.position =
            new Vector3(worldPosition.x, worldPosition.y, -3f);
    }

    private void ClearDeploymentGhost()
    {
        Cursor.visible = true;

        if (deploymentGhostPiece != null)
        {
            Destroy(deploymentGhostPiece);
            deploymentGhostPiece = null;
        }
    }

    private void DrawCursorLine(
        Color[] pixels,
        int textureSize,
        Vector2 start,
        Vector2 end,
        Color color,
        int radius
    )
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end) * 2f);

        for (int step = 0; step <= steps; step++)
        {
            Vector2 point = Vector2.Lerp(start, end, step / (float)steps);
            int centerX = Mathf.RoundToInt(point.x);
            int centerY = Mathf.RoundToInt(point.y);

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (x >= 0 && x < textureSize &&
                        y >= 0 && y < textureSize)
                    {
                        pixels[y * textureSize + x] = color;
                    }
                }
            }
        }
    }

    private void SetInfiniteArrowPlacementMode(bool isActive)
    {
        isPlacingInfiniteArrow = isActive;
        activeCursorDirectionIndex = -1;

        if (isActive && Mouse.current != null)
        {
            UpdateDirectionalArrowCursor(
                Mouse.current.position.ReadValue()
            );
            return;
        }

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void UpdateDirectionalArrowCursor(Vector2 screenPosition)
    {
        if (!isPlacingInfiniteArrow ||
            customizationPreviewPiece == null ||
            Camera.main == null)
        {
            return;
        }

        Vector3 pointerWorld = Camera.main.ScreenToWorldPoint(screenPosition);
        Vector2 delta = pointerWorld -
            customizationPreviewPiece.transform.position;
        int xDirection = 0;
        int yDirection = 0;
        float absoluteX = Mathf.Abs(delta.x);
        float absoluteY = Mathf.Abs(delta.y);

        if (absoluteX > absoluteY * 1.5f)
        {
            xDirection = delta.x >= 0f ? 1 : -1;
        }
        else if (absoluteY > absoluteX * 1.5f)
        {
            yDirection = delta.y >= 0f ? 1 : -1;
        }
        else
        {
            xDirection = delta.x >= 0f ? 1 : -1;
            yDirection = delta.y >= 0f ? 1 : -1;
        }

        Vector2Int direction = new Vector2Int(xDirection, yDirection);
        int directionIndex = Array.IndexOf(Directions, direction);

        if (directionIndex < 0 ||
            directionIndex == activeCursorDirectionIndex)
        {
            return;
        }

        activeCursorDirectionIndex = directionIndex;
        Cursor.SetCursor(
            arrowCursorTextures[directionIndex],
            new Vector2(16f, 16f),
            CursorMode.ForceSoftware
        );
    }

    private void CancelDeploymentSelection()
    {
        selectedDeploymentPiece = null;
        ClearDeploymentGhost();
        HighlightDeploymentArea();
    }

    private void Update()
    {
        if (currentPhase == GamePhase.GameOver || Mouse.current == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        if (currentPhase == GamePhase.Deployment &&
            deploymentGhostPiece != null)
        {
            UpdateDeploymentGhost(screenPosition);
        }

        if (isPlacingInfiniteArrow)
        {
            UpdateDirectionalArrowCursor(screenPosition);
        }

        // Right-click cancels the currently selected deployment piece.
        if (currentPhase == GamePhase.Deployment &&
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelDeploymentSelection();
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (IsPointerOverInterface(screenPosition))
        {
            return;
        }

        HandleMouseClick(screenPosition);
    }

    private void ConfigureCameraForInterface()
    {
        if (Camera.main == null)
        {
            return;
        }

        Camera.main.orthographic = true;
        Camera.main.orthographicSize = 5.6f;
        Camera.main.transform.position = new Vector3(
            (boardSize - 1) / 2f,
            (boardSize - 1) / 2f,
            Camera.main.transform.position.z
        );
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

    private void CreatePieceConfigurations()
    {
        AddTeamConfigurations(PieceTeam.Player, "Blue");
        AddTeamConfigurations(PieceTeam.Enemy, "Red");
    }

    private void AddTeamConfigurations(PieceTeam team, string teamName)
    {
        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} Rook 1",
                team,
                PieceType.Rook
            )
        );

        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} Knight 1",
                team,
                PieceType.Knight
            )
        );

        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} Bishop 1",
                team,
                PieceType.Bishop
            )
        );

        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} Queen",
                team,
                PieceType.Queen
            )
        );

        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} King",
                team,
                PieceType.King
            )
        );

        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} Bishop 2",
                team,
                PieceType.Bishop
            )
        );

        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} Knight 2",
                team,
                PieceType.Knight
            )
        );

        pieceConfigurations.Add(
            new PieceConfiguration(
                $"{teamName} Rook 2",
                team,
                PieceType.Rook
            )
        );

        for (int index = 1; index <= 8; index++)
        {
            pieceConfigurations.Add(
                new PieceConfiguration(
                    $"{teamName} Pawn {index}",
                    team,
                    PieceType.Pawn
                )
            );
        }
    }

    private ChessPiece CreatePiece(
        PieceConfiguration configuration,
        Vector2Int boardPosition
    )
    {
        Vector3 worldPosition = BoardToWorld(boardPosition);
        GameObject newPieceObject = Instantiate(
            piecePrefab,
            worldPosition,
            Quaternion.identity
        );

        newPieceObject.name = configuration.PieceName.Replace(" ", "_");

        ChessPiece newPiece =
            newPieceObject.GetComponent<ChessPiece>();

        if (newPiece == null)
        {
            newPiece = newPieceObject.AddComponent<ChessPiece>();
        }

        Color pieceColor =
            configuration.Team == PieceTeam.Player
                ? playerColor
                : enemyColor;

        newPiece.Initialize(configuration, boardPosition, pieceColor);
        pieces[boardPosition.x, boardPosition.y] = newPiece;

        return newPiece;
    }

    private void HandleMouseClick(Vector2 screenPosition)
    {
        if (Camera.main == null)
        {
            return;
        }

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, 0f)
        );
        Collider2D[] clickedColliders =
            Physics2D.OverlapPointAll(worldPosition);

        if (currentPhase == GamePhase.Customization)
        {
            HandleCustomizationClick(clickedColliders);
            return;
        }

        if (currentPhase == GamePhase.Deployment)
        {
            HandleDeploymentClick(clickedColliders);
            return;
        }

        if (currentPhase == GamePhase.Battle)
        {
            HandleBattleClick(clickedColliders);
        }
    }

    private void HandleCustomizationClick(
        Collider2D[] clickedColliders
    )
    {
        if (customizationTab == CustomizationTab.Ability ||
            selectedCustomization == null ||
            selectedCustomization.Type == PieceType.King)
        {
            return;
        }

        GameObject clickedTile = FindClickedTile(clickedColliders);

        if (clickedTile == null)
        {
            return;
        }

        Vector2Int boardPosition = WorldToBoard(
            clickedTile.transform.position
        );
        Vector2Int boardOffset = boardPosition - CustomizationCenter;

        if (boardOffset == Vector2Int.zero ||
            Mathf.Abs(boardOffset.x) > 1 ||
            Mathf.Abs(boardOffset.y) > 1)
        {
            customizationMessage =
                "One-square upgrades must touch the preview piece.";
            return;
        }

        int forwardDirection =
            customizationTeam == PieceTeam.Player ? 1 : -1;
        Vector2Int relativeOffset = new Vector2Int(
            boardOffset.x,
            boardOffset.y * forwardDirection
        );

        if (isPlacingInfiniteArrow)
        {
            CustomizationChangeType arrowType =
                customizationTab == CustomizationTab.Attack
                    ? CustomizationChangeType.InfiniteAttack
                    : CustomizationChangeType.InfiniteMove;
            TogglePendingArrow(relativeOffset, arrowType);
            SetInfiniteArrowPlacementMode(false);
            return;
        }

        TogglePendingCell(relativeOffset);
    }

    private void HandleDeploymentClick(Collider2D[] clickedColliders)
    {
        if (deploymentAwaitingFinish)
        {
            return;
        }

        ChessPiece clickedPiece = FindClickedPiece(clickedColliders);

        if (clickedPiece != null)
        {
            if (clickedPiece.Team != deploymentTeam)
            {
                if (inspectedPiece == clickedPiece)
                {
                    inspectedPiece = null;
                    ClearBattleEffects();
                    HighlightDeploymentArea();
                }
                else
                {
                    inspectedPiece = clickedPiece;
                    HighlightValidMoves(clickedPiece);
                }
            }

            return;
        }

        GameObject clickedTile = FindClickedTile(clickedColliders);

        if (clickedTile == null || selectedDeploymentPiece == null)
        {
            return;
        }

        Vector2Int targetPosition = WorldToBoard(
            clickedTile.transform.position
        );

        TryDeploySelectedPiece(targetPosition);
    }

    private void HandleBattleClick(Collider2D[] clickedColliders)
    {
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

    private void TryDeploySelectedPiece(Vector2Int targetPosition)
    {
        if (selectedDeploymentPiece.Type == PieceType.King &&
            !AreAllNonKingPiecesDeployed(deploymentTeam))
        {
            ShowCenteredAlert("THE KING MUST BE DEPLOYED LAST");
            return;
        }

        if (!IsInsideBoard(targetPosition) ||
            !IsDeploymentRow(deploymentTeam, targetPosition.y) ||
            pieces[targetPosition.x, targetPosition.y] != null ||
            selectedDeploymentPiece.Team != deploymentTeam ||
            selectedDeploymentPiece.IsDeployed)
        {
            Debug.Log("Invalid deployment square.");
            return;
        }

        CreatePiece(selectedDeploymentPiece, targetPosition);
        ClearDeploymentGhost();
        bool deployedKing =
            selectedDeploymentPiece.Type == PieceType.King;
        selectedDeploymentPiece.IsDeployed = true;
        ClearMoveHighlights();
        ClearBattleEffects();
        inspectedPiece = null;

        if (deployedKing)
        {
            deploymentAwaitingFinish = true;
            deploymentFinishTeam = deploymentTeam;
            selectedDeploymentPiece = null;
            ClearMoveHighlights();
            Debug.Log(
                $"{GetTeamName(deploymentTeam)} placed the King and may finish deployment."
            );
            return;
        }

        deploymentTeam = GetOpposingTeam(deploymentTeam);
        selectedDeploymentPiece = null;
        HighlightDeploymentArea();
    }

    private bool IsDeploymentRow(PieceTeam team, int row)
    {
        if (team == PieceTeam.Player)
        {
            return row == 0 || row == 1;
        }

        return row == boardSize - 1 || row == boardSize - 2;
    }

    private bool AreAllNonKingPiecesDeployed(PieceTeam team)
    {
        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team == team &&
                configuration.Type != PieceType.King &&
                !configuration.IsDeployed)
            {
                return false;
            }
        }

        return true;
    }

    private bool CanCastleSwap(
        ChessPiece firstPiece,
        ChessPiece secondPiece
    )
    {
        if (firstPiece == null || secondPiece == null)
        {
            return false;
        }

        if (firstPiece.Team != secondPiece.Team)
        {
            return false;
        }

        ChessPiece king = null;
        ChessPiece rook = null;

        if (firstPiece.Type == PieceType.King &&
            secondPiece.Type == PieceType.Rook)
        {
            king = firstPiece;
            rook = secondPiece;
        }
        else if (firstPiece.Type == PieceType.Rook &&
                 secondPiece.Type == PieceType.King)
        {
            rook = firstPiece;
            king = secondPiece;
        }
        else
        {
            return false;
        }

        if (!rook.Configuration.HasCastleSwap)
        {
            return false;
        }

        if (king.HasMoved || rook.HasMoved)
        {
            return false;
        }

        return true;
    }

    private void PerformCastleSwap(
        ChessPiece firstPiece,
        ChessPiece secondPiece
    )
    {
        if (!CanCastleSwap(firstPiece, secondPiece))
        {
            return;
        }

        Vector2Int firstPosition = firstPiece.BoardPosition;
        Vector2Int secondPosition = secondPiece.BoardPosition;

        pieces[firstPosition.x, firstPosition.y] = secondPiece;
        pieces[secondPosition.x, secondPosition.y] = firstPiece;

        firstPiece.MoveTo(
            secondPosition,
            BoardToWorld(secondPosition)
        );

        secondPiece.MoveTo(
            firstPosition,
            BoardToWorld(firstPosition)
        );

        ClearSelection();
        inspectedPiece = null;
        SwitchTurn();
    }

    private void DrawCastleSwapIndicator(ChessPiece selected)
    {
        if (selected == null || selected.HasMoved)
        {
            return;
        }

        foreach (ChessPiece candidate in pieces)
        {
            if (candidate == null ||
                candidate == selected ||
                !CanCastleSwap(selected, candidate))
            {
                continue;
            }

            GameObject indicatorObject =
                new GameObject("CastleSwapIndicator");

            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.transform.position = new Vector3(
                candidate.transform.position.x,
                candidate.transform.position.y + 0.58f,
                -3f
            );

            TextMesh indicator =
                indicatorObject.AddComponent<TextMesh>();

            indicator.text = "CASTLE";
            indicator.anchor = TextAnchor.MiddleCenter;
            indicator.alignment = TextAlignment.Center;
            indicator.fontSize = 42;
            indicator.characterSize = 0.075f;
            indicator.fontStyle = FontStyle.Bold;
            indicator.color = new Color(1f, 0.75f, 0.1f, 1f);

            MeshRenderer renderer =
                indicatorObject.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sortingOrder = 12;
            }

            battleEffectObjects.Add(indicatorObject);
        }
    }

    private void HandlePieceClick(ChessPiece clickedPiece)
    {
        if (selectedPiece != null &&
            clickedPiece.Team == selectedPiece.Team &&
            CanCastleSwap(selectedPiece, clickedPiece))
        {
            PerformCastleSwap(selectedPiece, clickedPiece);
            return;
        }

        if (clickedPiece.Team == currentTurn)
        {
            if (selectedPiece == clickedPiece)
            {
                ClearSelection();
                inspectedPiece = null;
                return;
            }

            inspectedPiece = clickedPiece;
            SelectPiece(clickedPiece);
            return;
        }

        if (selectedPiece != null &&
            IsLegalMove(
                selectedPiece,
                clickedPiece.BoardPosition,
                clickedPiece
            ))
        {
            TryMoveSelectedPiece(clickedPiece.BoardPosition);
            return;
        }

        if (selectedPiece == null && inspectedPiece == clickedPiece)
        {
            ClearMoveHighlights();
            ClearBattleEffects();
            inspectedPiece = null;
            return;
        }

        ClearSelection();
        inspectedPiece = clickedPiece;
        HighlightValidMoves(clickedPiece);
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
        ClearBattleEffects();

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                Vector2Int targetPosition = new Vector2Int(x, y);
                ChessPiece targetPiece = pieces[x, y];

                if (piece.Type == PieceType.Pawn &&
                    IsPawnAttackPatternForDisplay(piece, targetPosition))
                {
                    HighlightTileWithColor(x, y, attackPatternColor);
                }
                else if (IsLegalMove(piece, targetPosition, targetPiece))
                {
                    Color highlightColor;

                    if (piece.Type == PieceType.Rook &&
                        piece.Configuration.HasJumpRook)
                    {
                        highlightColor = jumpPatternColor;
                    }
                    else
                    {
                        highlightColor = targetPiece == null
                            ? movementPatternColor
                            : attackPatternColor;
                    }

                    HighlightTileWithColor(x, y, highlightColor);
                }
            }
        }

        DrawBattleJumpArcs(piece);
        DrawCastleSwapIndicator(piece);
    }

    private bool IsPawnAttackPatternForDisplay(
        ChessPiece piece,
        Vector2Int targetPosition
    )
    {
        Vector2Int movement = targetPosition - piece.BoardPosition;
        int forwardDirection =
            piece.Team == PieceTeam.Player ? 1 : -1;
        Vector2Int relativeMovement = new Vector2Int(
            movement.x,
            movement.y * forwardDirection
        );

        if (relativeMovement == new Vector2Int(-1, 1) ||
            relativeMovement == new Vector2Int(1, 1) ||
            piece.Configuration.HasAdditionalAttack(relativeMovement))
        {
            return true;
        }

        foreach (Vector2Int direction in
                 piece.Configuration.InfiniteAttackDirections)
        {
            if (MatchesInfiniteDirection(relativeMovement, direction) &&
                IsPathClear(piece.BoardPosition, targetPosition))
            {
                return true;
            }
        }

        return false;
    }

    private void HighlightDeploymentArea()
    {
        ClearMoveHighlights();

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (IsDeploymentRow(deploymentTeam, y) &&
                    pieces[x, y] == null)
                {
                    HighlightTile(x, y);
                }
            }
        }
    }

    private void HighlightTile(int x, int y)
    {
        SpriteRenderer tileRenderer =
            gridArray[x, y].GetComponent<SpriteRenderer>();

        if (tileRenderer != null)
        {
            tileRenderer.color = Color.Lerp(
                originalTileColors[x, y],
                validMoveHighlightColor,
                0.4f
            );
        }
    }

    private void HighlightTileWithColor(int x, int y, Color color)
    {
        SpriteRenderer tileRenderer =
            gridArray[x, y].GetComponent<SpriteRenderer>();

        if (tileRenderer != null)
        {
            tileRenderer.color = Color.Lerp(
                originalTileColors[x, y],
                color,
                0.55f
            );
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
        if (targetPosition == piece.BoardPosition ||
            (targetPiece != null && targetPiece.Team == piece.Team))
        {
            return false;
        }

        Vector2Int movement = targetPosition - piece.BoardPosition;

        bool isBasicMove;

        switch (piece.Type)
        {
            case PieceType.King:
                isBasicMove = IsLegalKingMove(movement);
                break;

            case PieceType.Queen:
                isBasicMove = IsLegalQueenMove(
                    piece.BoardPosition,
                    targetPosition
                );
                break;

            case PieceType.Rook:
                isBasicMove = IsLegalRookMove(piece, targetPosition);
                break;

            case PieceType.Bishop:
                isBasicMove = IsLegalBishopMove(
                    piece.BoardPosition,
                    targetPosition
                );
                break;

            case PieceType.Knight:
                isBasicMove = IsLegalKnightMove(movement);
                break;

            case PieceType.Pawn:
                isBasicMove = IsLegalPawnMove(
                    piece,
                    movement,
                    targetPiece
                );
                break;

            default:
                isBasicMove = false;
                break;
        }

        return isBasicMove ||
               IsLegalCustomizedPattern(piece, movement, targetPiece);
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
        int forwardDistance = movement.y * forwardDirection;

        if (forwardDistance <= 0)
        {
            return false;
        }

        if (targetPiece == null)
        {
            if (movement.x != 0)
            {
                return false;
            }

            bool usesDoubleStep =
                piece.Configuration.HasPawnDoubleStep &&
                !piece.HasMoved &&
                forwardDistance == 2;

            return (forwardDistance == 1 || usesDoubleStep) &&
                   IsPathClear(
                       piece.BoardPosition,
                       piece.BoardPosition + movement
                   );
        }

        return Mathf.Abs(movement.x) == 1 && forwardDistance == 1;
    }

    private bool IsLegalQueenMove(
        Vector2Int startPosition,
        Vector2Int targetPosition
    )
    {
        Vector2Int movement = targetPosition - startPosition;

        bool movesInStraightLine =
            startPosition.x == targetPosition.x ||
            startPosition.y == targetPosition.y;

        bool movesDiagonally =
            Mathf.Abs(movement.x) == Mathf.Abs(movement.y);

        return
            (movesInStraightLine || movesDiagonally) &&
            IsPathClear(startPosition, targetPosition);
    }

    private bool IsLegalBishopMove(
        Vector2Int startPosition,
        Vector2Int targetPosition
    )
    {
        Vector2Int movement = targetPosition - startPosition;

        bool movesDiagonally =
            Mathf.Abs(movement.x) == Mathf.Abs(movement.y);

        return movesDiagonally &&
            IsPathClear(startPosition, targetPosition);
    }

    private bool IsLegalKnightMove(Vector2Int movement)
    {
        int xDistance = Mathf.Abs(movement.x);
        int yDistance = Mathf.Abs(movement.y);

        return
            (xDistance == 2 && yDistance == 1) ||
            (xDistance == 1 && yDistance == 2);
    }

    private bool IsLegalRookMove(
        ChessPiece piece,
        Vector2Int targetPosition
    )
    {
        Vector2Int startPosition = piece.BoardPosition;
        bool movesInStraightLine =
            startPosition.x == targetPosition.x ||
            startPosition.y == targetPosition.y;

        if (!movesInStraightLine)
        {
            return false;
        }

        return piece.Configuration.HasJumpRook
            ? CanRookTraverseWithOneJump(startPosition, targetPosition)
            : IsPathClear(startPosition, targetPosition);
    }

    private bool IsLegalCustomizedPattern(
        ChessPiece piece,
        Vector2Int movement,
        ChessPiece targetPiece
    )
    {
        if (piece.Type == PieceType.King)
        {
            return false;
        }

        int forwardDirection =
            piece.Team == PieceTeam.Player ? 1 : -1;
        Vector2Int relativeMovement = new Vector2Int(
            movement.x,
            movement.y * forwardDirection
        );
        PieceConfiguration configuration = piece.Configuration;

        if (targetPiece == null)
        {
            if (configuration.HasAdditionalMove(relativeMovement))
            {
                return true;
            }

            foreach (Vector2Int direction in
                     configuration.InfiniteMoveDirections)
            {
                if (MatchesInfiniteDirection(relativeMovement, direction))
                {
                    return IsPathClear(
                        piece.BoardPosition,
                        piece.BoardPosition + movement
                    );
                }
            }

            return false;
        }

        if (configuration.HasAdditionalAttack(relativeMovement))
        {
            return true;
        }

        foreach (Vector2Int direction in
                 configuration.InfiniteAttackDirections)
        {
            if (MatchesInfiniteDirection(relativeMovement, direction))
            {
                return IsPathClear(
                    piece.BoardPosition,
                    piece.BoardPosition + movement
                );
            }
        }

        return false;
    }

    private bool MatchesInfiniteDirection(
        Vector2Int movement,
        Vector2Int direction
    )
    {
        if (direction.x == 0)
        {
            return movement.x == 0 &&
                   Math.Sign(movement.y) == direction.y;
        }

        if (direction.y == 0)
        {
            return movement.y == 0 &&
                   Math.Sign(movement.x) == direction.x;
        }

        return Mathf.Abs(movement.x) == Mathf.Abs(movement.y) &&
               Math.Sign(movement.x) == direction.x &&
               Math.Sign(movement.y) == direction.y;
    }

    private bool CanRookTraverseWithOneJump(
        Vector2Int startPosition,
        Vector2Int targetPosition
    )
    {
        Vector2Int direction = GetStepDirection(
            startPosition,
            targetPosition
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
        Vector2Int direction = GetStepDirection(
            startPosition,
            targetPosition
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

    private Vector2Int GetStepDirection(
        Vector2Int startPosition,
        Vector2Int targetPosition
    )
    {
        return new Vector2Int(
            Math.Sign(targetPosition.x - startPosition.x),
            Math.Sign(targetPosition.y - startPosition.y)
        );
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

    private void SwitchTurn()
    {
        currentTurn = GetOpposingTeam(currentTurn);
        Debug.Log(GetTurnMessage());
    }

    private PieceTeam GetOpposingTeam(PieceTeam team)
    {
        return team == PieceTeam.Player
            ? PieceTeam.Enemy
            : PieceTeam.Player;
    }

    private string GetTurnMessage()
    {
        return currentTurn == PieceTeam.Player
            ? "Blue Turn"
            : "Red Turn";
    }

    private void EndGame(PieceTeam winningTeam)
    {
        currentPhase = GamePhase.GameOver;
        resultMessage =
            winningTeam == PieceTeam.Player
                ? "BLUE WINS!"
                : "RED WINS!";

        Debug.Log(resultMessage);
    }

    private void ClearSelection()
    {
        ClearMoveHighlights();
        ClearBattleEffects();

        if (selectedPiece == null)
        {
            return;
        }

        selectedPiece.transform.localScale = Vector3.one;
        selectedPiece = null;
    }

    private PieceConfiguration GetFirstCustomizablePiece(PieceTeam team)
    {
        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team == team &&
                configuration.Type != PieceType.King)
            {
                return configuration;
            }
        }

        return null;
    }

    private PieceConfiguration GetFirstUndeployedPiece(PieceTeam team)
    {
        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team == team &&
                configuration.Type != PieceType.King &&
                !configuration.IsDeployed)
            {
                return configuration;
            }
        }

        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team == team &&
                configuration.Type == PieceType.King &&
                !configuration.IsDeployed)
            {
                return configuration;
            }
        }

        return null;
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

    private bool IsPointerOverInterface(Vector2 screenPosition)
    {
        Vector2 guiPosition = new Vector2(
            screenPosition.x,
            Screen.height - screenPosition.y
        );
        Rect leftPanel = new Rect(10f, 70f, 360f, Screen.height - 80f);
        Rect rightPanel = new Rect(
            Screen.width - 320f,
            70f,
            310f,
            360f
        );

        if (currentPhase == GamePhase.Deployment &&
            leftPanel.Contains(guiPosition))
        {
            return true;
        }

        if (currentPhase == GamePhase.Customization)
        {
            Rect pieceLibrary =
                new Rect(10f, 70f, 220f, Screen.height - 150f);
            Rect shop = new Rect(
                Screen.width - 245f,
                70f,
                235f,
                Screen.height - 150f
            );
            Rect tabs = new Rect(
                Screen.width / 2f - 140f,
                65f,
                280f,
                40f
            );
            Rect actions = new Rect(
                Screen.width / 2f - 280f,
                Screen.height - 82f,
                560f,
                72f
            );

            return pieceLibrary.Contains(guiPosition) ||
                   shop.Contains(guiPosition) ||
                   tabs.Contains(guiPosition) ||
                   actions.Contains(guiPosition);
        }

        return inspectedPiece != null && rightPanel.Contains(guiPosition);
    }

    private void OnGUI()
    {
        DrawPhaseHeader();

        switch (currentPhase)
        {
            case GamePhase.Customization:
                DrawCustomizationEditor();
                break;
            case GamePhase.Deployment:
                DrawDeploymentPanel();
                break;
            case GamePhase.Battle:
                DrawPieceDetails();
                break;
            case GamePhase.GameOver:
                DrawPieceDetails();
                DrawResultMessage();
                break;
        }

        DrawCenteredAlert();
    }

    private void DrawPhaseHeader()
    {
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = Color.white;

        string headerText;

        switch (currentPhase)
        {
            case GamePhase.Customization:
                headerText = $"CUSTOMIZATION - {GetTeamName(customizationTeam)}";
                break;
            case GamePhase.Deployment:
                PieceTeam displayedDeploymentTeam = deploymentAwaitingFinish
                    ? deploymentFinishTeam
                    : deploymentTeam;

                headerText = deploymentAwaitingFinish
                    ? $"DEPLOYMENT FINISH - {GetTeamName(displayedDeploymentTeam)}"
                    : $"DEPLOYMENT - {GetTeamName(displayedDeploymentTeam)}";

                headerStyle.normal.textColor =
                    displayedDeploymentTeam == PieceTeam.Player
                        ? Color.cyan
                        : Color.red;
                break;
            case GamePhase.Battle:
                headerText = GetTurnMessage().ToUpper();
                headerStyle.normal.textColor =
                    currentTurn == PieceTeam.Player
                        ? Color.cyan
                        : Color.red;
                break;
            default:
                headerText = "GAME OVER";
                break;
        }

        GUI.Label(new Rect(0f, 10f, Screen.width, 50f), headerText, headerStyle);
    }

    private void DrawCustomizationEditor()
    {
        DrawPieceLibrary();
        DrawCustomizationTabs();
        DrawCustomizationShop();
        DrawCustomizationActions();
    }

    private void DrawPieceLibrary()
    {
        Rect panelRect = new Rect(
            10f,
            70f,
            220f,
            Screen.height - 150f
        );

        GUI.Box(panelRect, "YOUR PIECES");

        Rect scrollRect = new Rect(
            panelRect.x + 8f,
            panelRect.y + 28f,
            panelRect.width - 16f,
            panelRect.height - 82f
        );

        int visiblePieceCount = 0;

        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team == customizationTeam &&
                configuration.Type != PieceType.King)
            {
                visiblePieceCount++;
            }
        }

        int rowCount = Mathf.CeilToInt(visiblePieceCount / 2f);
        float contentHeight = Mathf.Max(
            scrollRect.height,
            rowCount * 105f + 10f
        );

        Rect contentRect = new Rect(
            0f,
            0f,
            scrollRect.width - 18f,
            contentHeight
        );

        customizationScrollPosition = GUI.BeginScrollView(
            scrollRect,
            customizationScrollPosition,
            contentRect
        );

        int visibleIndex = 0;

        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team != customizationTeam ||
                configuration.Type == PieceType.King)
            {
                continue;
            }

            int column = visibleIndex % 2;
            int row = visibleIndex / 2;

            Rect cardRect = new Rect(
                2f + column * 94f,
                5f + row * 105f,
                88f,
                94f
            );

            bool isSelected =
                configuration == selectedCustomization;

            GUIStyle cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 12,
                fontStyle = isSelected
                    ? FontStyle.Bold
                    : FontStyle.Normal
            };

            if (GUI.Button(
                    cardRect,
                    GetShortPieceName(configuration),
                    cardStyle
                ))
            {
                SelectCustomizationPiece(configuration);
            }

            DrawPieceImage(
                cardRect,
                configuration,
                isSelected
            );

            visibleIndex++;
        }

        GUI.EndScrollView();

        Rect finishRect = new Rect(
            panelRect.x + 10f,
            panelRect.yMax - 45f,
            panelRect.width - 20f,
            34f
        );

        if (GUI.Button(finishRect, "FINISH TEAM"))
        {
            FinishCustomizationTurn();
        }
    }

    private void DrawPieceImage(
        Rect cardRect,
        PieceConfiguration configuration,
        bool isSelected
    )
    {
        const float imageSize = 56f;
        Rect imageRect = new Rect(
            cardRect.x + (cardRect.width - imageSize) / 2f,
            cardRect.y + 8f,
            imageSize,
            imageSize
        );
        SpriteRenderer prefabRenderer =
            piecePrefab.GetComponentInChildren<SpriteRenderer>();

        if (prefabRenderer != null && prefabRenderer.sprite != null)
        {
            Color previousColor = GUI.color;
            GUI.color = configuration.Team == PieceTeam.Player
                ? playerColor
                : enemyColor;
            GUI.DrawTexture(
                imageRect,
                prefabRenderer.sprite.texture,
                ScaleMode.ScaleToFit,
                true
            );
            GUI.color = previousColor;
        }

        GUIStyle symbolStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        symbolStyle.normal.textColor = Color.white;
        string marker = configuration.IsModified ? "*" : "";
        GUI.Label(
            imageRect,
            GetPieceSymbol(configuration.Type) + marker,
            symbolStyle
        );

        if (isSelected)
        {
            GUIStyle selectedStyle = new GUIStyle(symbolStyle)
            {
                fontSize = 10
            };
            selectedStyle.normal.textColor = pendingPatternColor;
            GUI.Label(
                new Rect(cardRect.x, cardRect.y + 2f, cardRect.width, 18f),
                "SELECTED",
                selectedStyle
            );
        }
    }

    private string GetShortPieceName(PieceConfiguration configuration)
    {
        string teamPrefix =
            configuration.Team == PieceTeam.Player ? "Blue " : "Red ";
        return configuration.PieceName.Replace(teamPrefix, "");
    }

    private string GetPieceSymbol(PieceType type)
    {
        switch (type)
        {
            case PieceType.King:
                return "K";

            case PieceType.Queen:
                return "Q";

            case PieceType.Rook:
                return "R";

            case PieceType.Bishop:
                return "B";

            case PieceType.Knight:
                return "N";

            default:
                return "P";
        }
    }

    private void DrawCustomizationTabs()
    {
        if (selectedCustomization == null)
        {
            return;
        }

        float startX = Screen.width / 2f - 140f;

        if (selectedCustomization.Type != PieceType.Pawn)
        {
            customizationTab = CustomizationTab.Movement;
            DrawCustomizationTabButton(
                new Rect(startX, 65f, 280f, 40f),
                "MOVEMENT / ATTACK",
                CustomizationTab.Movement
            );
            return;
        }

        DrawCustomizationTabButton(
            new Rect(startX, 65f, 140f, 40f),
            "MOVEMENT",
            CustomizationTab.Movement
        );
        DrawCustomizationTabButton(
            new Rect(startX + 140f, 65f, 140f, 40f),
            "ATTACK",
            CustomizationTab.Attack
        );
    }

    private void DrawCustomizationTabButton(
        Rect rect,
        string label,
        CustomizationTab tab
    )
    {
        bool isSelected = customizationTab == tab;
        GUIStyle tabStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
        };
        Color previousBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = isSelected
            ? new Color(0.72f, 0.76f, 0.86f, 1f)
            : new Color(0.3f, 0.32f, 0.37f, 1f);
        tabStyle.normal.textColor = Color.white;
        tabStyle.hover.textColor = Color.white;
        tabStyle.active.textColor = Color.white;
        tabStyle.focused.textColor = Color.white;

        if (GUI.Button(rect, label, tabStyle) && !isSelected)
        {
            customizationTab = tab;
            SetInfiniteArrowPlacementMode(false);
            customizationMessage =
                "Click an adjacent board square to add or remove it.";
            RefreshCustomizationBoard();
        }

        GUI.backgroundColor = previousBackgroundColor;
    }

    private void DrawCustomizationShop()
    {
        Rect shopRect = new Rect(
            Screen.width - 245f,
            70f,
            235f,
            Screen.height - 150f
        );
        GUILayout.BeginArea(shopRect, GUI.skin.box);
        GUIStyle shopTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("CUSTOM SHOP", shopTitleStyle);

        if (selectedCustomization == null ||
            selectedCustomization.Type == PieceType.King)
        {
            GUILayout.Label("Select a customizable piece.", shopTitleStyle);
        }
        else
        {
            if (selectedCustomization.Type != PieceType.Queen)
            {
                DrawArrowShop();
                GUILayout.Space(18f);
            }

            GUILayout.Label("ABILITY", shopTitleStyle);
            DrawAbilityShop();
        }

        GUILayout.EndArea();
    }

    private void DrawArrowShop()
    {
        GUIStyle largeLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle largeButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            fixedHeight = 42f
        };
        GUILayout.Label(
            "Buy one infinite arrow, then click one of the eight squares around the piece.",
            largeLabelStyle
        );
        string buttonText = isPlacingInfiniteArrow
            ? "SELECT A DIRECTION"
            : $"BUY ∞ ARROW  ${infiniteRangeCost}";

        if (GUILayout.Button(buttonText, largeButtonStyle))
        {
            if (isPlacingInfiniteArrow)
            {
                SetInfiniteArrowPlacementMode(false);
                customizationMessage = "Infinite arrow placement cancelled.";
            }
            else if (CanAddPendingChange(infiniteRangeCost))
            {
                SetInfiniteArrowPlacementMode(true);
                customizationMessage =
                    "Select an adjacent square for the infinite direction.";
            }
        }

        GUI.enabled = true;
    }

    private void DrawAbilityShop()
    {
        if (selectedCustomization.Type == PieceType.Pawn)
        {
            DrawPendingAbilityButton(
                "Pawn Double Step",
                "Move two squares on the first move.",
                CustomizationChangeType.PawnDoubleStep,
                pawnDoubleStepCost,
                selectedCustomization.HasPawnDoubleStep
            );
        }
        else if (selectedCustomization.Type == PieceType.Rook)
        {
            DrawPendingAbilityButton(
                "Jump Rook",
                "Cross one occupied square, but not two.",
                CustomizationChangeType.JumpRook,
                jumpRookCost,
                selectedCustomization.HasJumpRook
            );

            GUILayout.Space(12f);

            DrawPendingAbilityButton(
                "Castle Swap",
                "Swap positions with an unmoved friendly King.",
                CustomizationChangeType.CastleSwap,
                castleSwapCost,
                selectedCustomization.HasCastleSwap
            );
        }
    }

    private void DrawPendingAbilityButton(
        string title,
        string description,
        CustomizationChangeType type,
        int cost,
        bool isOwned
    )
    {
        bool isPending = HasPendingChange(type, Vector2Int.zero);
        string state = isOwned
            ? "OWNED"
            : isPending
                ? "PENDING"
                : $"${cost}";
        GUIStyle abilityLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true
        };
        GUIStyle abilityButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            fixedHeight = 42f
        };
        GUILayout.Label(description, abilityLabelStyle);
        GUI.enabled = !isOwned;

        if (GUILayout.Button($"{title} - {state}", abilityButtonStyle))
        {
            TogglePendingAbility(type, cost);
        }

        GUI.enabled = true;
    }

    private void DrawCustomizationActions()
    {
        Rect actionRect = new Rect(
            Screen.width / 2f - 280f,
            Screen.height - 82f,
            560f,
            72f
        );
        GUI.Box(actionRect, "");
        int pendingCost = GetPendingCost();
        GUIStyle costStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle actionButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17
        };
        GUI.Label(
            new Rect(actionRect.x + 12f, actionRect.y + 5f, 535f, 27f),
            $"CHANGES  ${pendingCost}       MONEY  ${GetMoney(customizationTeam)}",
            costStyle
        );

        GUI.enabled = pendingChanges.Count > 0 &&
                      pendingCost <= GetMoney(customizationTeam);

        if (GUI.Button(
                new Rect(actionRect.x + 12f, actionRect.y + 34f, 170f, 30f),
                "CONFIRM",
                actionButtonStyle
            ))
        {
            ConfirmPendingChanges();
        }

        GUI.enabled = pendingChanges.Count > 0;

        if (GUI.Button(
                new Rect(actionRect.x + 195f, actionRect.y + 34f, 170f, 30f),
                "UNDO",
                actionButtonStyle
            ))
        {
            UndoPendingChange();
        }

        if (GUI.Button(
                new Rect(actionRect.x + 378f, actionRect.y + 34f, 170f, 30f),
                "CANCEL",
                actionButtonStyle
            ))
        {
            CancelPendingChanges();
        }

        GUI.enabled = true;
    }

    private void SelectCustomizationPiece(
        PieceConfiguration configuration
    )
    {
        if (pendingChanges.Count > 0 &&
            configuration != selectedCustomization)
        {
            customizationMessage =
                "Confirm or cancel the current changes before switching pieces.";
            ShowCenteredAlert("CONFIRM OR CANCEL CHANGES FIRST");
            return;
        }

        selectedCustomization = configuration;
        SetInfiniteArrowPlacementMode(false);

        if (configuration.Type != PieceType.Pawn)
        {
            customizationTab = CustomizationTab.Movement;
        }

        customizationMessage = configuration.Type == PieceType.King
            ? "The King cannot be customized."
            : "Choose a tab and edit this piece on the board.";
        CreateCustomizationPreview();
        RefreshCustomizationBoard();
    }

    private void CreateCustomizationPreview()
    {
        if (customizationPreviewPiece != null)
        {
            Destroy(customizationPreviewPiece.gameObject);
        }

        if (selectedCustomization == null)
        {
            customizationPreviewPiece = null;
            return;
        }

        GameObject previewObject = Instantiate(
            piecePrefab,
            BoardToWorld(CustomizationCenter),
            Quaternion.identity
        );
        previewObject.name = "Customization_Preview";
        customizationPreviewPiece =
            previewObject.GetComponent<ChessPiece>();

        if (customizationPreviewPiece == null)
        {
            customizationPreviewPiece =
                previewObject.AddComponent<ChessPiece>();
        }

        Color previewColor =
            selectedCustomization.Team == PieceTeam.Player
                ? playerColor
                : enemyColor;
        customizationPreviewPiece.Initialize(
            selectedCustomization,
            CustomizationCenter,
            previewColor
        );
        customizationPreviewPiece.transform.localScale =
            new Vector3(1.2f, 1.2f, 1f);
    }

    private void TogglePendingCell(Vector2Int direction)
    {
        bool isAttack = customizationTab == CustomizationTab.Attack;
        bool isPawn = selectedCustomization.Type == PieceType.Pawn;

        if (IsBaseDirection(
                selectedCustomization.Type,
                direction,
                isAttack,
                false
            ))
        {
            customizationMessage =
                "That square is already part of the base pattern.";
            return;
        }

        bool isOwned =
            selectedCustomization.HasAdditionalMove(direction) ||
            selectedCustomization.HasAdditionalAttack(direction);

        if (isOwned)
        {
            customizationMessage = "That square is already owned.";
            return;
        }

        CustomizationChangeType type = isPawn
            ? (isAttack
                ? CustomizationChangeType.AttackCell
                : CustomizationChangeType.MoveCell)
            : CustomizationChangeType.MoveCell;

        PendingCustomizationChange pending =
            FindPendingPawnCellChange(direction);

        if (pending != null)
        {
            pendingChanges.Remove(pending);
            customizationMessage = "Pending square removed.";
            RefreshCustomizationBoard();
            return;
        }

        if (!CanAddPendingChange(singleSquareCost))
        {
            return;
        }

        pendingChanges.Add(
            new PendingCustomizationChange(
                type,
                direction,
                singleSquareCost
            )
        );

        customizationMessage =
            isPawn
                ? "Movement and attack square added to the current changes."
                : "Square added to the current changes.";

        RefreshCustomizationBoard();
    }

    private void TogglePendingArrow(
        Vector2Int direction,
        CustomizationChangeType type
    )
    {
        bool isAttack = customizationTab == CustomizationTab.Attack;
        bool isPawn = selectedCustomization.Type == PieceType.Pawn;

        if (IsBaseDirection(
                selectedCustomization.Type,
                direction,
                isAttack,
                true
            ))
        {
            customizationMessage =
                "That infinite direction is already part of the base pattern.";
            return;
        }

        bool isOwned =
            selectedCustomization.HasInfiniteMove(direction) ||
            selectedCustomization.HasInfiniteAttack(direction);

        if (isOwned)
        {
            customizationMessage =
                "That infinite direction is already owned.";
            return;
        }

        if (isPawn)
        {
            type = isAttack
                ? CustomizationChangeType.InfiniteAttack
                : CustomizationChangeType.InfiniteMove;
        }

        PendingCustomizationChange pending =
            FindPendingPawnArrowChange(direction);

        if (pending != null)
        {
            pendingChanges.Remove(pending);
            customizationMessage = "Pending arrow removed.";
            RefreshCustomizationBoard();
            return;
        }

        if (!CanAddPendingChange(infiniteRangeCost))
        {
            return;
        }

        pendingChanges.Add(
            new PendingCustomizationChange(
                type,
                direction,
                infiniteRangeCost
            )
        );

        customizationMessage =
            isPawn
                ? "Movement and attack arrow added to the current changes."
                : "Infinite arrow added to the current changes.";

        RefreshCustomizationBoard();
    }

    private void TogglePendingAbility(
        CustomizationChangeType type,
        int cost
    )
    {
        PendingCustomizationChange pending =
            FindPendingChange(type, Vector2Int.zero);

        if (pending != null)
        {
            pendingChanges.Remove(pending);
            customizationMessage = "Pending ability removed.";
            RefreshCustomizationBoard();
            return;
        }

        if (!CanAddPendingChange(cost))
        {
            return;
        }

        pendingChanges.Add(
            new PendingCustomizationChange(type, Vector2Int.zero, cost)
        );

        if (type == CustomizationChangeType.PawnDoubleStep)
        {
            customizationTab = CustomizationTab.Movement;
        }

        customizationMessage = "Ability added to the current changes.";
        RefreshCustomizationBoard();
    }

    private bool CanAddPendingChange(int cost)
    {
        if (GetPendingCost() + cost <= GetMoney(customizationTeam))
        {
            return true;
        }

        customizationMessage = "Not enough money for this change.";
        ShowCenteredAlert("NOT ENOUGH MONEY");
        return false;
    }

    private void ShowCenteredAlert(string message)
    {
        centeredAlertMessage = message;
        centeredAlertEndTime = Time.unscaledTime + 1.8f;
    }

    private void DrawCenteredAlert()
    {
        if (string.IsNullOrEmpty(centeredAlertMessage) ||
            Time.unscaledTime >= centeredAlertEndTime)
        {
            return;
        }

        Rect alertRect = new Rect(
            Screen.width / 2f - 210f,
            Screen.height / 2f - 28f,
            420f,
            56f
        );
        Color previousBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.35f, 0.38f, 0.45f, 0.96f);
        GUI.Box(alertRect, "");
        GUIStyle alertStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        alertStyle.normal.textColor = new Color(1f, 0.88f, 0.45f, 1f);
        GUI.Label(alertRect, centeredAlertMessage, alertStyle);
        GUI.backgroundColor = previousBackgroundColor;
    }

    private PendingCustomizationChange FindPendingChange(
        CustomizationChangeType type,
        Vector2Int direction
    )
    {
        foreach (PendingCustomizationChange change in pendingChanges)
        {
            if (change.Type == type && change.Direction == direction)
            {
                return change;
            }
        }

        return null;
    }

    private PendingCustomizationChange FindPendingPawnCellChange(
        Vector2Int direction
    )
    {
        foreach (PendingCustomizationChange change in pendingChanges)
        {
            if (change.Direction == direction &&
                (change.Type == CustomizationChangeType.MoveCell ||
                 change.Type == CustomizationChangeType.AttackCell))
            {
                return change;
            }
        }

        return null;
    }

    private PendingCustomizationChange FindPendingPawnArrowChange(
        Vector2Int direction
    )
    {
        foreach (PendingCustomizationChange change in pendingChanges)
        {
            if (change.Direction == direction &&
                (change.Type == CustomizationChangeType.InfiniteMove ||
                 change.Type == CustomizationChangeType.InfiniteAttack))
            {
                return change;
            }
        }

        return null;
    }

    private bool HasPendingChange(
        CustomizationChangeType type,
        Vector2Int direction
    )
    {
        return FindPendingChange(type, direction) != null;
    }

    private int GetPendingCost()
    {
        int totalCost = 0;

        foreach (PendingCustomizationChange change in pendingChanges)
        {
            totalCost += change.Cost;
        }

        return totalCost;
    }

    private void UndoPendingChange()
    {
        if (pendingChanges.Count == 0)
        {
            return;
        }

        pendingChanges.RemoveAt(pendingChanges.Count - 1);
        customizationMessage = "Last change undone.";
        RefreshCustomizationBoard();
    }

    private void CancelPendingChanges()
    {
        SetInfiniteArrowPlacementMode(false);
        pendingChanges.Clear();
        customizationMessage = "All unconfirmed changes cancelled.";
        RefreshCustomizationBoard();
    }

    private void ConfirmPendingChanges()
    {
        if (selectedCustomization == null || pendingChanges.Count == 0)
        {
            return;
        }

        int totalCost = GetPendingCost();

        if (totalCost > GetMoney(customizationTeam))
        {
            customizationMessage = "Not enough money to confirm these changes.";
            return;
        }

        foreach (PendingCustomizationChange change in pendingChanges)
        {
            ApplyCustomizationChange(selectedCustomization, change);
        }

        SpendMoney(customizationTeam, totalCost);
        SetInfiniteArrowPlacementMode(false);
        pendingChanges.Clear();
        customizationMessage = "Changes confirmed.";

        if (customizationPreviewPiece != null)
        {
            customizationPreviewPiece.RefreshLabel();
        }

        RefreshCustomizationBoard();
    }

    private void ApplyCustomizationChange(
        PieceConfiguration configuration,
        PendingCustomizationChange change
    )
    {
        switch (change.Type)
        {
            case CustomizationChangeType.MoveCell:
            case CustomizationChangeType.AttackCell:
                configuration.AddMoveOffset(change.Direction);
                configuration.AddAttackOffset(change.Direction);
                break;

            case CustomizationChangeType.InfiniteMove:
            case CustomizationChangeType.InfiniteAttack:
                configuration.AddInfiniteMoveDirection(change.Direction);
                configuration.AddInfiniteAttackDirection(change.Direction);
                break;

            case CustomizationChangeType.PawnDoubleStep:
                configuration.UnlockPawnDoubleStep();
                break;

            case CustomizationChangeType.JumpRook:
                configuration.UnlockJumpRook();
                break;
            case CustomizationChangeType.CastleSwap:
                configuration.UnlockCastleSwap();
                break;
        }
    }

    private void RefreshCustomizationBoard()
    {
        if (currentPhase != GamePhase.Customization)
        {
            return;
        }

        ClearMoveHighlights();
        ClearCustomizationArrows();

        if (selectedCustomization == null ||
            customizationTab == CustomizationTab.Ability)
        {
            return;
        }

        int forwardDirection =
            customizationTeam == PieceTeam.Player ? 1 : -1;

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                Vector2Int boardOffset =
                    new Vector2Int(x, y) - CustomizationCenter;

                if (boardOffset == Vector2Int.zero)
                {
                    continue;
                }

                Vector2Int relativeOffset = new Vector2Int(
                    boardOffset.x,
                    boardOffset.y * forwardDirection
                );
                Color? patternColor = GetCustomizationPatternColor(
                    relativeOffset
                );

                if (patternColor.HasValue)
                {
                    HighlightCustomizationTile(x, y, patternColor.Value);
                }
            }
        }

        HighlightCustomizationJumpDirections();
        RefreshCustomizationArrows();
    }

    private void HighlightCustomizationJumpDirections()
    {
        if (selectedCustomization.Type != PieceType.Rook ||
            (!selectedCustomization.HasJumpRook &&
             !HasPendingChange(
                 CustomizationChangeType.JumpRook,
                 Vector2Int.zero
             )))
        {
            return;
        }

        Vector2Int[] cardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        foreach (Vector2Int direction in cardinalDirections)
        {
            Vector2Int position = CustomizationCenter + direction;

            if (IsInsideBoard(position))
            {
                HighlightCustomizationTile(
                    position.x,
                    position.y,
                    jumpPatternColor
                );
            }
        }
    }

    private Color? GetCustomizationPatternColor(Vector2Int offset)
    {
        bool hasJumpRookPreview =
            selectedCustomization.Type == PieceType.Rook &&
            (selectedCustomization.HasJumpRook ||
             HasPendingChange(
                 CustomizationChangeType.JumpRook,
                 Vector2Int.zero
             ));

        if (hasJumpRookPreview &&
            (IsPendingPreviewPattern(offset) ||
             IsOwnedPreviewPattern(offset) ||
             IsBasePreviewPattern(offset)))
        {
            return jumpPatternColor;
        }

        if (IsPendingPreviewPattern(offset) ||
            IsOwnedPreviewPattern(offset) ||
            IsBasePreviewPattern(offset))
        {
            return GetCurrentPatternColor();
        }

        return null;
    }

    private Color GetCurrentPatternColor()
    {
        return customizationTab == CustomizationTab.Attack
            ? attackPatternColor
            : movementPatternColor;
    }

    private Color GetCurrentArrowColor()
    {
        return customizationTab == CustomizationTab.Attack
            ? attackArrowColor
            : movementArrowColor;
    }

    private bool IsBasePreviewPattern(Vector2Int offset)
    {
        PieceType type = selectedCustomization.Type;

        if (type == PieceType.King)
        {
            return Mathf.Abs(offset.x) <= 1 &&
                   Mathf.Abs(offset.y) <= 1;
        }

        if (type == PieceType.Queen)
        {
            return offset.x == 0 ||
                   offset.y == 0 ||
                   Mathf.Abs(offset.x) == Mathf.Abs(offset.y);
        }

        if (type == PieceType.Rook)
        {
            return offset.x == 0 || offset.y == 0;
        }

        if (type == PieceType.Bishop)
        {
            return Mathf.Abs(offset.x) == Mathf.Abs(offset.y);
        }

        if (type == PieceType.Knight)
        {
            int xDistance = Mathf.Abs(offset.x);
            int yDistance = Mathf.Abs(offset.y);

            return (xDistance == 2 && yDistance == 1) ||
                   (xDistance == 1 && yDistance == 2);
        }

        if (customizationTab == CustomizationTab.Movement)
        {
            return offset == new Vector2Int(0, 1);
        }

        return offset == new Vector2Int(-1, 1) ||
               offset == new Vector2Int(1, 1);
    }

    private bool IsOwnedPreviewPattern(Vector2Int offset)
    {
        if (customizationTab == CustomizationTab.Movement)
        {
            if (selectedCustomization.HasAdditionalMove(offset))
            {
                return true;
            }

            if (selectedCustomization.HasPawnDoubleStep &&
                offset == new Vector2Int(0, 2))
            {
                return true;
            }

            foreach (Vector2Int direction in
                     selectedCustomization.InfiniteMoveDirections)
            {
                if (MatchesInfiniteDirection(offset, direction))
                {
                    return true;
                }
            }
        }
        else
        {
            if (selectedCustomization.HasAdditionalAttack(offset))
            {
                return true;
            }

            foreach (Vector2Int direction in
                     selectedCustomization.InfiniteAttackDirections)
            {
                if (MatchesInfiniteDirection(offset, direction))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPendingPreviewPattern(Vector2Int offset)
    {
        if (selectedCustomization.Type == PieceType.Pawn)
        {
            if (FindPendingPawnCellChange(offset) != null)
            {
                return true;
            }

            foreach (PendingCustomizationChange change in pendingChanges)
            {
                if ((change.Type == CustomizationChangeType.InfiniteMove ||
                     change.Type == CustomizationChangeType.InfiniteAttack) &&
                    MatchesInfiniteDirection(offset, change.Direction))
                {
                    return true;
                }
            }

            if (customizationTab == CustomizationTab.Movement &&
                HasPendingChange(
                    CustomizationChangeType.PawnDoubleStep,
                    Vector2Int.zero
                ) &&
                offset == new Vector2Int(0, 2))
            {
                return true;
            }

            return false;
        }

        if (HasPendingChange(
                CustomizationChangeType.MoveCell,
                offset
            ))
        {
            return true;
        }

        foreach (PendingCustomizationChange change in pendingChanges)
        {
            if (change.Type == CustomizationChangeType.InfiniteMove &&
                MatchesInfiniteDirection(offset, change.Direction))
            {
                return true;
            }
        }

        return false;
    }

    private void HighlightCustomizationTile(int x, int y, Color color)
    {
        SpriteRenderer tileRenderer =
            gridArray[x, y].GetComponent<SpriteRenderer>();

        if (tileRenderer != null)
        {
            tileRenderer.color = Color.Lerp(
                originalTileColors[x, y],
                color,
                0.55f
            );
        }
    }

    private void RefreshCustomizationArrows()
    {
        HashSet<Vector2Int> directions = new HashSet<Vector2Int>();

        if (selectedCustomization.Type == PieceType.Rook ||
            selectedCustomization.Type == PieceType.Queen)
        {
            AddCustomizationArrow(
                new Vector2Int(0, 1),
                GetCurrentArrowColor(),
                directions
            );

            AddCustomizationArrow(
                new Vector2Int(1, 0),
                GetCurrentArrowColor(),
                directions
            );

            AddCustomizationArrow(
                new Vector2Int(0, -1),
                GetCurrentArrowColor(),
                directions
            );

            AddCustomizationArrow(
                new Vector2Int(-1, 0),
                GetCurrentArrowColor(),
                directions
            );
        }

        if (selectedCustomization.Type == PieceType.Bishop ||
            selectedCustomization.Type == PieceType.Queen)
        {
            AddCustomizationArrow(
                new Vector2Int(1, 1),
                GetCurrentArrowColor(),
                directions
            );

            AddCustomizationArrow(
                new Vector2Int(1, -1),
                GetCurrentArrowColor(),
                directions
            );

            AddCustomizationArrow(
                new Vector2Int(-1, -1),
                GetCurrentArrowColor(),
                directions
            );

            AddCustomizationArrow(
                new Vector2Int(-1, 1),
                GetCurrentArrowColor(),
                directions
            );
        }

        IReadOnlyList<Vector2Int> ownedDirections =
            customizationTab == CustomizationTab.Movement
                ? selectedCustomization.InfiniteMoveDirections
                : selectedCustomization.InfiniteAttackDirections;

        foreach (Vector2Int direction in ownedDirections)
        {
            AddCustomizationArrow(
                direction,
                GetCurrentArrowColor(),
                directions
            );
        }

        foreach (PendingCustomizationChange change in pendingChanges)
        {
            bool isPendingArrow =
                change.Type == CustomizationChangeType.InfiniteMove ||
                change.Type == CustomizationChangeType.InfiniteAttack;

            if (isPendingArrow)
            {
                AddCustomizationArrow(
                    change.Direction,
                    GetCurrentArrowColor(),
                    directions
                );
            }
        }

        if (selectedCustomization.Type == PieceType.Rook &&
            (selectedCustomization.HasJumpRook ||
             HasPendingChange(
                 CustomizationChangeType.JumpRook,
                 Vector2Int.zero
             )))
        {
            CreateParabolicArrow(
                CustomizationCenter,
                Vector2Int.up,
                jumpArrowColor,
                customizationArrowObjects
            );
            CreateParabolicArrow(
                CustomizationCenter,
                Vector2Int.right,
                jumpArrowColor,
                customizationArrowObjects
            );
            CreateParabolicArrow(
                CustomizationCenter,
                Vector2Int.down,
                jumpArrowColor,
                customizationArrowObjects
            );
            CreateParabolicArrow(
                CustomizationCenter,
                Vector2Int.left,
                jumpArrowColor,
                customizationArrowObjects
            );
        }
    }

    private void CreateParabolicArrow(
        Vector2Int startPosition,
        Vector2Int direction,
        Color color,
        List<GameObject> effectObjects
    )
    {
        CreateParabolicArrow(
            startPosition,
            direction,
            color,
            effectObjects,
            1.8f
        );
    }

    private void CreateParabolicArrow(
        Vector2Int startPosition,
        Vector2Int direction,
        Color color,
        List<GameObject> effectObjects,
        float arrowLength
    )
    {
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        const int pointCount = 7;

        for (int index = 1; index <= pointCount; index++)
        {
            float progress = index / (float)pointCount;
            Vector2 point =
                (Vector2)startPosition +
                (Vector2)direction * arrowLength * progress +
                perpendicular * Mathf.Sin(Mathf.PI * progress) * 0.35f;
            GameObject pointObject = new GameObject("JumpArcPoint");
            pointObject.transform.SetParent(transform, false);
            pointObject.transform.position =
                new Vector3(point.x, point.y, -2.2f);
            TextMesh pointText = pointObject.AddComponent<TextMesh>();
            pointText.text = index == pointCount
                ? GetArrowSymbol(direction)
                : "•";
            pointText.anchor = TextAnchor.MiddleCenter;
            pointText.alignment = TextAlignment.Center;
            pointText.fontSize = index == pointCount ? 54 : 32;
            pointText.characterSize = 0.1f;
            pointText.fontStyle = FontStyle.Bold;
            pointText.color = color;

            MeshRenderer renderer =
                pointObject.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sortingOrder = 4;
            }

            effectObjects.Add(pointObject);
        }
    }

    private void DrawBattleJumpArcs(ChessPiece piece)
    {
        if (piece.Type != PieceType.Rook ||
            !piece.Configuration.HasJumpRook)
        {
            return;
        }

        Vector2Int[] cardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        foreach (Vector2Int direction in cardinalDirections)
        {
            Vector2Int position = piece.BoardPosition + direction;

            while (IsInsideBoard(position) &&
                   pieces[position.x, position.y] == null)
            {
                position += direction;
            }

            if (!IsInsideBoard(position) ||
                !IsInsideBoard(position + direction))
            {
                continue;
            }

            Vector2Int landingPosition = position + direction;
            float arrowLength = Mathf.Abs(
                direction.x == 0
                    ? landingPosition.y - piece.BoardPosition.y
                    : landingPosition.x - piece.BoardPosition.x
            );
            CreateParabolicArrow(
                piece.BoardPosition,
                direction,
                jumpArrowColor,
                battleEffectObjects,
                arrowLength
            );
        }
    }

    private void ClearBattleEffects()
    {
        foreach (GameObject effectObject in battleEffectObjects)
        {
            if (effectObject != null)
            {
                Destroy(effectObject);
            }
        }

        battleEffectObjects.Clear();
    }

    private void AddCustomizationArrow(
        Vector2Int relativeDirection,
        Color color,
        HashSet<Vector2Int> usedDirections
    )
    {
        if (!usedDirections.Add(relativeDirection))
        {
            return;
        }

        int forwardDirection =
            customizationTeam == PieceTeam.Player ? 1 : -1;
        Vector2Int boardDirection = new Vector2Int(
            relativeDirection.x,
            relativeDirection.y * forwardDirection
        );
        Vector2Int arrowPosition = CustomizationCenter + boardDirection;

        GameObject arrowObject = new GameObject("CustomizationArrow");
        arrowObject.transform.SetParent(transform, false);
        arrowObject.transform.position = new Vector3(
            arrowPosition.x,
            arrowPosition.y,
            -2f
        );
        TextMesh arrow = arrowObject.AddComponent<TextMesh>();
        arrow.text = GetArrowSymbol(boardDirection);
        arrow.anchor = TextAnchor.MiddleCenter;
        arrow.alignment = TextAlignment.Center;
        arrow.fontSize = 72;
        arrow.characterSize = 0.13f;
        arrow.fontStyle = FontStyle.Bold;
        arrow.color = color;

        MeshRenderer arrowRenderer =
            arrowObject.GetComponent<MeshRenderer>();

        if (arrowRenderer != null)
        {
            arrowRenderer.sortingOrder = 3;
        }

        customizationArrowObjects.Add(arrowObject);
    }

    private void ClearCustomizationArrows()
    {
        foreach (GameObject arrowObject in customizationArrowObjects)
        {
            if (arrowObject != null)
            {
                Destroy(arrowObject);
            }
        }

        customizationArrowObjects.Clear();
    }

    private string GetArrowSymbol(Vector2Int direction)
    {
        if (direction == new Vector2Int(0, 1))
        {
            return "↑";
        }

        if (direction == new Vector2Int(1, 1))
        {
            return "↗";
        }

        if (direction == new Vector2Int(1, 0))
        {
            return "→";
        }

        if (direction == new Vector2Int(1, -1))
        {
            return "↘";
        }

        if (direction == new Vector2Int(0, -1))
        {
            return "↓";
        }

        if (direction == new Vector2Int(-1, -1))
        {
            return "↙";
        }

        if (direction == new Vector2Int(-1, 0))
        {
            return "←";
        }

        return "↖";
    }

    private void DrawCustomizationPanel()
    {
        GUILayout.BeginArea(
            new Rect(10f, 70f, 360f, Screen.height - 80f),
            GUI.skin.box
        );

        GUILayout.Label($"Money: ${GetMoney(customizationTeam)}");
        GUILayout.Label("Select a non-King piece to modify:");
        customizationScrollPosition = GUILayout.BeginScrollView(
            customizationScrollPosition
        );

        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team != customizationTeam)
            {
                continue;
            }

            string marker = configuration.IsModified ? " *" : "";

            if (GUILayout.Button($"{configuration.PieceName}{marker}"))
            {
                selectedCustomization = configuration;
            }
        }

        GUILayout.Space(8f);

        if (selectedCustomization != null)
        {
            GUILayout.Label($"Selected: {selectedCustomization.PieceName}");

            if (selectedCustomization.Type == PieceType.King)
            {
                GUILayout.Label("The King cannot be customized.");
            }
            else
            {
                DrawPatternCustomizationButtons(selectedCustomization);

                if (selectedCustomization.Type == PieceType.Pawn)
                {
                    DrawPawnAbilityButton(selectedCustomization);
                }
                else if (selectedCustomization.Type == PieceType.Rook)
                {
                    DrawRookAbilityButton(selectedCustomization);
                }
            }
        }

        GUILayout.EndScrollView();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Finish This Team's Customization"))
        {
            FinishCustomizationTurn();
        }

        GUILayout.EndArea();
    }

    private void DrawPatternCustomizationButtons(
        PieceConfiguration configuration
    )
    {
        DrawDirectionPurchaseGroup(
            "Add one-square movement",
            configuration.Type,
            false,
            false,
            singleSquareCost,
            configuration.HasAdditionalMove,
            configuration.AddMoveOffset
        );
        DrawDirectionPurchaseGroup(
            "Add one-square attack",
            configuration.Type,
            true,
            false,
            singleSquareCost,
            configuration.HasAdditionalAttack,
            configuration.AddAttackOffset
        );
        DrawDirectionPurchaseGroup(
            "Add infinite movement direction",
            configuration.Type,
            false,
            true,
            infiniteRangeCost,
            configuration.HasInfiniteMove,
            configuration.AddInfiniteMoveDirection
        );
        DrawDirectionPurchaseGroup(
            "Add infinite attack direction",
            configuration.Type,
            true,
            true,
            infiniteRangeCost,
            configuration.HasInfiniteAttack,
            configuration.AddInfiniteAttackDirection
        );
    }

    private void DrawPawnAbilityButton(
        PieceConfiguration configuration
    )
    {
        GUILayout.Space(6f);
        GUILayout.Label("Pawn ability");
        DrawSinglePurchaseButton(
            $"First-move double step (${pawnDoubleStepCost})",
            pawnDoubleStepCost,
            configuration.HasPawnDoubleStep,
            configuration.UnlockPawnDoubleStep
        );
    }

    private void DrawRookAbilityButton(
        PieceConfiguration configuration
    )
    {
        GUILayout.Space(6f);
        GUILayout.Label("Rook ability");
        GUILayout.Label("Rooks already have unlimited straight movement.");
        DrawSinglePurchaseButton(
            $"Jump Rook ability (${jumpRookCost})",
            jumpRookCost,
            configuration.HasJumpRook,
            configuration.UnlockJumpRook
        );
        GUILayout.Label(
            "Jump Rook may cross one occupied square, but not two."
        );
    }

    private void DrawDirectionPurchaseGroup(
        string title,
        PieceType pieceType,
        bool isAttack,
        bool isInfinite,
        int cost,
        Func<Vector2Int, bool> isPurchased,
        Action<Vector2Int> purchaseAction
    )
    {
        GUILayout.Space(6f);
        GUILayout.Label($"{title} (${cost} each)");

        for (int index = 0; index < Directions.Length; index += 2)
        {
            GUILayout.BeginHorizontal();

            for (int column = 0; column < 2; column++)
            {
                int directionIndex = index + column;
                Vector2Int direction = Directions[directionIndex];
                bool alreadyPurchased = isPurchased(direction);
                bool isBaseDirection = IsBaseDirection(
                    pieceType,
                    direction,
                    isAttack,
                    isInfinite
                );
                GUI.enabled =
                    !isBaseDirection &&
                    !alreadyPurchased &&
                    GetMoney(customizationTeam) >= cost;
                string label = DirectionNames[directionIndex];

                if (isBaseDirection)
                {
                    label += " - BASE";
                }
                else if (alreadyPurchased)
                {
                    label += " - OWNED";
                }

                if (GUILayout.Button(label))
                {
                    SpendMoney(customizationTeam, cost);
                    purchaseAction(direction);
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    private bool IsBaseDirection(
        PieceType pieceType,
        Vector2Int direction,
        bool isAttack,
        bool isInfinite
    )
    {
        if (pieceType == PieceType.Queen)
        {
            return true;
        }

        if (pieceType == PieceType.Rook)
        {
            return direction.x == 0 || direction.y == 0;
        }

        if (pieceType == PieceType.Bishop)
        {
            return Mathf.Abs(direction.x) == 1 &&
                   Mathf.Abs(direction.y) == 1;
        }

        if (pieceType == PieceType.Knight)
        {
            return false;
        }

        if (pieceType != PieceType.Pawn || isInfinite)
        {
            return false;
        }

        if (!isAttack)
        {
            return direction == new Vector2Int(0, 1);
        }

        return direction == new Vector2Int(-1, 1) ||
               direction == new Vector2Int(1, 1);
    }

    private void DrawSinglePurchaseButton(
        string label,
        int cost,
        bool alreadyPurchased,
        Action purchaseAction
    )
    {
        GUI.enabled =
            !alreadyPurchased && GetMoney(customizationTeam) >= cost;
        string buttonLabel = alreadyPurchased ? $"{label} - OWNED" : label;

        if (GUILayout.Button(buttonLabel))
        {
            SpendMoney(customizationTeam, cost);
            purchaseAction();
        }

        GUI.enabled = true;
    }

    private void FinishCustomizationTurn()
    {
        if (pendingChanges.Count > 0)
        {
            customizationMessage =
                "Confirm or cancel the current changes before finishing.";
            ShowCenteredAlert("CONFIRM OR CANCEL BEFORE FINISHING");
            return;
        }

        SetInfiniteArrowPlacementMode(false);

        if (customizationTeam == PieceTeam.Player)
        {
            customizationTeam = PieceTeam.Enemy;
            selectedCustomization =
                GetFirstCustomizablePiece(customizationTeam);
            customizationTab = CustomizationTab.Movement;
            customizationMessage =
                "Red customization started. Choose a piece.";
            CreateCustomizationPreview();
            RefreshCustomizationBoard();
            return;
        }

        if (customizationPreviewPiece != null)
        {
            Destroy(customizationPreviewPiece.gameObject);
            customizationPreviewPiece = null;
        }

        ClearCustomizationArrows();
        ClearMoveHighlights();
        currentPhase = GamePhase.Deployment;
        deploymentTeam = PieceTeam.Player;
        ClearDeploymentGhost();
        selectedDeploymentPiece = null;
        selectedCustomization = null;
        HighlightDeploymentArea();
        Debug.Log("Deployment phase started. Blue deploys first.");
    }

    private void DrawDeploymentPanel()
    {
        Rect panelRect = new Rect(10f, 70f, 230f, Screen.height - 150f);
        GUI.Box(
            panelRect,
            deploymentAwaitingFinish
                ? "DEPLOYMENT CONFIRMATION"
                : $"{GetTeamName(deploymentTeam)} RESERVE"
        );

        if (deploymentAwaitingFinish)
        {
            DrawDeploymentConfirmation(panelRect);
            return;
        }

        int visibleIndex = 0;

        foreach (PieceConfiguration configuration in pieceConfigurations)
        {
            if (configuration.Team != deploymentTeam ||
                configuration.IsDeployed ||
                (configuration.Type == PieceType.King &&
                 !AreAllNonKingPiecesDeployed(deploymentTeam)))
            {
                continue;
            }

            int column = visibleIndex % 2;
            int row = visibleIndex / 2;
            Rect cardRect = new Rect(
                panelRect.x + 10f + column * 105f,
                panelRect.y + 35f + row * 88f,
                98f,
                80f
            );
            bool isSelected =
                configuration == selectedDeploymentPiece;
            GUIStyle cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 13,
                fontStyle = isSelected
                    ? FontStyle.Bold
                    : FontStyle.Normal
            };
            if (GUI.Button(
                    cardRect,
                    GetShortPieceName(configuration),
                    cardStyle
                ))
            {
                selectedDeploymentPiece = configuration;
                SetDeploymentGhost(configuration);
                HighlightDeploymentArea();
            }

            DrawPieceImage(cardRect, configuration, isSelected);
            visibleIndex++;
        }

        GUIStyle instructionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(
            new Rect(
                panelRect.x + 10f,
                panelRect.yMax - 65f,
                panelRect.width - 20f,
                55f
            ),
            "The King appears after every other piece is deployed.",
            instructionStyle
        );
    }

    private void DrawDeploymentConfirmation(Rect panelRect)
    {
        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(
            new Rect(panelRect.x + 10f, panelRect.y + 55f, 210f, 35f),
            playerDeploymentFinished ? "BLUE ✓" : "BLUE WAITING",
            statusStyle
        );
        GUI.Label(
            new Rect(panelRect.x + 10f, panelRect.y + 95f, 210f, 35f),
            enemyDeploymentFinished ? "RED ✓" : "RED WAITING",
            statusStyle
        );
        GUIStyle finishStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };

        if (GUI.Button(
                new Rect(
                    panelRect.x + 15f,
                    panelRect.y + 155f,
                    panelRect.width - 30f,
                    48f
                ),
                $"{GetTeamName(deploymentFinishTeam)} FINISH",
                finishStyle
            ))
        {
            ConfirmDeploymentForCurrentTeam();
        }
    }

    private void ConfirmDeploymentForCurrentTeam()
    {
        if (deploymentFinishTeam == PieceTeam.Player)
        {
            playerDeploymentFinished = true;
        }
        else
        {
            enemyDeploymentFinished = true;
        }

        if (playerDeploymentFinished && enemyDeploymentFinished)
        {
            deploymentAwaitingFinish = false;
            currentPhase = GamePhase.Battle;
            currentTurn = PieceTeam.Player;
            inspectedPiece = null;
            ClearDeploymentGhost();
            ClearMoveHighlights();
            ClearBattleEffects();
            Debug.Log("Battle phase started. Blue moves first.");
            return;
        }

        deploymentAwaitingFinish = false;
        deploymentTeam = GetOpposingTeam(deploymentFinishTeam);
        selectedDeploymentPiece = null;
        inspectedPiece = null;
        ClearDeploymentGhost();
        HighlightDeploymentArea();
    }

    private void DrawPieceDetails()
    {
        if (inspectedPiece == null)
        {
            return;
        }

        PieceConfiguration configuration = inspectedPiece.Configuration;
        GUILayout.BeginArea(
            new Rect(Screen.width - 320f, 70f, 310f, 360f),
            GUI.skin.box
        );

        GUILayout.Label($"INSPECT: {configuration.PieceName}");
        GUILayout.Label($"Type: {configuration.Type}");
        GUILayout.Label($"Team: {GetTeamName(configuration.Team)}");
        GUILayout.Label(
            configuration.IsModified
                ? "Modified: Yes (*)"
                : "Modified: No"
        );

        if (configuration.AdditionalMoveOffsets.Count > 0)
        {
            GUILayout.Label(
                $"Move cells: {FormatDirections(configuration.AdditionalMoveOffsets)}"
            );
        }

        if (configuration.AdditionalAttackOffsets.Count > 0)
        {
            GUILayout.Label(
                $"Attack cells: {FormatDirections(configuration.AdditionalAttackOffsets)}"
            );
        }

        if (configuration.InfiniteMoveDirections.Count > 0)
        {
            GUILayout.Label(
                $"Infinite move: {FormatDirections(configuration.InfiniteMoveDirections)}"
            );
        }

        if (configuration.InfiniteAttackDirections.Count > 0)
        {
            GUILayout.Label(
                $"Infinite attack: {FormatDirections(configuration.InfiniteAttackDirections)}"
            );
        }

        if (configuration.HasPawnDoubleStep)
        {
            GUILayout.Label("Ability: First-move double step");
        }

        if (configuration.HasJumpRook)
        {
            GUILayout.Label("Ability: Jump Rook");
            GUILayout.Label("May cross one occupied square.");
        }

        if (configuration.HasCastleSwap)
        {
            GUILayout.Label("Ability: Castle Swap");
            GUILayout.Label(
                "May swap with an unmoved friendly King if both pieces have never moved."
            );
        }
        GUILayout.EndArea();
    }

    private string FormatDirections(
        IReadOnlyList<Vector2Int> directions
    )
    {
        List<string> names = new List<string>();

        foreach (Vector2Int direction in directions)
        {
            for (int index = 0; index < Directions.Length; index++)
            {
                if (Directions[index] == direction)
                {
                    names.Add(DirectionNames[index]);
                    break;
                }
            }
        }

        return string.Join(", ", names);
    }

    private int GetMoney(PieceTeam team)
    {
        return team == PieceTeam.Player ? playerMoney : enemyMoney;
    }

    private void SpendMoney(PieceTeam team, int amount)
    {
        if (team == PieceTeam.Player)
        {
            playerMoney -= amount;
        }
        else
        {
            enemyMoney -= amount;
        }
    }

    private string GetTeamName(PieceTeam team)
    {
        return team == PieceTeam.Player ? "BLUE" : "RED";
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
