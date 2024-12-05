using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using CodeWalker.GameFiles;
using CodeWalker.World;

namespace CodeWalker.Project;

public class ProjectFile
{
    //fields stored in file
    public string Name { get; set; } //friendly name for this project
    public int Version { get; set; }
    public List<string> YmapFilenames { get; set; } = new();
    public List<string> YtypFilenames { get; set; } = new();
    public List<string> YbnFilenames { get; set; } = new();
    public List<string> YndFilenames { get; set; } = new();
    public List<string> YnvFilenames { get; set; } = new();
    public List<string> TrainsFilenames { get; set; } = new();
    public List<string> ScenarioFilenames { get; set; } = new();
    public List<string> AudioRelFilenames { get; set; } = new();
    public List<string> YdrFilenames { get; set; } = new();
    public List<string> YddFilenames { get; set; } = new();
    public List<string> YftFilenames { get; set; } = new();
    public List<string> YtdFilenames { get; set; } = new();

    //fields not stored
    public string Filename { get; set; } //filename without path
    public string Filepath { get; set; } //full path of the current file
    public bool HasChanged { get; set; } //flag for use by the UI

    public List<YmapFile> YmapFiles { get; set; } = new();
    public List<YtypFile> YtypFiles { get; set; } = new();
    public List<YbnFile> YbnFiles { get; set; } = new();
    public List<YndFile> YndFiles { get; set; } = new();
    public List<YnvFile> YnvFiles { get; set; } = new();
    public List<TrainTrack> TrainsFiles { get; set; } = new();
    public List<YmtFile> ScenarioFiles { get; set; } = new();
    public List<RelFile> AudioRelFiles { get; set; } = new();
    public List<YdrFile> YdrFiles { get; set; } = new();
    public List<YddFile> YddFiles { get; set; } = new();
    public List<YftFile> YftFiles { get; set; } = new();
    public List<YtdFile> YtdFiles { get; set; } = new();


    public void Save()
    {
        var doc = new XmlDocument();
        var projelem = doc.CreateElement("CodeWalkerProject");
        doc.AppendChild(projelem);

        Xml.AddChildWithInnerText(doc, projelem, "Name", Name);
        Xml.AddChildWithAttribute(doc, projelem, "Version", "value", Version.ToString());

        var ymapselem = Xml.AddChild(doc, projelem, "YmapFilenames");
        foreach (var ymapfilename in YmapFilenames) Xml.AddChildWithInnerText(doc, ymapselem, "Item", ymapfilename);

        var ytypselem = Xml.AddChild(doc, projelem, "YtypFilenames");
        foreach (var ytypfilename in YtypFilenames) Xml.AddChildWithInnerText(doc, ytypselem, "Item", ytypfilename);

        var ybnselem = Xml.AddChild(doc, projelem, "YbnFilenames");
        foreach (var ybnfilename in YbnFilenames) Xml.AddChildWithInnerText(doc, ybnselem, "Item", ybnfilename);

        var yndselem = Xml.AddChild(doc, projelem, "YndFilenames");
        foreach (var yndfilename in YndFilenames) Xml.AddChildWithInnerText(doc, yndselem, "Item", yndfilename);

        var ynvselem = Xml.AddChild(doc, projelem, "YnvFilenames");
        foreach (var ynvfilename in YnvFilenames) Xml.AddChildWithInnerText(doc, ynvselem, "Item", ynvfilename);

        var trainselem = Xml.AddChild(doc, projelem, "TrainsFilenames");
        foreach (var trainsfile in TrainsFilenames) Xml.AddChildWithInnerText(doc, trainselem, "Item", trainsfile);

        var scenarioselem = Xml.AddChild(doc, projelem, "ScenarioFilenames");
        foreach (var scenariofilename in ScenarioFilenames)
            Xml.AddChildWithInnerText(doc, scenarioselem, "Item", scenariofilename);

        var audiorelselem = Xml.AddChild(doc, projelem, "AudioRelFilenames");
        foreach (var audiorelfilename in AudioRelFilenames)
            Xml.AddChildWithInnerText(doc, audiorelselem, "Item", audiorelfilename);

        var ydrselem = Xml.AddChild(doc, projelem, "YdrFilenames");
        foreach (var ydrfilename in YdrFilenames) Xml.AddChildWithInnerText(doc, ydrselem, "Item", ydrfilename);

        var yddselem = Xml.AddChild(doc, projelem, "YddFilenames");
        foreach (var yddfilename in YddFilenames) Xml.AddChildWithInnerText(doc, yddselem, "Item", yddfilename);

        var yftselem = Xml.AddChild(doc, projelem, "YftFilenames");
        foreach (var yftfilename in YftFilenames) Xml.AddChildWithInnerText(doc, yftselem, "Item", yftfilename);

        var ytdselem = Xml.AddChild(doc, projelem, "YtdFilenames");
        foreach (var ytdfilename in YtdFilenames) Xml.AddChildWithInnerText(doc, ytdselem, "Item", ytdfilename);

        doc.Save(Filepath);
    }

