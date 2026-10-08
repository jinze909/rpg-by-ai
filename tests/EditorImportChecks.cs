// Invoke the actual import rule, with declarations only for Unity infrastructure.
using System;
using System.IO;
using System.Reflection;
using Starfall.Editor;
using UnityEditor;
using UnityEngine;
public static class EditorImportChecks
{
    public static int Main()
    {
        int count=0;
        foreach(var path in Directory.GetFiles("Assets/Resources/Frontier/Art","*.png",SearchOption.AllDirectories))
        {
            if(path.EndsWith("/TitleVista.png")||path.EndsWith("/Portraits.png"))continue;
            var rule=new FrontierImports();var texture=new TextureImporter();rule.assetPath=path.Replace('\\','/');rule.assetImporter=texture;
            typeof(FrontierImports).GetMethod("OnPreprocessTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rule,null);
            string name=Path.GetFileNameWithoutExtension(path);
            bool readable=(name.StartsWith("Player")&&name!="PlayerPortraits")||name.StartsWith("Walk")||name.StartsWith("Companion")||name=="Monsters"||name=="Bosses"||name=="EnemyArcherVertical";
            if(texture.isReadable!=readable||texture.filterMode!=FilterMode.Point||texture.mipmapEnabled||texture.maxTextureSize!=4096||texture.textureCompression!=TextureImporterCompression.Uncompressed)throw new Exception("Fresh native import rule contradicts fixed pixel metadata: "+path);
            count++;
        }
        Console.WriteLine("PASS Production Editor import rule: "+count+" textures, sources readable, prepared masks immutable, Point/no mipmaps/uncompressed");return 0;
    }
}
