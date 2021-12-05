using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{

    public Texture2D texture;
    public int depth = 20;

    private GameObject terrain;
    private Vector3 position;

    public static readonly int MaxHeightMapRes = 4097;

    public Vector3 Position
    {
        get
        {
            return position;
        }
        set
        {
            position = value;
            if (terrain != null)
                terrain.transform.position = value;
        }
    }
    public float Width { get; set; }
    public float Length { get; set; }

    // Start is called before the first frame update
    void Start()
    {
        texture = Resources.Load<Texture2D>("Textures/grass");
        TerrainData tData = new TerrainData
        {
            size = new Vector3(Length, depth, Width)
        };
        TerrainLayer[] terrainTexture = new TerrainLayer[1];
        terrainTexture[0] = new TerrainLayer
        {
            diffuseTexture = texture
        };
        tData.terrainLayers = terrainTexture;
        terrain = Terrain.CreateTerrainGameObject(tData);
        terrain.transform.position = position;
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}