    public void Load(string filepath)
    {
        var fi = new FileInfo(filepath);
        Filename = fi.Name;
        Filepath = filepath;

        var doc = new XmlDocument();
        doc.Load(filepath);

        var projelem = doc.DocumentElement;

        Name = Xml.GetChildInnerText(projelem, "Name");
        Version = Xml.GetChildIntAttribute(projelem, "Version");

        YmapFilenames.Clear();
        YmapFiles.Clear();
        var ymapselem = Xml.GetChild(projelem, "YmapFilenames");
        if (ymapselem != null)
            foreach (var node in ymapselem.SelectNodes("Item"))
            {
                var ymapel = node as XmlElement;
                if (ymapel != null) AddYmapFile(ymapel.InnerText);
            }


        YtypFilenames.Clear();
        YtypFiles.Clear();
        var ytypselem = Xml.GetChild(projelem, "YtypFilenames");
        if (ytypselem != null)
            foreach (var node in ytypselem.SelectNodes("Item"))
            {
                var ytypel = node as XmlElement;
                if (ytypel != null) AddYtypFile(ytypel.InnerText);
            }


        YbnFilenames.Clear();
        YbnFiles.Clear();
        var ybnselem = Xml.GetChild(projelem, "YbnFilenames");
        if (ybnselem != null)
            foreach (var node in ybnselem.SelectNodes("Item"))
            {
                var ybnel = node as XmlElement;
                if (ybnel != null) AddYbnFile(ybnel.InnerText);
            }


        YndFilenames.Clear();
        YndFiles.Clear();
        var yndselem = Xml.GetChild(projelem, "YndFilenames");
        if (yndselem != null)
            foreach (var node in yndselem.SelectNodes("Item"))
            {
                var yndel = node as XmlElement;
                if (yndel != null) AddYndFile(yndel.InnerText);
            }


        YnvFilenames.Clear();
        YnvFiles.Clear();
        var ynvselem = Xml.GetChild(projelem, "YnvFilenames");
        if (ynvselem != null)
            foreach (var node in ynvselem.SelectNodes("Item"))
            {
                var ynvel = node as XmlElement;
                if (ynvel != null) AddYnvFile(ynvel.InnerText);
            }


        TrainsFilenames.Clear();
        TrainsFiles.Clear();
        var trainsselem = Xml.GetChild(projelem, "TrainsFilenames");
        if (trainsselem != null)
            foreach (var node in trainsselem.SelectNodes("Item"))
            {
                var trainel = node as XmlElement;
                if (trainel != null) AddTrainsFile(trainel.InnerText);
            }


        ScenarioFilenames.Clear();
        ScenarioFiles.Clear();
        var scenarioselem = Xml.GetChild(projelem, "ScenarioFilenames");
        if (scenarioselem != null)
            foreach (var node in scenarioselem.SelectNodes("Item"))
            {
                var scenarioel = node as XmlElement;
                if (scenarioel != null) AddScenarioFile(scenarioel.InnerText);
            }


        AudioRelFilenames.Clear();
        AudioRelFiles.Clear();
        var audiorelselem = Xml.GetChild(projelem, "AudioRelFilenames");
        if (audiorelselem != null)
            foreach (var node in audiorelselem.SelectNodes("Item"))
            {
                var audiorelel = node as XmlElement;
                if (audiorelel != null) AddAudioRelFile(audiorelel.InnerText);
            }


        YdrFilenames.Clear();
        YdrFiles.Clear();
        var ydrselem = Xml.GetChild(projelem, "YdrFilenames");
        if (ydrselem != null)
            foreach (var node in ydrselem.SelectNodes("Item"))
            {
                var ydrel = node as XmlElement;
                if (ydrel != null) AddYdrFile(ydrel.InnerText);
            }


        YddFilenames.Clear();
        YddFiles.Clear();
        var yddselem = Xml.GetChild(projelem, "YddFilenames");
        if (yddselem != null)
            foreach (var node in yddselem.SelectNodes("Item"))
            {
                var yddel = node as XmlElement;
                if (yddel != null) AddYddFile(yddel.InnerText);
            }


        YftFilenames.Clear();
        YftFiles.Clear();
        var yftselem = Xml.GetChild(projelem, "YftFilenames");
        if (yftselem != null)
            foreach (var node in yftselem.SelectNodes("Item"))
            {
                var yftel = node as XmlElement;
                if (yftel != null) AddYftFile(yftel.InnerText);
            }


        YtdFilenames.Clear();
        YtdFiles.Clear();
        var ytdselem = Xml.GetChild(projelem, "YtdFilenames");
        if (ytdselem != null)
            foreach (var node in ytdselem.SelectNodes("Item"))
            {
                var ytdel = node as XmlElement;
                if (ytdel != null) AddYtdFile(ytdel.InnerText);
            }
    }


