using OsmSharp;
using OsmSharp.Streams;
using OsmSharp.Geo;
using OsmSharp.Complete;
using OsmSharp.Tags;
using GeoAPI.Geometries;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using OsmSharp.Streams.Complete;

namespace OsmImport
{
    public class PBFOsmFile
    {
        private PBFOsmFile(string path)
        {
            Path = path;
        }

        private Boundaries? bounds;
        private string path;

        public string Path      //le chemin courant du fichier
        {
            get
            {
                return path;
            }
            set     //vérifie si le chemin spécifié est valide, puis modifie la valeur du chemin courant si c'est le cas
            {
                try
                {
                    using (var fileStream = File.OpenRead(value))
                    {
                        path = value;
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.Message);
                }
            }
        }

        public Boundaries Bounds    //structure contenant la latitude et longitude minimale/maximale de la région peuplée par les noeuds OSM du fichier
        {
            get
            {
                if (bounds != null)
                    return (Boundaries)bounds;
                Boundaries b;
                using (var fileStream = File.OpenRead(Path))
                {
                    var source = new PBFOsmStreamSource(fileStream);
                    var lats = from osmGeo in source
                               where osmGeo.Type == OsmGeoType.Node
                               select ((Node)osmGeo).Latitude;
                    var lons = from osmGeo in source
                                where osmGeo.Type == OsmGeoType.Node
                                select ((Node)osmGeo).Longitude;
                    b = new Boundaries(lats.Min() ?? -90, lats.Max() ?? 90, lons.Min() ?? -180, lons.Max() ?? 180);
                    source.Dispose();
                }
                bounds = b;
                return b;
            }
        }

        public enum GeoAttrs
        {
            ID, Timestamp, Type, UserID, Username, Version, Visible, ChangeSetID, Tag
        };

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

        public static PBFOsmFile CreateInstance(string path)
        {
            PBFOsmFile osm = new PBFOsmFile(path);
            if (osm.path != null)
                return osm;
            else
                return null;
        }

