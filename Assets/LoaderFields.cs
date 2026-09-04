using UnityEngine;

public class LoaderFields: MonoBehaviour
{

    //noms de fichier entrés par l'utilisateur, pour l'importation des données OSM
    [Header("OSM file names")]
    [Tooltip("File name of the OSM elements")]
    public string elementsFile = "liechtenstein-buildings.xml";
    [Tooltip("File name of the OSM tag elements")]
    public string tagsFile = "liechtenstein-buildings-tags.xml";
    [Tooltip("File name of the subnodes of the OSM elements and tag elements")]
    public string nodesFile = "liechtenstein-buildings-nodes.xml";
    [Tooltip("File name of the subways of the OSM elements and tag elements")]
    public string waysFile = "liechtenstein-buildings-ways.xml";
    [Tooltip("File name of the subrelations of the OSM elements and tag elements")]
    public string relationsFile = "liechtenstein-buildings-relations.xml";

    public void SetDefaultValues(string cityName, string cityObject, bool extract = false)
    {
        string extractStr = extract ? "-extract" : "";
        elementsFile = cityName + '-' + cityObject + extractStr + ".xml";
        tagsFile = cityName + '-' + cityObject + "-tags" + extractStr + ".xml";
        nodesFile = cityName + '-' + cityObject + "-nodes" + extractStr + ".xml";
        waysFile = cityName + '-' + cityObject + "-ways" + extractStr + ".xml";
        relationsFile = cityName + '-' + cityObject + "-relations" + extractStr + ".xml";
    }

}
