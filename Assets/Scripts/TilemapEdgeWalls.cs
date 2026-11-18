using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapEdgeWalls : MonoBehaviour
{
    [Header("Map")]
    public Tilemap tilemap;

    [Header("Wall Settings")]
    public float wallThickness = 0.5f;
    public float wallHeight = 5f;

    [Header("Wall Sprite")]
    [Tooltip("Leave empty for invisible walls.")]
    public Sprite wallSprite;

    [Tooltip("Scales the sprite so one copy is exactly one tile long. " +
             "Anything that doesn't fit (like the sprite's extra height) is cut off.")]
    public bool fitSpriteToCell = true;

    [Tooltip("Maximum height of the standing wall sprite. The sprite is cut off at this " +
             "height if it's taller, and it never repeats vertically.")]
    public float spriteHeight = 1f;

    [Tooltip("Pushes the sprite away from the floor so it doesn't clip the tilemap. " +
             "The collider stays where it is. Corners are adjusted automatically.")]
    public float spriteOffset = 0.3f;

    [Header("Sorting")]
    [Tooltip("Draws the wall sprites on the tilemap's sorting layer, one order above it, " +
             "so they never flicker in front of / behind the tilemap.")]
    public bool drawAboveTilemap = true;

    [Tooltip("Used when drawAboveTilemap is off. Otherwise added on top of the tilemap's order.")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    static readonly Vector3Int[] Directions =
    {
        Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
    };

    void Start()
    {
        BuildWalls();
    }

    void BuildWalls()
    {
        tilemap.CompressBounds();

        // Container so the Hierarchy stays tidy
        GameObject container = new GameObject("EdgeWalls");
        container.transform.SetParent(tilemap.transform, false);

        BoundsInt bounds = tilemap.cellBounds;

        foreach (Vector3Int dir in Directions)
        {
            // Walls along a left/right edge run vertically, up/down edges run horizontally
            Vector3Int along = (dir.x == 0) ? Vector3Int.right : Vector3Int.up;
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                if (!IsExposed(pos, dir) || visited.Contains(pos)) continue;

                // Merge neighbouring exposed edges into one long wall
                int count = 1;
                visited.Add(pos);

                Vector3Int next = pos + along;
                while (IsExposed(next, dir) && !visited.Contains(next))
                {
                    visited.Add(next);
                    count++;
                    next += along;
                }

                // If there's a tile right past either end of the run, the wall turns an
                // inner corner there (the neighbouring wall comes towards this one).
                bool startConcave = tilemap.HasTile(pos - along);
                bool endConcave = tilemap.HasTile(next);

                CreateWall(container.transform, pos, dir, along, count, startConcave, endConcave);
            }
        }
    }

    // A tile edge is exposed if this cell has a tile but the neighbour in "dir" doesn't
    bool IsExposed(Vector3Int pos, Vector3Int dir)
    {
        return tilemap.HasTile(pos) && !tilemap.HasTile(pos + dir);
    }

    void CreateWall(Transform parent, Vector3Int startCell, Vector3Int dir, Vector3Int along, int count,
                    bool startConcave, bool endConcave)
    {
        Vector3 startCenter = tilemap.GetCellCenterLocal(startCell);

        // Step vectors in the tilemap's local space (handles cell swizzle, e.g. XZY)
        Vector3 dirStep = tilemap.GetCellCenterLocal(startCell + dir) - startCenter;
        Vector3 alongStep = tilemap.GetCellCenterLocal(startCell + along) - startCenter;

        Vector3 dirAxis = Abs(dirStep).normalized;
        Vector3 alongAxis = Abs(alongStep).normalized;
        Vector3 heightAxis = Vector3.one - dirAxis - alongAxis;

        Vector3 center = startCenter
                         + dirStep * 0.5f
                         + dirStep.normalized * (wallThickness * 0.5f)
                         + alongStep * (count - 1) * 0.5f;

        float length = alongStep.magnitude * count;

        Vector3 size = Abs(alongStep) * count
                       + dirAxis * wallThickness
                       + heightAxis * wallHeight;

        GameObject wall = new GameObject("Wall");
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = center;

        BoxCollider col = wall.AddComponent<BoxCollider>();
        col.size = size;
        col.isTrigger = false;

        if (wallSprite != null)
        {
            CreateWallSprite(wall.transform, dirStep, alongStep, length, startConcave, endConcave);
        }
    }

    void CreateWallSprite(Transform wall, Vector3 dirStep, Vector3 alongStep, float length,
                          bool startConcave, bool endConcave)
    {
        // Directions in world space
        Vector3 outward = tilemap.transform.TransformDirection(dirStep.normalized);
        Vector3 alongDir = tilemap.transform.TransformDirection(alongStep.normalized);

        GameObject spriteObj = new GameObject("WallSprite");
        spriteObj.transform.SetParent(wall, false);

        SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
        sr.sprite = wallSprite;

        // Sorting: sit just above the tilemap so the two never swap draw order
        TilemapRenderer tr = tilemap.GetComponent<TilemapRenderer>();
        if (drawAboveTilemap && tr != null)
        {
            sr.sortingLayerID = tr.sortingLayerID;
            sr.sortingOrder = tr.sortingOrder + 1 + sortingOrder;
        }
        else
        {
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
        }

        // Tiled + Continuous: crops anything that doesn't fit inside sr.size
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;

        float scale = 1f;
        if (fitSpriteToCell)
        {
            // One copy of the sprite = exactly one tile wide
            scale = alongStep.magnitude / wallSprite.bounds.size.x;
        }

        // Height is limited by spriteHeight (crops if the sprite is taller)
        // but never exceeds one copy of the sprite (no vertical repeating)
        float spriteNaturalHeight = wallSprite.bounds.size.y * scale;
        float finalHeight = Mathf.Min(spriteHeight, spriteNaturalHeight);

        // Corner handling: since the sprite is pushed out by spriteOffset, grow it at outer
        // corners (to meet the next wall's sprite) and shrink it at inner corners (so the
        // two sprites don't cross each other).
        float startExt = startConcave ? -spriteOffset : spriteOffset;
        float endExt = endConcave ? -spriteOffset : spriteOffset;
        float spriteLength = Mathf.Max(length + startExt + endExt, 0.01f);
        float alongShift = (endExt - startExt) * 0.5f;

        spriteObj.transform.localScale = new Vector3(scale, scale, 1f);
        sr.size = new Vector2(spriteLength / scale, finalHeight / scale);

        // Stand the sprite upright on the floor edge, push it outward by spriteOffset
        // (the collider is not affected), and slide it to account for the corner extension.
        spriteObj.transform.position = wall.position
                                       - outward * (wallThickness * 0.5f)
                                       + outward * spriteOffset
                                       + alongDir * alongShift
                                       + Vector3.up * (finalHeight * 0.5f);
        spriteObj.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
    }

    static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}