        public void PrintElems(long nElems, bool verbose = false, bool subVerbose = false)     //affiche (avec détails ou non) chaque élément OSM du fichier, avec 'nElems' éléments à afficher avant chaque pause (négatif ou nul si pas de pause)
        {
            using (var fileStream = File.OpenRead(Path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                var completeSource = source.ToComplete();
                long inc = 0;
                foreach (var element in completeSource)
                {
                    inc++;
                    if (nElems > 0 && inc % nElems == 0)
                        Console.ReadLine();
                    Console.WriteLine(element.ToString());
                    if (verbose)
                    {
                        if (element.Type == OsmGeoType.Node)
                        {
                            PrintNodeInfos((Node)element);
                        }
                        else
                        {
                            if (element.Type == OsmGeoType.Way)
                            {
                                PrintWayInfos((CompleteWay)element, subVerbose);
                            }
                            else
                            {
                                PrintRelationInfos((CompleteRelation)element, subVerbose);
                            }
                        }
                    }
                }
                source.Dispose();
                completeSource.Dispose();
            }
        }

        public void PrintNodes(long nElems, bool verbose = false)     //affiche (avec détails ou non) chaque noeud OSM du fichier, avec 'nElems' éléments à afficher avant chaque pause (négatif ou nul si pas de pause)
        {
            using (var fileStream = File.OpenRead(Path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                long inc = 0;
                while (source.MoveNextNode())
                {
                    inc++;
                    if (nElems > 0 && inc % nElems == 0)
                        Console.ReadLine();
                    var element = source.Current();
                    Console.WriteLine(element.ToString());
                    if (verbose)
                    {
                        PrintNodeInfos((Node)element);
                    }
                }
                source.Dispose();
            }
        }

        public void PrintWays(long nElems, bool verbose = false, bool subVerbose = false)     //affiche (avec détails ou non) chaque chemin OSM du fichier, avec 'nElems' éléments à afficher avant chaque pause (négatif ou nul si pas de pause)
        {
            using (var fileStream = File.OpenRead(Path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                var completeSource = source.ToComplete();
                var filtered = from osmGeo in completeSource 
                               where osmGeo.Type == OsmGeoType.Way 
                               select osmGeo;
                long inc = 0;
                foreach (var element in filtered)
                {
                    inc++;
                    if (nElems > 0 && inc % nElems == 0)
                        Console.ReadLine();
                    Console.WriteLine($"{element.Type}[{element.Id}]");
                    if (verbose)
                    {
                        PrintWayInfos((CompleteWay)element, subVerbose);
                    }
                }
                source.Dispose();
                completeSource.Dispose();
            }
        }

        public void PrintRelations(long nElems, bool verbose = false, bool subVerbose = false)     //affiche (avec détails ou non) chaque relation OSM du fichier, avec 'nElems' éléments à afficher avant chaque pause (négatif ou nul si pas de pause)
        {
            using (var fileStream = File.OpenRead(Path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                var completeSource = source.ToComplete();
                var filtered = from osmGeo in completeSource
                               where osmGeo.Type == OsmGeoType.Relation
                               select osmGeo;
                long inc = 0;
                foreach (var element in filtered)
                {
                    inc++;
                    if (nElems > 0 && inc % nElems == 0)
                        Console.ReadLine();
                    Console.WriteLine($"{element.Type}[{element.Id}]");
                    if (verbose)
                    {
                        PrintRelationInfos((CompleteRelation)element, subVerbose);
                    }
                }
                source.Dispose();
                completeSource.Dispose();
            }
        }

        private void PrintNodeInfos(Node node)       //affiche les détails d'un noeud OSM
        {
            Console.WriteLine($"ID = {node.Id}, Timestamp = {node.TimeStamp}, Type = {node.Type}, User ID = {node.UserId}, Username = {node.UserName}, Version = {node.Version}, Visible = {node.Visible}, Changeset ID = {node.ChangeSetId}");
            Console.WriteLine($"Latitude = {node.Latitude}, Longitude = {node.Longitude}");

            //print node tags
            Console.Write("Node tags : ");
            foreach (var tag in node.Tags)
            {
                Console.Write($"({tag.Key}, {tag.Value}) - ");
            }
            Console.WriteLine();
        }

        private void PrintWayInfos(CompleteWay way, bool verbose)     //affiche les détails d'un chemin OSM, avec détails de chaque noeud contenu ou non
        {
            Console.WriteLine($"ID = {way.Id}, Timestamp = {way.TimeStamp}, Type = {way.Type}, User ID = {way.UserId}, Username = {way.UserName}, Version = {way.Version}, Visible = {way.Visible}, Changeset ID = {way.ChangeSetId}");
            if (verbose)
            {
                foreach (var node in way.Nodes)
                {
                    Console.WriteLine(node.ToString());
                    PrintNodeInfos(node);
                }
            }
            else
            {
                Console.Write("Node IDs : ");
                foreach (var node in way.Nodes)
                {
                    Console.Write(node.Id + " - ");
                }
                Console.WriteLine();
            }

            //print way tags
            Console.Write("Way tags : ");
            foreach (var tag in way.Tags)
            {
                Console.Write($"({tag.Key}, {tag.Value}) - ");
            }
            Console.WriteLine();
        }

        private void PrintRelationInfos(CompleteRelation relation, bool verbose)     //affiche les détails d'une relation OSM, avec détails de chaque membre (relation, chemin ou noeud) contenu ou non
        {
            Console.WriteLine($"ID = {relation.Id}, Timestamp = {relation.TimeStamp}, Type = {relation.Type}, User ID = {relation.UserId}, Username = {relation.UserName}, Version = {relation.Version}, Visible = {relation.Visible}, Changeset ID = {relation.ChangeSetId}");
            foreach (var member in relation.Members)
            {
                ICompleteOsmGeo element = member.Member;
                Console.WriteLine($"Member ID = {element.Id}, Member Role = {member.Role}, Member Type = {element.Type}");
                if (verbose)
                {
                    Console.WriteLine($"{element.Type}[{element.Id}]");
                    if (element.Type == OsmGeoType.Node)
                    {
                        PrintNodeInfos((Node)element);
                    }
                    else
                    {
                        if (element.Type == OsmGeoType.Way)
                        {
                            PrintWayInfos((CompleteWay)element, verbose);
                        }
                        else
                        {
                            PrintRelationInfos((CompleteRelation)element, verbose);
                        }
                    }
            }
            }

            //print relation tags
            Console.Write("Relation tags : ");
            foreach (var tag in relation.Tags)
            {
                Console.Write($"({tag.Key}, {tag.Value}) - ");
            }
            Console.WriteLine();
        }

        public static void WriteElemsTo(string path, OsmGeo[] elements)
        {
            using (var fileStream = File.OpenWrite(path))
            {
                var target = new PBFOsmStreamTarget(fileStream);
                target.Initialize();
                foreach (var elem in elements)
                {
                    if (elem.Type == OsmGeoType.Node)
                    {
                        target.AddNode((Node)elem);
                    }
                    else
                    {
                        if (elem.Type == OsmGeoType.Way)
                        {
                            target.AddWay((Way)elem);
                        }
                        else
                        {
                            target.AddRelation((Relation)elem);
                        }
                    }
                }
                target.Flush();
                target.Close();
            }
        }

        public ICompleteOsmGeo[] ExtractRegion(string path, float left, float top, float right, float bottom, bool completeWays = false, bool isXml = true)
        {
            OsmStreamSource source = null, filtered = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));
                filtered = source.FilterBox(left, top, right, bottom, completeWays);
                if (filtered != null)
                {
                    if (path != null)
                        WriteSourceTo(path, filtered, isXml);
                    return filtered.ToComplete().ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return null;
            }
            finally
            {
                filtered?.Dispose();
                source?.Dispose();
            }
            
        }

        public ICompleteOsmGeo[] ExtractRegion(string path, IPolygon polygon, bool completeWays = false, bool isXml = true)    // filter by keeping everything inside the given polygon
        {
            OsmStreamSource source = null, filtered = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));
                filtered = source.FilterSpatial(polygon, completeWays);
                if (filtered != null)
                {
                    if (path != null)
                        WriteSourceTo(path, filtered, isXml);
                    return filtered.ToComplete().ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return null;
            }
            finally
            {
                filtered?.Dispose();
                source?.Dispose();
            }
        }

