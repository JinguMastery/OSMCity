using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UI;

public class Building : CityObject
{

    //the height is used for all buildings where the real height is not known (its value should correspond to the approximative height of a floor)
    [Header("Building attributes")]
    [Tooltip("Height must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float height = 3f;
    [Tooltip("Number of floors must be greater or equal to 1")]
    [Min(1)]
    [Delayed]
    public int nFloors = 1;
    [Tooltip("Surface must be strictly positive")]
    [Min(0f)]
    public float netInternalSurface;
    public string buildingType = "yes";
    public string age;
    public Color color = Color.clear;
    [Tooltip("House number must be positive")]
    [Min(0)]
    public int houseNumber;
    [Tooltip("Post code must be positive")]
    [Min(0)]
    public int postCode;
    public string street, city, country;

    [Header("Picture of a face of the building")]
    public Texture2D picture;
    [Header("Using width/length, a color marker, and controller position ?")]
    public bool isWidth = true;
    public bool useMarker = true;
    public bool useControllerPos = false;
    [Header("Colors used for edge detection and marker")]
    public Color edgeCol = Color.white;
    public Color markerCol = Color.red;
    [Header("Tolerance used for testing the equality of colors")]
    [Tooltip("Tolerance value must be between 0 and 1 included")]
    [Range(0f, 1f)]
    public double tolCol = 0;
    [Header("Minimal number of pixels of vertical lines for edge detection")]
    [Tooltip("Number of pixels must be greater or equal to 1")]
    [Min(1)]
    public int nPixelsEdge = 20;
    [Header("Method used for the height prediction")]
    public PredictionMethod predMethod;

    private float prevHeight, prev_height, prevNetInternalSurface;
    private float? heightCache;
    private bool prevIsWidth = true, prevUseMarker = true, prevUseControllerPos = false;
    private Color prevEdgeCol = Color.white, prevMarkerCol = Color.red, prevColor;
    private double prevTolCol;
    private int prevNFloors = 1, prevNPixelsEdge = 20, pictureWidth, pictureHeight;
    private Texture2D prevPicture;
    private Vector3[] positions;
    private List<float> mergedSideLengths;
    private List<Vector3> mergedSideVectors;

    //attributs cadastres
    public int NFloors
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("building:levels", out string nLevels))
            {
                try
                {
                    nFloors = int.Parse(nLevels, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + nLevels);
                }
            }
            return nFloors;
        }
    }

    public string Type
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("building", out string type))
            {
                buildingType = type;
            }
            return buildingType;
        }
    }

    public string Age
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("start_date", out string date))
            {
                try
                {
                    TimeSpan timespan = DateTime.Now - DateTime.ParseExact(date, "yyyy", CultureInfo.InvariantCulture);
                    age = timespan.ToString();
                    return age;
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + date);
                }
            }
            return age;
        }
    }

    public float NetInternalSurface
    {
        get
        {
            return GroundArea * NFloors;
        }
    }
    
    //attributs géométriques
    public float Height
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("height", out string strHeight))
            {
                float h = 0;
                try
                {
                    if (strHeight.Contains('m'))
                    {
                        h = float.Parse(strHeight.Substring(0, strHeight.IndexOf('m') - 1), CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        if (strHeight.Contains('\'') || strHeight.Contains('\"'))
                        {
                            h = FeetsInchesToMeters(strHeight);
                        }
                        else
                        {
                            h = float.Parse(strHeight, CultureInfo.InvariantCulture);
                        }
                    }
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + strHeight);
                }
                if (h == 0f)
                {
                    return GetHeight(predMethod);
                }
                else
                {
                    height = h * osmObj.Loader.Main.yMeterScale;
                    return height;
                }
            }
            else
            {
                return GetHeight(predMethod);
            }
        }
    }

    public float LongestSide
    {
        get
        {
            // Find longest merged side
            return mergedSideLengths.Count > 0 ? mergedSideLengths.Max() : float.NaN;
        }
    }

    public float ShortestSide
    {
        get
        {
            // Find shortest merged side
            return mergedSideLengths.Count > 0 ? mergedSideLengths.Min() : float.NaN;
        }
    }

    public bool IsParallelogram
    {
        get
        {
            if (positions != null && positions.Length >= 4)
            {
                // Find corner vertices (skip intermediate ones that are collinear)
                List<Vector3> cornerPositions = new List<Vector3>();
                float collinearTolerance = 1f; // tolerance for detecting collinear points

                for (int i = 0; i < positions.Length; i++)
                {
                    int prevIdx = (i - 1 + positions.Length) % positions.Length;
                    int nextIdx = (i + 1) % positions.Length;

                    Vector3 edgeBefore = positions[i] - positions[prevIdx];
                    Vector3 edgeAfter = positions[nextIdx] - positions[i];

                    float angle = Vector3.Angle(edgeBefore, edgeAfter);

                    // If angle ≈ 180°, vertex is intermediate/collinear - skip it
                    if (Mathf.Abs(angle - 180f) > collinearTolerance)
                    {
                        cornerPositions.Add(positions[i]);
                    }
                }

                // We should have exactly 4 corners for a parallelogram
                if (cornerPositions.Count != 4)
                {
                    return false;
                }

                // Calculate angles between the 4 corners
                Vector3 v1 = cornerPositions[1] - cornerPositions[0];
                Vector3 v2 = cornerPositions[2] - cornerPositions[1];
                Vector3 v3 = cornerPositions[3] - cornerPositions[2];
                Vector3 v4 = cornerPositions[0] - cornerPositions[3];  // Close the polygon

                // For a parallelogram, opposite sides must be parallel
                // Parallel = dot product of normalized vectors ≈ ±1
                float dot13 = Mathf.Abs(Vector3.Dot(v1.normalized, v3.normalized));  // Opposite sides 1 & 3
                float dot24 = Mathf.Abs(Vector3.Dot(v2.normalized, v4.normalized));  // Opposite sides 2 & 4

                float parallelTolerance = 0.99f; // dot product threshold for parallel sides

                bool isParallelogram =
                    dot13 > parallelTolerance &&  // Sides 1 and 3 are parallel
                    dot24 > parallelTolerance;    // Sides 2 and 4 are parallel

                return isParallelogram;
            }
            return false;
        }
    }

    public float LengthAngle
    {
        get
        {
            if (mergedSideLengths.Count > 0 && mergedSideVectors.Count > 0)
            {
                int ind = mergedSideLengths.IndexOf(LongestSide);
                Vector3 longestSideVec = ind >= 0 && ind < mergedSideVectors.Count ? mergedSideVectors[ind] : Vector3.zero;

                if (longestSideVec == Vector3.zero)
                {
                    return 0.0f;
                }

                float angle = Mathf.Atan2(longestSideVec.x, longestSideVec.z);
                if (angle < 0)
                    angle += Mathf.PI * 2;  // Normalize to [0, 2π]
                if (angle >= Mathf.PI)
                    angle -= Mathf.PI;
                return angle * Mathf.Rad2Deg;
            }
            return 0.0f;
        }
    }

    public float WidthAngle
    {
        get
        {
            if (mergedSideLengths.Count > 0 && mergedSideVectors.Count > 0)
            {
                float angle = LengthAngle + 90f;  // Perpendicular to the length angle
                // Normalize to [0, 180) to match LengthAngle range
                if (angle >= 180f)
                    angle -= 180f;
                return angle;
            }
            return 90f;
        }
    }

    //attributs qualitatifs relatifs aux bâtiments
    public int HouseNumber
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("addr:housenumber", out string number))
            {
                try
                {
                    houseNumber = int.Parse(number, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + number);
                }
            }
            return houseNumber;
        }
    }

    public int PostCode
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("addr:postcode", out string code))
            {
                try
                {
                    postCode = int.Parse(code, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + code);
                }
            }
            return postCode;
        }
    }

    public string Street
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("addr:street", out string street))
            {
                this.street = street;
            }
            return this.street;
        }
    }

    public string City
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("addr:city", out string city))
            {
                this.city = city;
            }
            return this.city;
        }
    }

    public string Country
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("addr:country", out string country))
            {
                this.country = country;
            }
            return this.country;
        }
    }

    public Color BuildingColor
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("building:colour", out string strColor))
            {
                Color? col = Convert(strColor);
                color = col ?? Color.clear;
            }
            return color;
        }
    }

    public Material BuildingMaterial
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("building:material", out string strMaterial))
            {
                surface = strMaterial;
                Material mat = Resources.Load<Material>("Materials/" + strMaterial);
                if (mat != null)
                    material = mat;
            }
            return material;
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        trueMiscellaneous = GetMiscellaneous();
        miscellaneous = trueMiscellaneous;
        trueNodePositions = GetNodePositionsText();
        nodePositions = trueNodePositions;
        picture = Resources.Load<Texture2D>("Pictures/" + Name);
        if (picture == null)
            picture = Resources.Load<Texture2D>("Pictures/" + osmObj.Element.Id);
        prevPicture = picture;
        predMethod = ((BuildingLoader)osmObj.Loader).Fields.predMethod;

        // Add a material
        if (Surface != null)
            material = Resources.Load<Material>("Materials/" + surface);
        if (BuildingMaterial == null)
            material = BuildingLoader.DefBuildingMat;
        prevMat = material;
        prevSurface = surface;
        prevColor = BuildingColor;
        prevHeight = Height; prev_height = prevHeight;

        if (osmObj.Element.Type == OsmGeoType.Relation)
        {
            CreateMultiPolygon((Relation)osmObj.Element);
        }
        else
        {
            if (osmObj.Element.Type == OsmGeoType.Way)
            {
                Vector3[] pos = GetNodePositions(osmObj.SubNodes);
                if (pos != null)
                {
                    if (pos.Length > 2)
                        CreatePolygon(pos);
                    else
                    {
                        if (pos.Length == 2)
                        {
                            AddPrimitive(pos[0]);
                            AddPrimitive(pos[1]);
                        }
                        else
                        {
                            if (pos.Length == 1)
                                AddPrimitive(pos[0]);
                        }
                    }
                }
            }
            else
            {
                AddPrimitive((Node)osmObj.Element);
            }
        }

        CalculateMergedSides();
        if (osmObj.Element.Tags.ContainsKey("roof:shape"))
            CreateRoof();
        else
            UpdateRoofMaterial(BuildingLoader.DefRoofMat);

        IsMeshCreated = true;
        prev_height = height;
        prevNetInternalSurface = NetInternalSurface;
        prevNFloors = NFloors;
        // initialisation des champs restants
        _ = Length; _ = Width; _ = Amenity; _ = Source; _ = Elevation; _ = Surface;
        // attributs relatifs aux bâtiments
        _ = Type; _ = Age; _ = HouseNumber; _ = PostCode; _ = Street; _ = City; _ = Country;
    }

    // Update is called once per frame
    void Update()
    {
        //teste la validité des entrées
        if (height <= 0)
            height = prev_height;
        if (netInternalSurface <= 0)
            netInternalSurface = prevNetInternalSurface;
        if (nFloors < 1)
            nFloors = prevNFloors;
        if (width <= 0)
            width = prevWidth;
        if (length <= 0)
            length = prevLength;
        if (tolCol < 0 || tolCol > 1)
            tolCol = prevTolCol;
        if (nPixelsEdge <= 0)
            nPixelsEdge = prevNPixelsEdge;
        if (nodePositions != trueNodePositions)
            nodePositions = trueNodePositions;
        if (miscellaneous != trueMiscellaneous)
            miscellaneous = trueMiscellaneous;

        //met à jour la position
        transform.position = Barycenter ?? Vector3.zero;

        //charge les données sur les hauteurs si besoin
        if (predMethod == PredictionMethod.Text && ((BuildingLoader)osmObj.Loader).Heights.Count == 0)
        {
            ((BuildingLoader)osmObj.Loader).LoadHeights();
        }

        if (predMethod == PredictionMethod.Picture)
        {
            if (useControllerPos != prevUseControllerPos)
            {
                heightCache = null;
                prevUseControllerPos = useControllerPos;
            }
            if (picture != null && prevPicture != null && picture.name != prevPicture.name)
            {
                heightCache = null;
                prevPicture = picture;
            }
            if (isWidth != prevIsWidth || useMarker != prevUseMarker || tolCol != prevTolCol)
            {
                heightCache = null;
                prevIsWidth = isWidth; prevUseMarker = useMarker; prevTolCol = tolCol;
            }
            if (useMarker && markerCol != prevMarkerCol)
            {
                heightCache = null;
                prevMarkerCol = markerCol;
            }
            if (!useMarker && (edgeCol != prevEdgeCol || nPixelsEdge != prevNPixelsEdge))
            {
                heightCache = null;
                prevEdgeCol = edgeCol;
                prevNPixelsEdge = nPixelsEdge;
            }
        }

        //met à jour la texture et la couleur si besoin
        if (material != null && prevMat != null && material.name != prevMat.name)
        {
            UpdateMaterial();
            prevMat = material;
            surface = material.name;
        }
        if (surface != prevSurface)
        {
            Material mat = Resources.Load<Material>("Materials/" + surface);
            if (mat != null)
            {
                material = mat;
                UpdateMaterial();
            }
            prevSurface = surface;
        }
        if (color != prevColor)
        {
            UpdateColor(color);
            prevColor = color;
        }

        prev_height = Height;
        if (osmObj.Element.Type == OsmGeoType.Node)
        {
            //met à jour la géométrie pour les noeuds si besoin
            if (geometry != PrimitiveType.Plane && geometry != PrimitiveType.Quad && geometry != prevGeometry)
            {
                Destroy(cityObj);
                AddPrimitive((Node)osmObj.Element);
                prevGeometry = geometry;
            }
            if (width != prevWidth || length != prevLength || prev_height != prevHeight)
            {
                UpdatePrimitive((Node)osmObj.Element);
                prevWidth = width;
                prevLength = length;
                prevHeight = prev_height;
            }
        }
        else
        {
            if (prev_height != prevHeight)
            {
                UpdateMesh(prev_height - prevHeight);
                prevHeight = prev_height;
            }
        }
        prev_height = height;
    }

    private void CalculateMergedSides()
    {
        mergedSideLengths = new List<float>();
        mergedSideVectors = new List<Vector3>();
        if (positions != null && positions.Length > 2)
        {
            float currentSideLength = 0f;
            Vector3 currentSideDirection = Vector3.zero;
            Vector3 currentSideVector = Vector3.zero;

            // Loop through all edges including the closing edge (wrapping around)
            for (int i = 0; i < positions.Length; i++)
            {
                int nextIdx = (i + 1) % positions.Length; // Wraps to 0 at the end
                Vector3 edgeVec = positions[nextIdx] - positions[i];
                float edgeDist = edgeVec.magnitude;

                if (i == 0)
                {
                    // First edge - initialize
                    currentSideLength = edgeDist;
                    currentSideDirection = edgeVec.normalized;
                    currentSideVector = edgeVec;
                }
                else
                {
                    // Check if parallel to current side (same direction)
                    float dotProduct = Vector3.Dot(edgeVec.normalized, currentSideDirection);

                    if (dotProduct > 0.99f) // Higher tolerance for parallel edges
                    {
                        currentSideLength += edgeDist;
                        currentSideVector += edgeVec;
                    }
                    else
                    {
                        // Direction changed, save current merged side and start new one
                        mergedSideLengths.Add(currentSideLength);
                        mergedSideVectors.Add(currentSideVector);

                        currentSideLength = edgeDist;
                        currentSideDirection = edgeVec.normalized;
                        currentSideVector = edgeVec;
                    }
                }
            }

            // After loop: check if last accumulated side merges with the first side
            if (mergedSideLengths.Count > 0 && mergedSideVectors.Count > 0)
            {
                Vector3 firstEdgeDirection = (positions[1] - positions[0]).normalized;
                float dotProductLastToFirst = Vector3.Dot(currentSideDirection, firstEdgeDirection);

                if (dotProductLastToFirst > 0.99f)
                {
                    // Last side and first side are parallel - merge them
                    mergedSideLengths[0] += currentSideLength;
                    mergedSideVectors[0] += currentSideVector;
                }
                else
                {
                    // Last side is separate
                    mergedSideLengths.Add(currentSideLength);
                    mergedSideVectors.Add(currentSideVector);
                }
            }
        }
    }

    private float GetHeight(PredictionMethod method)
    {
        if (method == PredictionMethod.Text && osmObj.Element.Id != null)
        {
            if (((BuildingLoader)osmObj.Loader).Heights.TryGetValue((long)osmObj.Element.Id, out float h))
            {
                if (h > 0)
                {
                    height = h * osmObj.Loader.Main.yMeterScale;
                    return height;
                }
                else
                    return height * osmObj.Loader.Main.yMeterScale * NFloors;
            }
            else
                return height * osmObj.Loader.Main.yMeterScale * NFloors;
        }
        else
        {
            if (method == PredictionMethod.Picture)
            {
                if (!useControllerPos && heightCache.HasValue)
                {
                    height = heightCache.Value;
                    return height;
                }
                float h;
                if (useControllerPos && heightCache.HasValue)
                {
                    h = GetHeightFromControllerView(pictureWidth, pictureHeight);
                }
                else
                {
                    if (useMarker)
                        h = GetHeightFromPicture(markerCol, tolCol);
                    else
                        h = GetHeightFromPicture(edgeCol, tolCol, nPixelsEdge);
                }
                if (h > 0)
                {
                    height = h * osmObj.Loader.Main.yMeterScale;
                    heightCache = height;
                    return height;
                }
                else
                    return height * osmObj.Loader.Main.yMeterScale * NFloors;
            }
            else
            {
                return height * osmObj.Loader.Main.yMeterScale * NFloors;
            }
        }
    }

    private float GetHeightFromPicture(Color edgeCol, double tol, int nPixels)      //prédit la hauteur réelle à partir d'une image de contours du bâtiment, en identifiant toutes les lignes verticales de couleur 'edgeCol' et de 'nPixels' pixels au minimum
                                                                                    //si 'isWidth=true(resp. false)', la largeur du bâtiment mesurée en pixels sur l'image correspond à la largeur (resp. longueur) réelle du bâtiment: la façade du bâtiment
                                                                                    //qui a été photographiée est donc exposé face Est ou Ouest (resp. Nord ou Sud)
    {
        if (picture == null || !picture.isReadable)
            return 0;
        int edgeXMin = picture.width-1, edgeXMax = 0, edgeYMin = picture.height-1, edgeYMax = 0;
        for (int i = 0; i < picture.width; i++)
        {
            int countPixels = 0;
            for (int j = 0; j < picture.height; j++)
            {
                Color c = picture.GetPixel(i, j);
                // teste si la couleur du pixel est approximativement égale à 'edgeCol', suivant la valeur du paramètre de tolérance
                if (c.r >= edgeCol.r-tol && c.r <= edgeCol.r+tol && c.g >= edgeCol.g - tol && c.g <= edgeCol.g + tol && c.b >= edgeCol.b - tol && c.b <= edgeCol.b + tol)
                {
                    countPixels++;
                    if (countPixels >= nPixels)
                    {
                        if (i < edgeXMin)
                            edgeXMin = i;
                        edgeXMax = i;
                        if (j - countPixels + 1 < edgeYMin)
                            edgeYMin = j - countPixels + 1;
                        if (j > edgeYMax)
                            edgeYMax = j;
                    }
                }
                else
                {
                    countPixels = 0;
                }
            }
        }
        int w = edgeXMax - edgeXMin + 1, h = edgeYMax - edgeYMin + 1;
        if (w > 0 && h > 0)
        {
            pictureWidth = w;
            pictureHeight = h;
            if (useControllerPos)
            {
                return GetHeightFromControllerView(w, h);
            }
            else
            {
                if (isWidth)
                    return h * Width / w;
                else
                    return h * Length / w;
            }
        }
        else
            return 0;
    }

    private float GetHeightFromPicture(Color markerCol, double tol)     //prédit la hauteur réelle à partir d'une image de contours du bâtiment, en mesurant les dimensions de la zone délimitée par un marqueur de couleur 'markerCol'
                                                                        //si 'isWidth=true(resp. false)', la largeur du bâtiment mesurée en pixels sur l'image correspond à la largeur (resp. longueur) réelle du bâtiment: la façade du bâtiment
                                                                        //qui a été photographiée est donc exposé face Est ou Ouest (resp. Nord ou Sud)
    {
        if (picture == null || !picture.isReadable)
            return 0;
        int xMax = 0, yMax = 0, xMin = picture.width - 1, yMin = picture.height - 1;
        for (int i = 0; i < picture.width; i++)
        {
            for (int j = 0; j < picture.height; j++)
            {
                Color c = picture.GetPixel(i, j);
                // teste si la couleur du pixel est approximativement égale à celle du marqueur, suivant la valeur du paramètre de tolérance
                if (c.r >= markerCol.r - tol && c.r <= markerCol.r + tol && c.g >= markerCol.g - tol && c.g <= markerCol.g + tol && c.b >= markerCol.b - tol && c.b <= markerCol.b + tol)
                {
                    xMax = i;
                    if (i < xMin)
                        xMin = i;
                    if (j > yMax)
                        yMax = j;
                    if (j < yMin)
                        yMin = j;
                }
            }
        }
        int w = xMax - xMin + 1, h = yMax - yMin + 1;
        if (w > 0 && h > 0)
        {
            pictureWidth = w;
            pictureHeight = h;
            if (useControllerPos)
            {
                return GetHeightFromControllerView(w, h);
            }
            else
            {
                if (isWidth)
                    return h * Width / w;
                else
                    return h * Length / w;
            }
        }
        else
            return 0;
    }

    private float GetHeightFromControllerView(int w, int h)
    {
        if (w <= 0 || h <= 0)
            return 0;
        if (isWidth)
            return (float)(w * GetDimFromControllerView(Length, Width).width / h);
        else
            return (float)(w * GetDimFromControllerView(Length, Width).length / h);
    }

    private (float length, float width) GetDimFromControllerView(float l, float w)
    {
        GameObject go = osmObj.Loader.Main.Controller;
        if (!Barycenter.HasValue || go.transform.position.z == Barycenter.Value.z)
        {
            return (l, w);
        }
        float xMeter = go.transform.position.x / osmObj.Loader.Main.xMeterScale;
        float zMeter = go.transform.position.z / osmObj.Loader.Main.zMeterScale;
        float min = Mathf.Min(Mathf.Abs(xMeter - Barycenter.Value.x), Mathf.Abs(zMeter - Barycenter.Value.z));
        float max = Mathf.Max(Mathf.Abs(xMeter - Barycenter.Value.x), Mathf.Abs(zMeter - Barycenter.Value.z));
        float angle = Mathf.Atan2(max, min);
        float a = l * Mathf.Cos(angle) + w * Mathf.Sin(angle);
        float b = w * Mathf.Cos(angle) + l * Mathf.Sin(angle);
        return (a, b);      //a = new Length, b = new Width
    }

    private float FeetsInchesToMeters(string measure)
    {
        
        float feets = 0, inches = 0;
        try
        {
            if (measure.Contains('\''))
            {
                int ind = measure.IndexOf('\'');
                feets = float.Parse(measure.Substring(0, ind), CultureInfo.InvariantCulture);
                if (measure.Contains('\"'))
                    inches = float.Parse(measure.Substring(ind + 1, measure.IndexOf('\"') - ind - 1), CultureInfo.InvariantCulture);
            }
            else
            {
                if (measure.Contains('\"'))
                    inches = float.Parse(measure.Substring(0, measure.IndexOf('\"')), CultureInfo.InvariantCulture);
            }
        }
        catch (Exception)
        {
            return 0;
        }
        return .3048f * feets + .0254f * inches;
    }

    private void AddPrimitive(Node node)
    {
        Vector3? pos = GetNodePosition(node);
        if (pos.HasValue)
            AddPrimitive(pos.Value);
    }

    private void AddPrimitive(Vector3 pos)
    {
        cityObj = GameObject.CreatePrimitive(geometry);
        cityObj.name = "ID = " + osmObj.Element.Id;
        cityObj.hideFlags = HideFlags.NotEditable;
        cityObj.transform.position = new Vector3(pos.x, pos.y + prev_height / 2f, pos.z);
        cityObj.transform.localScale = new Vector3(Length, prev_height, Width);
        IsVisible = true;
        // Add infos
        AddObjInfos();
    }

    private void UpdatePrimitive(Node node)
    {
        Vector3? pos = GetNodePosition(node);
        if (pos.HasValue)
        {
            cityObj.transform.position = new Vector3(pos.Value.x, pos.Value.y + prev_height / 2f, pos.Value.z);
            cityObj.transform.localScale = new Vector3(Length, prev_height, Width);
        }
    }

    public void UpdatePrimitiveHeight(float heightOffset)
    {
        Vector3? pos = GetNodePosition((Node)osmObj.Element);
        if (pos.HasValue)
        {
            cityObj.transform.position = new Vector3(pos.Value.x, pos.Value.y + (height + heightOffset) / 2f, pos.Value.z);
            cityObj.transform.localScale = new Vector3(Length, height + heightOffset, Width);
        }
    }

    private void CreateRoof()
    {
        Roof roof = gameObject.AddComponent<Roof>();
        roof.SetBuilding(this);
    }

    private void CreatePolygon(Vector3[] pos)
    {
        positions = pos;
        cityObj = new GameObject
        {
            name = "ID = " + osmObj.Element.Id,
            hideFlags = HideFlags.NotEditable
        };
        // Add a ProBuilderMesh component (ProBuilder mesh data is stored here)
        mesh = cityObj.AddComponent<ProBuilderMesh>();
        // Create a mesh from the polygon shape with the real or predicted height
        ActionResult act = mesh.CreateShapeFromPolygon(pos.ToList(), prev_height, false);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        IsVisible = act.ToBool();
        // Add a mesh collider
        AddMeshCollider();
        // Add infos
        AddObjInfos();
    }

    private void AddObjInfos()
    {
        // Update the texture and the color
        UpdateMaterial();
        if (color != Color.clear)
            UpdateColor(color);
        // Add a canvas and text fields for node positions and miscellanous infos
        Text t = cityObj.AddComponent<Text>();
        t.text = GetNodePositionsText();
        if (osmObj.Loader.Main.hideMeshInHierarchy)
            cityObj.hideFlags = HideFlags.HideInHierarchy;
        else
            cityObj.transform.SetParent(((BuildingLoader)osmObj.Loader).BuildingMeshes.transform);
    }
    
    private void CreateMultiPolygon(Relation relation)
    {
        //for each outer or inner way, we associate an index
        Dictionary<Way, int> outerWays = new Dictionary<Way, int>();
        Dictionary<Way, int> innerWays = new Dictionary<Way, int>();
        int indWay = 0;
        bool isLastWayInner = false;
        foreach (var member in relation.Members)
        {
            if (member.Type == OsmGeoType.Node)
            {
                var nodes = from node in osmObj.DirectSubNodes
                            where node.Id == member.Id
                            select node;
                if (nodes.Any())
                {
                    AddPrimitive(nodes.First());
                }
            }
            else
            {
                if (member.Type == OsmGeoType.Way)
                {
                    var ways = from way in osmObj.SubWays
                               where way.Id == member.Id
                               select way;
                    if (ways.Any())
                    {
                        if (member.Role == "outer")
                        {
                            if (isLastWayInner)
                                indWay++;
                            outerWays.Add(ways.First(), indWay);
                            isLastWayInner = false;
                        }
                        else
                        {
                            if (member.Role == "inner")
                            {
                                innerWays.Add(ways.First(), indWay);
                                isLastWayInner = true;
                            }
                        }
                    }
                }
                else
                {
                    var subRelations = from subRelation in osmObj.SubRelations
                                       where subRelation.Id == member.Id
                                       select subRelation;
                    if (subRelations.Any())
                    {
                        CreateMultiPolygon(subRelations.First());
                    }
                }
            }
        }

        //find the outer and inner paths of the multi-polygon
        List<Vector3[]> outerPaths = new List<Vector3[]>();
        List<Vector3[]> innerPaths = new List<Vector3[]>();
        foreach (var way in outerWays.Keys)
        {
            var nodes = from node in osmObj.SubNodes
                        where node.Id != null && way.Nodes.ToList().Contains((long)node.Id)
                        select node;
            if (nodes.Any()) {
                Vector3[] pos = GetNodePositions(nodes.ToArray());
                outerPaths.Add(pos);
            }
        }
        foreach (var way in innerWays.Keys)
        {
            var nodes = from node in osmObj.SubNodes
                        where node.Id != null && way.Nodes.ToList().Contains((long)node.Id)
                        select node;
            if (nodes.Any())
            {
                Vector3[] pos = GetNodePositions(nodes.ToArray());
                innerPaths.Add(pos);
            }
        }

        //join multiple adjacent outer paths in a ring if necessary
        List<Vector3> completeOuterPath = new List<Vector3>();
        for (int i = 0; i < outerPaths.Count(); i++)
        {

        }

    }

    public void UpdateRoofMaterial(Material mat)
    {
        Face ceilingFace = GetCeilingFace();
        if (material == null || mat == null || ceilingFace == null)
            return;
        MeshRenderer renderer = cityObj.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new Material[2]
        {
            material, mat
        };
        ceilingFace.submeshIndex = 1;
        mesh.ToMesh();
        mesh.Refresh();
    }

}
