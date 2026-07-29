public class OsmHighway : OsmObject       //classe représentant une route, un chemin ou une rue
{
    
    public OsmHighway(OsmElement elem, HighwayLoader loader)
    {
        Element = elem;
        Loader = loader;
        SetSubElements();
    }

}
