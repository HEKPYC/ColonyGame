using System;
using System.Collections.Generic;
using ColonyGame.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColonyGame.Presentation
{
    public sealed class PrototypePalette
    {
        private readonly Material source;
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        public PrototypePalette(Material source) { this.source = source; }
        public Material Get(Color color)
        {
            if (!materials.TryGetValue(color, out var material))
            {
                material = new Material(source) { name = "Runtime schematic color" };
                material.SetColor("_BaseColor", color);
                materials.Add(color, material);
            }
            return material;
        }
        public void Dispose() { foreach (var material in materials.Values) UnityEngine.Object.Destroy(material); }
        public static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            shape.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            UnityEngine.Object.Destroy(shape.GetComponent<Collider>());
            return shape;
        }
    }
    public static class SchematicBuildingView
    {
        private static readonly Color Wood = new Color(.52f,.32f,.16f), Stone = new Color(.4f,.47f,.52f), Cream = new Color(.82f,.77f,.58f), Blue = new Color(.18f,.55f,.78f), Green = new Color(.32f,.55f,.15f), Purple = new Color(.59f,.3f,.8f);
        public static void Create(BuildingState building, Transform parent, PrototypePalette palette)
        {
            var site = new GameObject(building.Definition.DisplayName + " Site");
            site.transform.SetParent(parent, false);
            float w = building.Definition.BaseWidth, d = building.Definition.BaseDepth;
            var offset = building.OrientationDegrees == 90 ? new Vector3(0,0,w) : building.OrientationDegrees == 180 ? new Vector3(w,0,d) : building.OrientationDegrees == 270 ? new Vector3(d,0,0) : Vector3.zero;
            site.transform.localPosition = new Vector3(building.Origin.X,0,building.Origin.Z) + offset;
            site.transform.localRotation = Quaternion.Euler(0,building.OrientationDegrees,0);
            // All canonical site geometry is inset within [0,w] x [0,d]; transform rotates the entire site.
            Action<string,float,float,float,float,float,Color> box = (name,x,z,sx,sz,height,color) =>
                PrototypePalette.Shape(site.transform,name,PrimitiveType.Cube,new Vector3(x*w,height*.5f,z*d),new Vector3(sx*w,height,sz*d),palette.Get(color));
            box("West site edge",.01f,.5f,.02f,.98f,.12f,Cream);
            box("East site edge",.99f,.5f,.02f,.98f,.12f,Cream);
            box("South site edge",.5f,.01f,.98f,.02f,.12f,Cream);
            box("North site edge",.5f,.99f,.98f,.02f,.12f,Cream);
            Action<float,float,float,float,Color> hut = (x,z,sx,sz,color) =>
            {
                box("Walls",x,z,sx,sz,.6f,color);
                var roof = PrototypePalette.Shape(site.transform,"Roof",PrimitiveType.Cube,new Vector3(x*w,.67f,z*d),new Vector3(sx*w,.14f,sz*d),palette.Get(Wood));
                // The roof remains within the same normalized building volume.
            };
            switch (building.Type)
            {
                case BuildingType.TownHall:
                    hut(.5f,.5f,.56f,.52f,Cream); box("Civic tower",.5f,.65f,.18f,.18f,1.65f,Stone);
                    box("Civic approach",.5f,.22f,.22f,.2f,.04f,Stone); break;
                case BuildingType.BuildersHut:
                    hut(.4f,.58f,.44f,.5f,Cream); box("Material stack",.78f,.35f,.22f,.2f,.3f,Wood); box("Workbench",.35f,.2f,.4f,.16f,.2f,Stone); break;
                case BuildingType.House:
                    hut(.5f,.5f,.55f,.6f,new Color(.75f,.42f,.3f)); box("Chimney",.66f,.65f,.09f,.09f,1f,Stone); break;
                case BuildingType.Farm:
                    for (int row=0;row<4;row++) box("Crop row",.5f,.2f+row*.2f,.8f,.09f,.08f,row%2==0?Green:Wood);
                    break;
                case BuildingType.Tavern:
                    hut(.5f,.65f,.62f,.5f,new Color(.7f,.48f,.2f)); box("Table",.4f,.23f,.24f,.18f,.25f,Wood); box("Barrel",.75f,.23f,.13f,.14f,.4f,Wood); box("Sign",.2f,.35f,.05f,.08f,1f,Cream); break;
                case BuildingType.Butcher:
                    hut(.5f,.25f,.65f,.3f,new Color(.62f,.35f,.27f)); box("Empty yard divider",.5f,.45f,.86f,.015f,.2f,Wood); box("Empty yard soil",.5f,.7f,.8f,.45f,.02f,Wood); break;
                case BuildingType.FishermansHut:
                    hut(.5f,.24f,.5f,.28f,Wood); box("Pond",.5f,.7f,.72f,.44f,.035f,Blue); box("Dock",.2f,.62f,.16f,.28f,.08f,Wood); break;
                case BuildingType.GuardTower:
                    box("Watch tower",.5f,.5f,.48f,.48f,1.65f,Stone); PrototypePalette.Shape(site.transform,"Tower battlement",PrimitiveType.Cube,new Vector3(.5f*w,1.65f,.5f*d),new Vector3(.62f*w,.18f,.62f*d),palette.Get(Cream)); break;
                case BuildingType.Warehouse:
                    hut(.5f,.65f,.72f,.43f,Stone); for(int i=0;i<3;i++) box("Crate",.25f+i*.25f,.25f,.16f,.18f,.42f,Wood); break;
                case BuildingType.LumberMill:
                    hut(.5f,.65f,.62f,.42f,Wood); for(int i=0;i<3;i++) box("Log stack",.5f,.16f+i*.09f,.68f,.06f,.2f,Cream); break;
                case BuildingType.Mine:
                    box("Rock bank",.5f,.58f,.75f,.55f,.8f,Stone); box("Dark entrance",.5f,.27f,.3f,.18f,.6f,new Color(.12f,.14f,.16f)); box("Track marking",.5f,.13f,.15f,.16f,.04f,Wood); break;
                case BuildingType.Barracks:
                    hut(.5f,.7f,.7f,.36f,Stone); box("Training court",.5f,.3f,.75f,.35f,.02f,Cream); box("Training post",.3f,.3f,.06f,.06f,.8f,Wood); box("Military banner",.72f,.35f,.07f,.13f,1f,new Color(.55f,.16f,.17f)); break;
                case BuildingType.Laboratory:
                    hut(.4f,.5f,.45f,.6f,new Color(.82f,.9f,.91f)); box("Research wing",.75f,.55f,.24f,.4f,.9f,Blue); box("Instrument",.72f,.2f,.14f,.14f,.45f,Stone); break;
                case BuildingType.MageTower:
                    box("Mage tower",.3f,.55f,.26f,.3f,1.7f,Purple); box("Shared Place of Power",.73f,.5f,.3f,.45f,.07f,Blue);
                    PrototypePalette.Shape(site.transform,"Power crystal",PrimitiveType.Sphere,new Vector3(.73f*w,.38f,.5f*d),new Vector3(.3f,.65f,.3f),palette.Get(Purple)); break;
            }
        }
    }
}
