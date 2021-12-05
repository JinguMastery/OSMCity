using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum PredictionMethod
{
    None, Text, Picture
}

public enum CoordinatesFormat
{
    LatLonDeg, LatLonDMS, Space
}

public readonly struct Boundaries        //définition de la structure immutable 'Boundaries'
{
    public Boundaries(double minLat, double maxLat, double minLon, double maxLon)
    {
        MinLon = minLon;
        MaxLon = maxLon;
        MinLat = minLat;
        MaxLat = maxLat;
    }

    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLon { get; }
    public double MaxLon { get; }

}


public class Main : MonoBehaviour
{

    // constantes utiles pour la conversion des latitudes et longitudes (°) en distances (m)
    public const int EarthCircum = 40075017;
    public const string XmlPath = "Assets/Resources/XmlFiles/";
    public const string TxtPath = "Assets/Resources/TxtFiles/";
    public static readonly int[] LatDegDists = { 110574, 110649, 110852, 111132, 111412, 111618, 111694 };

    // champs pouvant être modifiés par l'utilisateur à l'initialisation
    [Header("Model scaling parameters")]
    [Tooltip("Scaling parameters must be between 0 excluded and 100 included")]
    [Range(0f, 100f)]
    public float xMeterScale = 1f;
    [Range(0f, 100f)]
    public float yMeterScale = 1f;
    [Range(0f, 100f)]
    public float zMeterScale = 1f;
    [Header("Coordinates format")]
    public CoordinatesFormat format = CoordinatesFormat.Space;
    [Header("Height of the terrain")]
    [Tooltip("Height must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float terrainHeight;
    [Header("Radius to search neighbors inside")]
    [Tooltip("Radius must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float radius = 50;
    [Header("Number of neighbors to search inside this radius")]
    [Tooltip("If negative, we search neighbors among all city objects")]
    [Delayed]
    public int nNeighbors = 100;
    [Header("Hiding object meshes in the hierarchy window ?")]
    public bool hideMeshInHierarchy;
    [Header("Destination object ID")]
    [Tooltip("ID must be positive")]
    [Min(0)]
    public long destID;

    private double terrainCenterLon, terrainCenterLat;      //the longitude/latitude of the center of the terrain
    private Vector3 terrainCenterCoords;        //the earth coordinates of the center of the terrain
    private TerrainGenerator terrain;
    private Boundaries terrainBounds;
    private Text coordsText, distFromDest;
    private CityObject destination;
    private long prevDestID;
    private List<Loader> loaders = new List<Loader>();

    //associe une distance en m chaque latitude entière en ° 
    private static readonly Dictionary<int, int> LatDegDistsDict = new Dictionary<int, int>();

    public GameObject Controller { get; private set; }
    public bool FinishedAllLoading { get; private set; }


    // Start is called before the first frame update
    void Start()
    {
        BuildingLoader buildingLoader = gameObject.AddComponent<BuildingLoader>();
        loaders.Add(buildingLoader);
        buildingLoader.Main = this;
        Controller = GameObject.Find("/FPSController");
        coordsText = GameObject.Find("/Canvas/CoordsText").GetComponent<Text>();
        distFromDest = GameObject.Find("/Canvas/DistFromDest").GetComponent<Text>();
        for (int i = 0; i <= 90; i++)
        {
            LatDegDistsDict.Add(i, GetLatDegDist(i));
        }
    }

    // Update is called once per frame
    void Update()
    {
        //teste la validité des entrées
        if (xMeterScale == 0f)
            xMeterScale = .001f;
        if (yMeterScale == 0f)
            yMeterScale = .001f;
        if (zMeterScale == 0f)
            zMeterScale = .001f;
        if (destID < 0)
            destID = prevDestID;

        if (!FinishedAllLoading)
        {
            bool finishedLoading = true;
            //test if each loader finished loading its data
            foreach (var loader in loaders)
            {
                if (!loader.FinishedLoading)
                    finishedLoading = false;
            }
            if (finishedLoading)
            {
                GenerateTerrain();
                FinishedAllLoading = true;
            }
        }
        else
        {
            //met à jour la latitude/longitude actuelle de l'avatar
            UpdateCoordsText();
        }

        if (destID != prevDestID)
        {
            var dest = from cityObj in loaders.First().CityObjects
                       where cityObj.OsmObject.Element.Id == destID
                       select cityObj;
            if (dest.Any())
            {
                destination = dest.First();
            }
            else
            {
                destination = null;
            }
            prevDestID = destID;
        }
        //met à jour la distance à parcourir vers la destination
        if (destination != null && destination.Barycenter.HasValue) 
        {
            Vector3 controllerPos = new Vector3(Controller.transform.position.x / xMeterScale, Controller.transform.position.y / yMeterScale, Controller.transform.position.z / zMeterScale);
            float d = CityObject.ComputeDist(destination.Barycenter.Value, controllerPos);
            distFromDest.text = $"DIST = {d}m";
        }
        else
        {
            if (destID != 0)
                distFromDest.text = "DEST NOT FOUND";
            else
                distFromDest.text = "";
        }
    }

    private void GenerateTerrain()
    {
        GameObject go = new GameObject
        {
            name = "TerrainGenerator"
        };
        terrain = go.AddComponent<TerrainGenerator>();

        //find terrain bounds
        List<double> minLats = new List<double>();
        List<double> maxLats = new List<double>();
        List<double> minLons = new List<double>();
        List<double> maxLons = new List<double>();
        foreach (var loader in loaders)
        {
            minLats.Add(loader.Bounds.MinLat);
            maxLats.Add(loader.Bounds.MaxLat);
            minLons.Add(loader.Bounds.MinLon);
            maxLons.Add(loader.Bounds.MaxLon);
        }
        terrainBounds = new Boundaries(minLats.Min(), maxLats.Max(), minLons.Min(), maxLons.Max());

        terrainCenterLon = (terrainBounds.MaxLon + terrainBounds.MinLon) / 2;
        terrainCenterLat = (terrainBounds.MaxLat + terrainBounds.MinLat) / 2;
        terrainCenterCoords = GetEarthCoords(terrainCenterLat, terrainCenterLon);

        Vector3 maxCoords = GetTerrainCoords(terrainBounds.MaxLat, terrainBounds.MaxLon);
        Vector3 minCoords = GetTerrainCoords(terrainBounds.MinLat, terrainBounds.MinLon);
        terrain.Length = maxCoords.x - minCoords.x;
        terrain.Width = maxCoords.z - minCoords.z;
        terrain.Position = new Vector3(-terrain.Length / 2, terrainHeight, -terrain.Width / 2);
    }

    private int GetLatDegDist(int lat)
    {
        if (lat == 90)
            return LatDegDists[LatDegDists.Length - 1];
        int indLat = lat * (LatDegDists.Length - 1) / 90;
        int d = LatDegDists[indLat + 1] - LatDegDists[indLat];
        return LatDegDists[indLat] + (lat - indLat * 15) * d / 15;
    }

    private void UpdateCoordsText()
    {
        float xMeter = Controller.transform.position.x / xMeterScale;
        float zMeter = Controller.transform.position.z / zMeterScale;
        if (format == CoordinatesFormat.Space)
        {
            float yMeter = Controller.transform.position.y / yMeterScale;
            coordsText.text = $"X = {xMeter}m\nZ = {zMeter}m\nY = {yMeter}m";
        }
        else
        {
            double zPos = zMeter / LatDegDists.Average();
            double xPos = xMeter / EarthCircum / Math.Cos(zPos) * 360d;
            double lat = zPos + terrainCenterLat;
            double lon = xPos + terrainCenterLon;
            if (format == CoordinatesFormat.LatLonDeg)
            {
                coordsText.text = $"LAT = {lat}\nLON = {lon}";
            }
            else
            {
                coordsText.text = $"LAT = {ToDMSFormat(lat)}\nLON = {ToDMSFormat(lon)}";
            }
        }
    }

    private string ToDMSFormat(double deg)
    {
        int intPart = (int)deg;
        double decPart = deg - intPart;
        int m = (int)(decPart * 60d);
        double decPart2 = decPart * 60d - m;
        int s = Mathf.RoundToInt((float)(decPart2 * 60d));
        return $"{intPart}.{m}'{s}''";
    }

    public Vector3 GetTerrainCoords(double lat, double lon)
    {
        Vector3 terrainCoords = GetEarthCoords(lat, lon) - terrainCenterCoords;
        return new Vector3(terrainCoords.x, terrainHeight, terrainCoords.z);
    }


    private Vector3 GetEarthCoords(double lat, double lon)
    {
        int intLat = Mathf.RoundToInt(Mathf.Abs((float)lat));
        double avgDist = LatDegDistsDict.Values.ToList().GetRange(0, intLat + 1).Average();
        double zMeter = lat * avgDist ;
        double xMeter = lon * EarthCircum / 360d ;
        double x = xMeter * xMeterScale;
        double z = zMeter * zMeterScale;
        return new Vector3((float)x, 0, (float)z);
    }

    public float DistFromNullIsland(double lat, double lon)
    {
        return GetEarthCoords(lat, lon).magnitude;
    }

}