    public void UpdateFilenames(string oldprojpath)
    {
        for (var i = 0; i < YmapFilenames.Count; i++)
            YmapFilenames[i] = GetUpdatedFilePath(YmapFilenames[i], oldprojpath);
        for (var i = 0; i < YtypFilenames.Count; i++)
            YtypFilenames[i] = GetUpdatedFilePath(YtypFilenames[i], oldprojpath);
        for (var i = 0; i < YbnFilenames.Count; i++) YbnFilenames[i] = GetUpdatedFilePath(YbnFilenames[i], oldprojpath);
        for (var i = 0; i < YndFilenames.Count; i++) YndFilenames[i] = GetUpdatedFilePath(YndFilenames[i], oldprojpath);
        for (var i = 0; i < YnvFilenames.Count; i++) YnvFilenames[i] = GetUpdatedFilePath(YnvFilenames[i], oldprojpath);
        for (var i = 0; i < TrainsFilenames.Count; i++)
            TrainsFilenames[i] = GetUpdatedFilePath(TrainsFilenames[i], oldprojpath);
        for (var i = 0; i < ScenarioFilenames.Count; i++)
            ScenarioFilenames[i] = GetUpdatedFilePath(ScenarioFilenames[i], oldprojpath);
        for (var i = 0; i < AudioRelFilenames.Count; i++)
            AudioRelFilenames[i] = GetUpdatedFilePath(AudioRelFilenames[i], oldprojpath);
        for (var i = 0; i < YdrFilenames.Count; i++) YdrFilenames[i] = GetUpdatedFilePath(YdrFilenames[i], oldprojpath);
        for (var i = 0; i < YddFilenames.Count; i++) YddFilenames[i] = GetUpdatedFilePath(YddFilenames[i], oldprojpath);
        for (var i = 0; i < YftFilenames.Count; i++) YftFilenames[i] = GetUpdatedFilePath(YftFilenames[i], oldprojpath);
        for (var i = 0; i < YtdFilenames.Count; i++) YtdFilenames[i] = GetUpdatedFilePath(YtdFilenames[i], oldprojpath);
    }

    public string GetUpdatedFilePath(string oldpath, string oldprojpath)
    {
        var fullpath = GetFullFilePath(oldpath, oldprojpath);
        var newpath = GetRelativePath(fullpath);
        return newpath;
    }

