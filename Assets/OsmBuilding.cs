public class OsmBuilding : OsmObject {       //classe représentant un bâtiment

    public OsmBuilding(OsmElement elem, BuildingLoader loader)
    {
        Element = elem;
        Loader = loader;
        SetSubElements();
    }

}
