using System.Collections.Generic;

class MultiPolygon       //Currently not used
{

    //un multi-polygone est composé d'anneaux extérieurs (frontières) et intérieurs (trous); ces anneaux intérieurs doivent être contenus à l'intérieur d'un unique anneau extérieur: les autres anneaux extérieurs ne contiennent pas de trous
    //un anneau extérieur ou intérieur peut être soit un unique chemin, soit une suite de chemins adjacents (éléments OSM de type Way) dont les extrémités (noeuds) du premier et dernier chemin coincident
    private readonly List<Way> outerWays = new List<Way>();
    private readonly List<Way> innerWays = new List<Way>();

    public Way[] OuterWays => outerWays.ToArray();
    public Way[] InnerWays => innerWays.ToArray();
    public Way OuterWayWithHoles { get; }

    public void AddOuterWay(Way way)
    {
        outerWays.Add(way);
    }

    public void AddInnerWay(Way way)
    {
        innerWays.Add(way);
    }

    public void RemoveOuterWay(Way way)
    {
        outerWays.Remove(way);
    }

    public void RemoveInnerWay(Way way)
    {
        innerWays.Remove(way);
    }



}

class Ring
{

    private List<Way> ways = new List<Way>();
    private Node firstNode, lastNode;

    public Way[] Ways => ways.ToArray();
    public Way CompleteWay { get; }

    public void AddWay(Way way)
    {

    }

    public void RemoveWay(Way way)
    {

    }

}