    public string GetRelativePath(string filepath)
    {
        if (filepath == null) return string.Empty;
        if (Filepath == null) return filepath;

        Uri fromUri;
        if (!Uri.TryCreate(Filepath, UriKind.RelativeOrAbsolute, out fromUri)) return filepath;

        Uri toUri;
        if (!Uri.TryCreate(filepath, UriKind.RelativeOrAbsolute, out toUri)) return filepath;
        if (!toUri.IsAbsoluteUri) return filepath; //already relative...

        //Uri fromUri = new Uri(Filepath);
        //Uri toUri = new Uri(filepath);
        if (fromUri.Scheme != toUri.Scheme) return filepath.ToLowerInvariant();
        var relativeUri = fromUri.MakeRelativeUri(toUri);
        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());
        if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return relativePath.ToLowerInvariant();
    }

    public string GetFullFilePath(string relpath)
    {
        if (string.IsNullOrEmpty(Filepath)) return relpath;
        var projfldr = new FileInfo(Filepath).DirectoryName + "\\";
        var cpath = Path.Combine(projfldr, relpath);
        var apath = Path.GetFullPath(cpath);
        return apath;
    }

    public string GetFullFilePath(string relpath, string basepath)
    {
        if (string.IsNullOrEmpty(basepath)) return relpath;
        var basefldr = new FileInfo(basepath).DirectoryName + "\\";
        var cpath = Path.Combine(basefldr, relpath);
        var apath = Path.GetFullPath(cpath);
        return apath;
    }


    public YmapFile AddYmapFile(string filename)
    {
        var ymap = new YmapFile();
        ymap.RpfFileEntry = new RpfResourceFileEntry();
        ymap.RpfFileEntry.Name = Path.GetFileName(filename);
        ymap.FilePath = GetFullFilePath(filename);
        ymap.Name = ymap.RpfFileEntry.Name;
        JenkIndex.Ensure(ymap.Name);
        JenkIndex.Ensure(Path.GetFileNameWithoutExtension(ymap.Name));
        JenkIndex.Ensure(filename);
        if (!AddYmapFile(ymap)) return null;
        return ymap;
    }

    public bool AddYmapFile(YmapFile ymap)
    {
        var relpath = GetRelativePath(ymap.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ymap.Name;
        if (YmapFilenames.Contains(relpath)) return false;
        YmapFilenames.Add(relpath);
        YmapFiles.Add(ymap);
        return true;
    }

    public void RemoveYmapFile(YmapFile ymap)
    {
        if (ymap == null) return;
        var relpath = GetRelativePath(ymap.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ymap.Name;
        YmapFiles.Remove(ymap);
        YmapFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYmap(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var ymapfn in YmapFilenames)
            if (ymapfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYmap(YmapFile ymap)
    {
        foreach (var f in YmapFiles)
            if (f == ymap)
                return true;
        return false;
    }

    public bool RenameYmap(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YmapFilenames.Count; i++)
            if (YmapFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YmapFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YtypFile AddYtypFile(string filename)
    {
        var ytyp = new YtypFile();
        ytyp.RpfFileEntry = new RpfResourceFileEntry();
        ytyp.RpfFileEntry.Name = Path.GetFileName(filename);
        ytyp.FilePath = GetFullFilePath(filename);
        ytyp.Name = ytyp.RpfFileEntry.Name;
        JenkIndex.Ensure(ytyp.Name);
        JenkIndex.Ensure(Path.GetFileNameWithoutExtension(ytyp.Name));
        JenkIndex.Ensure(filename);
        if (!AddYtypFile(ytyp)) return null;
        return ytyp;
    }

    public bool AddYtypFile(YtypFile ytyp)
    {
        var relpath = GetRelativePath(ytyp.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ytyp.Name;
        if (YtypFilenames.Contains(relpath)) return false;
        YtypFilenames.Add(relpath);
        YtypFiles.Add(ytyp);
        return true;
    }

    public void RemoveYtypFile(YtypFile ytyp)
    {
        if (ytyp == null) return;
        var relpath = GetRelativePath(ytyp.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ytyp.Name;
        YtypFiles.Remove(ytyp);
        YtypFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYtyp(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var ytypfn in YtypFilenames)
            if (ytypfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYtyp(YtypFile ytyp)
    {
        foreach (var f in YtypFiles)
            if (f == ytyp)
                return true;
        return false;
    }

    public bool RenameYtyp(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YtypFilenames.Count; i++)
            if (YtypFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YtypFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YbnFile AddYbnFile(string filename)
    {
        var ybn = new YbnFile();
        ybn.RpfFileEntry = new RpfResourceFileEntry();
        ybn.RpfFileEntry.Name = Path.GetFileName(filename);
        ybn.FilePath = GetFullFilePath(filename);
        ybn.Name = ybn.RpfFileEntry.Name;
        if (!AddYbnFile(ybn)) return null;
        return ybn;
    }

    public bool AddYbnFile(YbnFile ybn)
    {
        var relpath = GetRelativePath(ybn.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ybn.Name;
        if (YndFilenames.Contains(relpath)) return false;
        YbnFilenames.Add(relpath);
        YbnFiles.Add(ybn);
        return true;
    }

    public void RemoveYbnFile(YbnFile ybn)
    {
        if (ybn == null) return;
        var relpath = GetRelativePath(ybn.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ybn.Name;
        YbnFiles.Remove(ybn);
        YbnFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYbn(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var yndfn in YbnFilenames)
            if (yndfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYbn(YbnFile ybn)
    {
        foreach (var f in YbnFiles)
            if (f == ybn)
                return true;
        return false;
    }

    public bool RenameYbn(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YbnFilenames.Count; i++)
            if (YbnFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YbnFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YndFile AddYndFile(string filename)
    {
        var ynd = new YndFile();
        ynd.RpfFileEntry = new RpfResourceFileEntry();
        ynd.RpfFileEntry.Name = Path.GetFileName(filename);
        ynd.FilePath = GetFullFilePath(filename);
        ynd.Name = ynd.RpfFileEntry.Name;
        if (!AddYndFile(ynd)) return null;
        return ynd;
    }

    public bool AddYndFile(YndFile ynd)
    {
        var relpath = GetRelativePath(ynd.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ynd.Name;
        if (YndFilenames.Contains(relpath)) return false;
        YndFilenames.Add(relpath);
        YndFiles.Add(ynd);
        return true;
    }

    public void RemoveYndFile(YndFile ynd)
    {
        if (ynd == null) return;
        var relpath = GetRelativePath(ynd.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ynd.Name;
        YndFiles.Remove(ynd);
        YndFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYnd(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var yndfn in YndFilenames)
            if (yndfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYnd(YndFile ynd)
    {
        foreach (var f in YndFiles)
            if (f == ynd)
                return true;
        return false;
    }

    public bool RenameYnd(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YndFilenames.Count; i++)
            if (YndFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YndFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YnvFile AddYnvFile(string filename)
    {
        var ynv = new YnvFile();
        ynv.RpfFileEntry = new RpfResourceFileEntry();
        ynv.RpfFileEntry.Name = Path.GetFileName(filename);
        ynv.FilePath = GetFullFilePath(filename);
        ynv.Name = ynv.RpfFileEntry.Name;
        if (!AddYnvFile(ynv)) return null;
        return ynv;
    }

    public bool AddYnvFile(YnvFile ynv)
    {
        var relpath = GetRelativePath(ynv.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ynv.Name;
        if (YnvFilenames.Contains(relpath)) return false;
        YnvFilenames.Add(relpath);
        YnvFiles.Add(ynv);
        return true;
    }

    public void RemoveYnvFile(YnvFile ynv)
    {
        if (ynv == null) return;
        var relpath = GetRelativePath(ynv.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ynv.Name;
        YnvFiles.Remove(ynv);
        YnvFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYnv(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var ynvfn in YnvFilenames)
            if (ynvfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYnv(YnvFile ynv)
    {
        foreach (var f in YnvFiles)
            if (f == ynv)
                return true;
        return false;
    }

    public bool RenameYnv(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YnvFilenames.Count; i++)
            if (YnvFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YnvFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public TrainTrack AddTrainsFile(string filename)
    {
        var track = new TrainTrack();
        track.RpfFileEntry = new RpfResourceFileEntry();
        track.RpfFileEntry.Name = Path.GetFileName(filename);
        track.FilePath = GetFullFilePath(filename);
        track.Name = track.RpfFileEntry.Name;
        if (!AddTrainsFile(track)) return null;
        return track;
    }

    public bool AddTrainsFile(TrainTrack track)
    {
        var relpath = GetRelativePath(track.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = track.Name;
        if (TrainsFilenames.Contains(relpath)) return false;
        TrainsFilenames.Add(relpath);
        TrainsFiles.Add(track);
        return true;
    }

    public void RemoveTrainsFile(TrainTrack track)
    {
        if (track == null) return;
        var relpath = GetRelativePath(track.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = track.Name;
        TrainsFiles.Remove(track);
        TrainsFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsTrainTrack(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var trainsfn in TrainsFilenames)
            if (trainsfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsTrainTrack(TrainTrack track)
    {
        foreach (var f in TrainsFiles)
            if (f == track)
                return true;
        return false;
    }

    public bool RenameTrainTrack(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < TrainsFilenames.Count; i++)
            if (TrainsFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                TrainsFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YmtFile AddScenarioFile(string filename)
    {
        var scenario = new YmtFile();
        scenario.RpfFileEntry = new RpfResourceFileEntry();
        scenario.RpfFileEntry.Name = Path.GetFileName(filename);
        scenario.FilePath = GetFullFilePath(filename);
        scenario.Name = scenario.RpfFileEntry.Name;
        scenario.ContentType = YmtFileContentType.ScenarioPointRegion;
        scenario.FileFormat = YmtFileFormat.RSC;
        if (!AddScenarioFile(scenario)) return null;
        return scenario;
    }

    public bool AddScenarioFile(YmtFile ymt)
    {
        var relpath = GetRelativePath(ymt.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ymt.Name;
        if (ScenarioFilenames.Contains(relpath)) return false;
        ScenarioFilenames.Add(relpath);
        ScenarioFiles.Add(ymt);
        return true;
    }

    public void RemoveScenarioFile(YmtFile ymt)
    {
        if (ymt == null) return;
        var relpath = GetRelativePath(ymt.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ymt.Name;
        ScenarioFiles.Remove(ymt);
        ScenarioFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsScenario(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var scenariofn in ScenarioFilenames)
            if (scenariofn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsScenario(YmtFile ymt)
    {
        foreach (var f in ScenarioFiles)
            if (f == ymt)
                return true;
        return false;
    }

    public bool RenameScenario(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < ScenarioFilenames.Count; i++)
            if (ScenarioFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                ScenarioFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public RelFile AddAudioRelFile(string filename)
    {
        var relfile = new RelFile();
        relfile.RpfFileEntry = new RpfResourceFileEntry();
        relfile.RpfFileEntry.Name = Path.GetFileName(filename);
        relfile.RpfFileEntry.NameHash = JenkHash.GenHash(relfile.RpfFileEntry.Name);
        relfile.FilePath = GetFullFilePath(filename);
        relfile.Name = relfile.RpfFileEntry.Name;
        if (!AddAudioRelFile(relfile)) return null;
        return relfile;
    }

    public bool AddAudioRelFile(RelFile rel)
    {
        var relpath = GetRelativePath(rel.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = rel.Name;
        if (AudioRelFilenames.Contains(relpath)) return false;
        AudioRelFilenames.Add(relpath);
        AudioRelFiles.Add(rel);
        return true;
    }

    public void RemoveAudioRelFile(RelFile rel)
    {
        if (rel == null) return;
        var relpath = GetRelativePath(rel.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = rel.Name;
        AudioRelFiles.Remove(rel);
        AudioRelFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsAudioRel(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var audiorelfn in AudioRelFilenames)
            if (audiorelfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsAudioRel(RelFile rel)
    {
        foreach (var f in AudioRelFiles)
            if (f == rel)
                return true;
        return false;
    }

    public bool RenameAudioRel(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < AudioRelFilenames.Count; i++)
            if (AudioRelFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                AudioRelFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YdrFile AddYdrFile(string filename)
    {
        var ydr = new YdrFile();
        ydr.RpfFileEntry = new RpfResourceFileEntry();
        ydr.RpfFileEntry.Name = Path.GetFileName(filename);
        ydr.FilePath = GetFullFilePath(filename);
        ydr.Name = ydr.RpfFileEntry.Name;
        if (!AddYdrFile(ydr)) return null;
        return ydr;
    }

    public bool AddYdrFile(YdrFile ydr)
    {
        var relpath = GetRelativePath(ydr.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ydr.Name;
        if (YdrFilenames.Contains(relpath)) return false;
        YdrFilenames.Add(relpath);
        YdrFiles.Add(ydr);
        return true;
    }

    public void RemoveYdrFile(YdrFile ydr)
    {
        if (ydr == null) return;
        var relpath = GetRelativePath(ydr.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ydr.Name;
        YdrFiles.Remove(ydr);
        YdrFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYdr(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var ydrfn in YdrFilenames)
            if (ydrfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYdr(YdrFile ydr)
    {
        foreach (var f in YdrFiles)
            if (f == ydr)
                return true;
        return false;
    }

    public bool RenameYdr(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YdrFilenames.Count; i++)
            if (YdrFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YdrFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YddFile AddYddFile(string filename)
    {
        var ydd = new YddFile();
        ydd.RpfFileEntry = new RpfResourceFileEntry();
        ydd.RpfFileEntry.Name = Path.GetFileName(filename);
        ydd.FilePath = GetFullFilePath(filename);
        ydd.Name = ydd.RpfFileEntry.Name;
        if (!AddYddFile(ydd)) return null;
        return ydd;
    }

    public bool AddYddFile(YddFile ydd)
    {
        var relpath = GetRelativePath(ydd.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ydd.Name;
        if (YddFilenames.Contains(relpath)) return false;
        YddFilenames.Add(relpath);
        YddFiles.Add(ydd);
        return true;
    }

    public void RemoveYddFile(YddFile ydd)
    {
        if (ydd == null) return;
        var relpath = GetRelativePath(ydd.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ydd.Name;
        YddFiles.Remove(ydd);
        YddFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYdd(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var yddfn in YddFilenames)
            if (yddfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYdd(YddFile ydd)
    {
        foreach (var f in YddFiles)
            if (f == ydd)
                return true;
        return false;
    }

    public bool RenameYdd(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YddFilenames.Count; i++)
            if (YddFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YddFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YftFile AddYftFile(string filename)
    {
        var yft = new YftFile();
        yft.RpfFileEntry = new RpfResourceFileEntry();
        yft.RpfFileEntry.Name = Path.GetFileName(filename);
        yft.FilePath = GetFullFilePath(filename);
        yft.Name = yft.RpfFileEntry.Name;
        if (!AddYftFile(yft)) return null;
        return yft;
    }

    public bool AddYftFile(YftFile yft)
    {
        var relpath = GetRelativePath(yft.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = yft.Name;
        if (YftFilenames.Contains(relpath)) return false;
        YftFilenames.Add(relpath);
        YftFiles.Add(yft);
        return true;
    }

    public void RemoveYftFile(YftFile yft)
    {
        if (yft == null) return;
        var relpath = GetRelativePath(yft.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = yft.Name;
        YftFiles.Remove(yft);
        YftFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYft(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var yftfn in YftFilenames)
            if (yftfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYft(YftFile yft)
    {
        foreach (var f in YftFiles)
            if (f == yft)
                return true;
        return false;
    }

    public bool RenameYft(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YftFilenames.Count; i++)
            if (YftFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YftFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }


    public YtdFile AddYtdFile(string filename)
    {
        var ytd = new YtdFile();
        ytd.RpfFileEntry = new RpfResourceFileEntry();
        ytd.RpfFileEntry.Name = Path.GetFileName(filename);
        ytd.FilePath = GetFullFilePath(filename);
        ytd.Name = ytd.RpfFileEntry.Name;
        if (!AddYtdFile(ytd)) return null;
        return ytd;
    }

    public bool AddYtdFile(YtdFile ytd)
    {
        var relpath = GetRelativePath(ytd.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ytd.Name;
        if (YtdFilenames.Contains(relpath)) return false;
        YtdFilenames.Add(relpath);
        YtdFiles.Add(ytd);
        return true;
    }

    public void RemoveYtdFile(YtdFile ytd)
    {
        if (ytd == null) return;
        var relpath = GetRelativePath(ytd.FilePath);
        if (string.IsNullOrEmpty(relpath)) relpath = ytd.Name;
        YtdFiles.Remove(ytd);
        YtdFilenames.Remove(relpath);
        HasChanged = true;
    }

    public bool ContainsYtd(string filename)
    {
        var found = false;
        filename = filename.ToLowerInvariant();
        foreach (var ytdfn in YtdFilenames)
            if (ytdfn == filename)
            {
                found = true;
                break;
            }

        return found;
    }

    public bool ContainsYtd(YtdFile ytd)
    {
        foreach (var f in YtdFiles)
            if (f == ytd)
                return true;
        return false;
    }

    public bool RenameYtd(string oldfilename, string newfilename)
    {
        oldfilename = oldfilename.ToLowerInvariant();
        newfilename = newfilename.ToLowerInvariant();
        for (var i = 0; i < YtdFilenames.Count; i++)
            if (YtdFilenames[i]?.ToLowerInvariant() == oldfilename)
            {
                YtdFilenames[i] = newfilename;
                HasChanged = true;
                return true;
            }

        return false;
    }
}