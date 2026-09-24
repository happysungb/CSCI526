using System.Collections.Generic;
using UnityEngine;

public enum PieceTeam
{
    Player,
    Enemy
}

public enum PieceType
{
    King,
    Rook,
    Pawn
}

[System.Serializable]
public class PieceConfiguration
{
    private readonly List<Vector2Int> additionalMoveOffsets =
        new List<Vector2Int>();
    private readonly List<Vector2Int> additionalAttackOffsets =
        new List<Vector2Int>();
    private readonly List<Vector2Int> infiniteMoveDirections =
        new List<Vector2Int>();
    private readonly List<Vector2Int> infiniteAttackDirections =
        new List<Vector2Int>();

    public string PieceName { get; private set; }
    public PieceTeam Team { get; private set; }
    public PieceType Type { get; private set; }
    public bool HasPawnDoubleStep { get; private set; }
    public bool HasJumpRook { get; private set; }
    public bool IsDeployed { get; set; }

    public IReadOnlyList<Vector2Int> AdditionalMoveOffsets =>
        additionalMoveOffsets;
    public IReadOnlyList<Vector2Int> AdditionalAttackOffsets =>
        additionalAttackOffsets;
    public IReadOnlyList<Vector2Int> InfiniteMoveDirections =>
        infiniteMoveDirections;
    public IReadOnlyList<Vector2Int> InfiniteAttackDirections =>
        infiniteAttackDirections;

    public bool IsModified =>
        additionalMoveOffsets.Count > 0 ||
        additionalAttackOffsets.Count > 0 ||
        infiniteMoveDirections.Count > 0 ||
        infiniteAttackDirections.Count > 0 ||
        HasPawnDoubleStep ||
        HasJumpRook;

    public PieceConfiguration(
        string pieceName,
        PieceTeam team,
        PieceType type
    )
    {
        PieceName = pieceName;
        Team = team;
        Type = type;
    }

    public bool HasAdditionalMove(Vector2Int offset)
    {
        return additionalMoveOffsets.Contains(offset);
    }

    public bool HasAdditionalAttack(Vector2Int offset)
    {
        return additionalAttackOffsets.Contains(offset);
    }

    public bool HasInfiniteMove(Vector2Int direction)
    {
        return infiniteMoveDirections.Contains(direction);
    }

    public bool HasInfiniteAttack(Vector2Int direction)
    {
        return infiniteAttackDirections.Contains(direction);
    }

    public void AddMoveOffset(Vector2Int offset)
    {
        if (!additionalMoveOffsets.Contains(offset))
        {
            additionalMoveOffsets.Add(offset);
        }
    }

    public void AddAttackOffset(Vector2Int offset)
    {
        if (!additionalAttackOffsets.Contains(offset))
        {
            additionalAttackOffsets.Add(offset);
        }
    }

    public void AddInfiniteMoveDirection(Vector2Int direction)
    {
        if (!infiniteMoveDirections.Contains(direction))
        {
            infiniteMoveDirections.Add(direction);
        }
    }

    public void AddInfiniteAttackDirection(Vector2Int direction)
    {
        if (!infiniteAttackDirections.Contains(direction))
        {
            infiniteAttackDirections.Add(direction);
        }
    }

    public void UnlockPawnDoubleStep()
    {
        HasPawnDoubleStep = true;
    }

    public void UnlockJumpRook()
    {
        HasJumpRook = true;
    }
}

public class ChessPiece : MonoBehaviour
{
    private const float LabelZOffset = -0.1f;

    private TextMesh typeLabel;

    public PieceTeam Team { get; private set; }
    public PieceType Type { get; private set; }
    public Vector2Int BoardPosition { get; private set; }
    public bool HasMoved { get; private set; }
    public PieceConfiguration Configuration { get; private set; }

    public void Initialize(
        PieceConfiguration configuration,
        Vector2Int boardPosition,
        Color pieceColor
    )
    {
        Configuration = configuration;
        Team = configuration.Team;
        Type = configuration.Type;
        BoardPosition = boardPosition;
        HasMoved = false;

        SpriteRenderer pieceRenderer =
            GetComponentInChildren<SpriteRenderer>();

        if (pieceRenderer != null)
        {
            pieceRenderer.color = pieceColor;
        }
        else
        {
            Debug.LogWarning(
                $"{gameObject.name} does not have a SpriteRenderer component."
            );
        }

        CreateTypeLabel(pieceColor);
    }

    public void MoveTo(Vector2Int boardPosition, Vector3 worldPosition)
    {
        BoardPosition = boardPosition;
        HasMoved = true;
        transform.position = worldPosition;
    }

    public void RefreshLabel()
    {
        if (typeLabel != null)
        {
            typeLabel.text = GetTypeLabel();
        }
    }

    private void CreateTypeLabel(Color pieceColor)
    {
        GameObject labelObject = new GameObject("PieceLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition =
            new Vector3(0f, 0f, LabelZOffset);

        typeLabel = labelObject.AddComponent<TextMesh>();
        typeLabel.text = GetTypeLabel();
        typeLabel.anchor = TextAnchor.MiddleCenter;
        typeLabel.alignment = TextAlignment.Center;
        typeLabel.fontSize = 64;
        typeLabel.characterSize = 0.12f;
        typeLabel.fontStyle = FontStyle.Bold;
        typeLabel.color = GetReadableLabelColor(pieceColor);

        MeshRenderer labelRenderer =
            labelObject.GetComponent<MeshRenderer>();

        if (labelRenderer != null)
        {
            labelRenderer.sortingOrder = 1;
        }
    }

    private string GetTypeLabel()
    {
        string label;

        switch (Type)
        {
            case PieceType.King:
                label = "K";
                break;
            case PieceType.Rook:
                label = "R";
                break;
            default:
                label = "P";
                break;
        }

        return Configuration != null && Configuration.IsModified
            ? $"{label}*"
            : label;
    }

    private Color GetReadableLabelColor(Color backgroundColor)
    {
        float brightness =
            backgroundColor.r * 0.299f +
            backgroundColor.g * 0.587f +
            backgroundColor.b * 0.114f;

        return brightness > 0.5f ? Color.black : Color.white;
    }
}
