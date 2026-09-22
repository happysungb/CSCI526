using UnityEngine;

public enum PieceTeam
{
    Player,
    Enemy
}

public class ChessPiece : MonoBehaviour
{
    public PieceTeam Team { get; private set; }

    public void Initialize(PieceTeam team, Color pieceColor)
    {
        Team = team;

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
    }
}