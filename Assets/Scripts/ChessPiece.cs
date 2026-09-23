using UnityEngine;

public enum PieceTeam
{
    Player,
    Enemy
}

public enum PieceType
{
    King,
    Queen,
    Rook,
    Bishop,
    Knight,
    Pawn
}

public class ChessPiece : MonoBehaviour
{
    private const float LabelZOffset = -0.1f;

    public PieceTeam Team { get; private set; }
    public PieceType Type { get; private set; }
    public Vector2Int BoardPosition { get; private set; }
    public bool HasMoved { get; private set; }

    public void Initialize(
        PieceTeam team,
        PieceType type,
        Vector2Int boardPosition,
        Color pieceColor
    )
    {
        Team = team;
        Type = type;
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

    private void CreateTypeLabel(Color pieceColor)
    {
        GameObject labelObject = new GameObject("PieceLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition =
            new Vector3(0f, 0f, LabelZOffset);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = GetTypeLabel();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = 0.12f;
        label.fontStyle = FontStyle.Bold;
        label.color = GetReadableLabelColor(pieceColor);

        MeshRenderer labelRenderer =
            labelObject.GetComponent<MeshRenderer>();

        if (labelRenderer != null)
        {
            labelRenderer.sortingOrder = 1;
        }
    }

    private string GetTypeLabel()
    {
        switch (Type)
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

    private Color GetReadableLabelColor(Color backgroundColor)
    {
        float brightness =
            backgroundColor.r * 0.299f +
            backgroundColor.g * 0.587f +
            backgroundColor.b * 0.114f;

        return brightness > 0.5f ? Color.black : Color.white;
    }
}