        public OsmGeo[] FilterByAttr(string path, GeoAttrs attr, object value, bool isXml = true)
        {
            OsmStreamSource source = null;
            IEnumerable<OsmGeo> filtered = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));
                switch (attr)
                {
                    case GeoAttrs.ID:
                        filtered = from osmGeo in source
                                    where osmGeo.Id == (long)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.Timestamp:
                        filtered = from osmGeo in source
                                    where osmGeo.TimeStamp == (DateTime)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.Type:
                        filtered = from osmGeo in source
                                    where osmGeo.Type == (OsmGeoType)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.ChangeSetID:
                        filtered = from osmGeo in source
                                    where osmGeo.ChangeSetId == (long)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.UserID:
                        filtered = from osmGeo in source
                                    where osmGeo.UserId == (long)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.Username:
                        filtered = from osmGeo in source
                                    where osmGeo.UserName == (string)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.Version:
                        filtered = from osmGeo in source
                                    where osmGeo.Version == (int)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.Visible:
                        filtered = from osmGeo in source
                                    where osmGeo.Visible == (bool)value
                                    select osmGeo;
                        break;
                    case GeoAttrs.Tag:
                        filtered = from osmGeo in source
                                    where osmGeo.Tags != null && osmGeo.Tags.Contains((Tag)value)
                                    select osmGeo;
                        break;
                }
                if (filtered.Any())
                {
                    if (path != null)
                        WriteSourceTo(path, filtered, isXml);
                    return filtered.ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return null;
            }
            finally
            {
                source?.Dispose();
            }
        }

        public OsmGeo[] FilterByTags(string path, Tag[] tags, bool anyTag = false, bool isXml = true)
        {
            OsmStreamSource source = null;
            IEnumerable<OsmGeo> filtered = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));
                if (anyTag)
                {
                    filtered = from osmGeo in source
                               where osmGeo.Tags != null
                               select osmGeo;
                }
                else
                {
                    filtered = from osmGeo in source
                               where osmGeo.Tags != null && ContainsTag(osmGeo.Tags, tags)
                               select osmGeo;
                }
                if (filtered.Any())
                {
                    if (path != null)
                        WriteSourceTo(path, filtered, isXml);
                    return filtered.ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return null;
            }
            finally
            {
                source?.Dispose();
            }
        }

        public OsmGeo[] FilterByKeys(string path, string[] keys, bool containsKey = false, bool isXml = true)
        {
            OsmStreamSource source = null;
            IEnumerable<OsmGeo> filtered = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));
                if (containsKey)
                {
                    filtered = from osmGeo in source
                               where osmGeo.Tags != null && ContainsKey(osmGeo.Tags, keys)
                               select osmGeo;
                }
                else
                {
                    filtered = from osmGeo in source
                               where osmGeo.Tags != null && osmGeo.Tags.ContainsAnyKey(keys)
                               select osmGeo;
                }
                if (filtered.Any())
                {
                    if (path != null)
                        WriteSourceTo(path, filtered, isXml);
                    return filtered.ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return null;
            }
            finally
            {
                source?.Dispose();
            }
        }

        public void ToXmlFile(string path)
        {
            OsmStreamSource source = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));

                // convert to XML
                if (path != null)
                {
                    WriteSourceTo(path, source, true);
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
            finally
            {
                source?.Dispose();
            }
        }

        public static void Concatenate(string pathSource, string pathOtherSource, bool isXml = true)
        {
            FileStream stream = null;
            OsmStreamSource source = null;
            try
            {
                stream = new FileInfo(pathSource).Open(FileMode.Append, FileAccess.Write);
                if (isXml)
                    source = new XmlOsmStreamSource(File.OpenRead(pathOtherSource));
                else
                    source = new PBFOsmStreamSource(File.OpenRead(pathOtherSource));

                OsmStreamTarget target;
                if (isXml)
                    target = new XmlOsmStreamTarget(stream);
                else
                    target = new PBFOsmStreamTarget(stream);
                target.RegisterSource(source);
                target.Pull();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
            finally
            {
                stream?.Dispose();
                source?.Dispose();
            }
        }

        public OsmGeo[] GetAllElems()
        {
            using (var fileStream = File.OpenRead(path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                return source.ToArray();
            }
        }

        public ICompleteOsmGeo[] GetCompleteElems()
        {
            using (var fileStream = File.OpenRead(path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                return source.ToComplete().ToArray();
            }
        }

        public Node[] GetNodes()
        {
            OsmGeo[] elems = FilterByAttr(null, GeoAttrs.Type, OsmGeoType.Node, false);
            Node[] nodes = new Node[elems.Length];
            for (int i = 0; i < elems.Length; i++)
            {
                nodes[i] = (Node)elems[i];
            }
            return nodes;
        }

        public Way[] GetWays()
        {
            OsmGeo[] elems = FilterByAttr(null, GeoAttrs.Type, OsmGeoType.Way, false);
            Way[] ways = new Way[elems.Length];
            for (int i = 0; i < elems.Length; i++)
            {
                ways[i] = (Way)elems[i];
            }
            return ways;
        }

        public Relation[] GetRelations()
        {
            OsmGeo[] elems = FilterByAttr(null, GeoAttrs.Type, OsmGeoType.Relation, false);
            Relation[] relations = new Relation[elems.Length];
            for (int i = 0; i < elems.Length; i++)
            {
                relations[i] = (Relation)elems[i];
            }
            return relations;
        }

        public CompleteWay[] GetCompleteWays()
        {
            using (var fileStream = File.OpenRead(Path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                var completeSource = source.ToComplete();
                var filtered = from osmGeo in completeSource
                               where osmGeo.Type == OsmGeoType.Way
                               select osmGeo;
                ICompleteOsmGeo[] completeElems = filtered.ToArray();
                CompleteWay[] completeWays = new CompleteWay[completeElems.Length];
                for (int i = 0; i < completeElems.Length; i++)
                {
                    completeWays[i] = (CompleteWay)completeElems[i];
                }
                return completeWays;
            }
        }

        public CompleteRelation[] GetCompleteRelations()
        {
            using (var fileStream = File.OpenRead(Path))
            {
                var source = new PBFOsmStreamSource(fileStream);
                var completeSource = source.ToComplete();
                var filtered = from osmGeo in completeSource
                               where osmGeo.Type == OsmGeoType.Relation
                               select osmGeo;
                ICompleteOsmGeo[] completeElems = filtered.ToArray();
                CompleteRelation[] completeRelations = new CompleteRelation[completeElems.Length];
                for (int i = 0; i < completeElems.Length; i++)
                {
                    completeRelations[i] = (CompleteRelation)completeElems[i];
                }
                return completeRelations;
            }
        }

        public static List<Node> GetSubNodes(CompleteRelation relation)
        {
            List<Node> nodes = new List<Node>();
            foreach (var member in relation.Members)
            {
                ICompleteOsmGeo element = member.Member;
                if (element.Type == OsmGeoType.Node)
                {
                    nodes.Add((Node)element);
                }
                else
                {
                    if (element.Type == OsmGeoType.Way)
                    {
                        List<Node> subNodes = GetSubNodes((CompleteWay)element);
                        nodes.AddRange(subNodes);
                    }
                    else
                    {
                        List<Node> subNodes = GetSubNodes((CompleteRelation)element);
                        nodes.AddRange(subNodes);
                    }
                }
            }
            return nodes;
        }

        public static List<Node> GetSubNodes(CompleteWay way)
        {
            List<Node> nodes = new List<Node>();
            foreach (var node in way.Nodes)
            {
                nodes.Add(node);
            }
            return nodes;
        }

        public static List<Way> GetSubWays(CompleteRelation relation)
        {
            List<Way> ways = new List<Way>();
            foreach (var member in relation.Members)
            {
                ICompleteOsmGeo element = member.Member;
                if (element.Type == OsmGeoType.Way)
                {
                    ways.Add((Way)((CompleteWay)element).ToSimple());
                }
                else
                {
                    if (element.Type == OsmGeoType.Relation)
                    {
                        List<Way> subWays = GetSubWays((CompleteRelation)element);
                        ways.AddRange(subWays);
                    }
                }
            }
            return ways;
        }

        public static List<Relation> GetSubRelations(CompleteRelation relation)
        {
            List<Relation> relations = new List<Relation>();
            foreach (var member in relation.Members)
            {
                ICompleteOsmGeo element = member.Member;
                if (element.Type == OsmGeoType.Relation)
                {
                    relations.Add((Relation)((CompleteRelation)element).ToSimple());
                    List<Relation> subRelations = GetSubRelations((CompleteRelation)element);
                    relations.AddRange(subRelations);
                }
            }
            return relations;
        }

        private bool ContainsTag(TagsCollectionBase sourceTags, Tag[] tags)
        {
            foreach (var tag in tags)
            {
                if (sourceTags.Contains(tag))
                    return true;
            }
            return false;
        }

        private bool ContainsKey(TagsCollectionBase tags, string[] keys)
        {
            foreach (var tag in tags)
            {
                foreach (var key in keys)
                {
                    if (tag.Key.IndexOf(key) != -1)
                        return true;
                }
            }
            return false;
        }

        private void WriteSourceTo(string path, IEnumerable<OsmGeo> filtered, bool isXml = true)
        {
            using (var stream = new FileInfo(path).Open(FileMode.Create, FileAccess.ReadWrite))
            {
                OsmStreamTarget target;
                if (isXml)
                    target = new XmlOsmStreamTarget(stream);
                else
                    target = new PBFOsmStreamTarget(stream);
                target.RegisterSource(filtered);
                target.Pull();
            }
        }

        public static OsmGeo[] DropDuplicates(OsmGeo[] elems)
        {
            List<OsmGeo> visitedElems = new List<OsmGeo>();
            foreach (var elem in elems)
            {
                var found = from osmGeo in visitedElems
                            where osmGeo.Id == elem.Id && osmGeo.Type == elem.Type
                            select osmGeo;
                if (!found.Any())
                {
                    visitedElems.Add(elem);
                }
            }
            return visitedElems.ToArray();
        }

        public OsmGeo[] RejectSubDuplicates(string path, OsmGeo[] filteredElems, bool isXml = true)
        {
            OsmStreamSource source = null;
            OsmCompleteStreamSource completeSource = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));
                completeSource = source.ToComplete();
                List<Node> visitedNodes = new List<Node>();
                List<Way> visitedWays = new List<Way>();
                List<Relation> visitedRelations = new List<Relation>();
                List<OsmGeo> filtered = new List<OsmGeo>();
                foreach (var completeElem in completeSource)
                {
                    if (completeElem.Type == OsmGeoType.Node)
                        continue;
                    var found = from osmGeo in filteredElems
                                where osmGeo.Id == completeElem.Id && osmGeo.Type == completeElem.Type
                                select osmGeo;
                    if (filteredElems == null || found.Any())
                    {
                        if (completeElem.Type == OsmGeoType.Relation)
                        {
                            var relation = (CompleteRelation)completeElem;
                            visitedNodes.AddRange(GetSubNodes(relation));
                            visitedWays.AddRange(GetSubWays(relation));
                            visitedRelations.AddRange(GetSubRelations(relation));
                        }
                        else
                        {
                            visitedNodes.AddRange(GetSubNodes((CompleteWay)completeElem));
                        }
                    }
                }
                //reject duplicates in source comparing to all sub-elements
                IEnumerable<OsmGeo> duplicates;
                foreach (var elem in source)
                {
                    if (elem.Type == OsmGeoType.Relation)
                    {
                        duplicates = from relation in visitedRelations
                                     where relation.Id == elem.Id
                                     select relation;
                    }
                    else
                    {
                        if (elem.Type == OsmGeoType.Way)
                        {
                            duplicates = from way in visitedWays
                                         where way.Id == elem.Id
                                         select way;
                        }
                        else
                        {
                            duplicates = from node in visitedNodes
                                         where node.Id == elem.Id
                                         select node;
                        }
                    }
                    if (!duplicates.Any())
                        filtered.Add(elem);
                }
                if (filtered.Any())
                {
                    if (path != null)
                        WriteSourceTo(path, filtered, isXml);
                    return filtered.ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return null;
            }
            finally
            {
                source?.Dispose();
                completeSource?.Dispose();
            }
        }

        public void WriteSubElemsTo(string nodesPath, string waysPath, string relationsPath, OsmGeo[] filtered, bool relationSubNodes = false, bool isXml = true)        //trouve les sous-éléments (noeuds, chemins et relations) de tous les chemins et toutes les relations contenu(e)s dans le tableau passé en 4ème paramètre, 
                                                                                                                                                                         //puis écrit les noeuds (resp. les chemins/relations) trouvé(e)s dans le fichier de destination dont le chemin correspond au 1er paramètre (resp. 2nd/3ème paramètre)
                                                                                                                                                                         //si le tableau est null, on écrit tous les sous-éléments des éléments du fichier
        {
            OsmStreamSource source = null;
            OsmCompleteStreamSource completeSource = null;
            try
            {
                source = new PBFOsmStreamSource(File.OpenRead(Path));
                completeSource = source.ToComplete();
                List<Node> nodes = new List<Node>();
                List<Way> ways = new List<Way>();
                List<Relation> relations = new List<Relation>();

                foreach (var completeElem in completeSource)
                {
                    if (completeElem.Type == OsmGeoType.Node)
                        continue;
                    var found = from osmGeo in filtered
                                where osmGeo.Id == completeElem.Id && osmGeo.Type == completeElem.Type
                                select osmGeo;
                    if (filtered == null || found.Any())
                    {
                        if (completeElem.Type == OsmGeoType.Way)
                        {
                            List<Node> subNodes = GetSubNodes((CompleteWay)completeElem);
                            nodes.AddRange(subNodes);
                        }
                        else
                        {
                            if (relationSubNodes)
                            {
                                List<Node> subNodes = GetSubNodes((CompleteRelation)completeElem);
                                nodes.AddRange(subNodes);
                            }
                            List<Way> subWays = GetSubWays((CompleteRelation)completeElem);
                            ways.AddRange(subWays);
                            List<Relation> subRelations = GetSubRelations((CompleteRelation)completeElem);
                            relations.AddRange(subRelations);
                        }
                    }
                }

                if (nodes.Any())
                {
                    WriteSourceTo(nodesPath, nodes, isXml);
                }
                if (ways.Any())
                {
                    WriteSourceTo(waysPath, ways, isXml);
                }
                if (relations.Any())
                {
                    WriteSourceTo(relationsPath, relations, isXml);
                }
                    
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return;
            }
            finally
            {
                source?.Dispose();
                completeSource?.Dispose();
            }
        }

    }
}
