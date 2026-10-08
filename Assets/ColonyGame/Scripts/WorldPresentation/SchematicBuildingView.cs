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
        private float nightAmount;
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        public PrototypePalette(Material source) { this.source = source; }
        public Material Get(Color color)
        {
            if (!materials.TryGetValue(color, out var material))
            {
                material = new Material(source) { name = "Runtime schematic color" };
                material.SetColor("_BaseColor", Tint(color));
                materials.Add(color, material);
            }
            return material;
        }
                private Color Tint(Color color)=>color*Color.Lerp(Color.white,new Color(.55f,.6f,.75f,1),nightAmount);
        public void SetNightAmount(float amount)
        {
            nightAmount=amount;
            foreach(var pair in materials) pair.Value.SetColor("_BaseColor",Tint(pair.Key));
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
        private static void CreateEntranceThreshold(BuildingState building,Transform site,PrototypePalette palette)
        {
            float w=building.Definition.BaseWidth,d=building.Definition.BaseDepth;
            var entrance=building.Entrance;
            var facing=new Vector3(entrance.Facing.X,0,entrance.Facing.Z);
            var boundary=new Vector3(entrance.FootprintTile.X+.5f,0,entrance.FootprintTile.Z+.5f)+facing*.5f;
            // Derive the opening from domain entrance data, then convert into rotated site space.
            var inverse=Quaternion.Inverse(site.localRotation);
            var gate=inverse*(boundary-site.localPosition);
            var normal=inverse*facing;
            var inset=gate-normal*.07f;
            var thresholdScale=Mathf.Abs(normal.z)>.5f?new Vector3(.72f,.04f,.12f):new Vector3(.12f,.04f,.72f);
            PrototypePalette.Shape(site,"Entrance threshold",PrimitiveType.Cube,inset+Vector3.up*.025f,thresholdScale,palette.Get(Blue));
        }
        public static void Create(BuildingState building, Transform parent, PrototypePalette palette)
        {
            var site = new GameObject(building.Id);
            site.transform.SetParent(parent, false);
            float w = building.Definition.BaseWidth, d = building.Definition.BaseDepth;
            var offset = building.OrientationDegrees == 90 ? new Vector3(0,0,w) : building.OrientationDegrees == 180 ? new Vector3(w,0,d) : building.OrientationDegrees == 270 ? new Vector3(d,0,0) : Vector3.zero;
            site.transform.localPosition = new Vector3(building.Origin.X,0,building.Origin.Z) + offset;
            site.transform.localRotation = Quaternion.Euler(0,building.OrientationDegrees,0);
            // All canonical site geometry is inset within [0,w] x [0,d]; transform rotates the entire site.
            Action<string,float,float,float,float,float,Color> box = (name,x,z,sx,sz,height,color) =>
                PrototypePalette.Shape(site.transform,name,PrimitiveType.Cube,new Vector3(x*w,height*.5f,z*d),new Vector3(sx*w,height,sz*d),palette.Get(color));
            CreateEntranceThreshold(building,site.transform,palette);
            Action<float,float,float,float,Color> hut = (x,z,sx,sz,color) =>
            {
                box("Walls",x,z,sx,sz,1.15f,color);
                var roof = PrototypePalette.Shape(site.transform,"Roof",PrimitiveType.Cube,new Vector3(x*w,1.28f,z*d),new Vector3(sx*w,.22f,sz*d),palette.Get(Wood));
                float front=z*d-sz*d*.5f;
                PrototypePalette.Shape(site.transform,"Door",PrimitiveType.Cube,new Vector3(x*w,.38f,front-.012f),new Vector3(.32f,.76f,.035f),palette.Get(Wood));
                foreach(float side in new[]{-1f,1f})
                    PrototypePalette.Shape(site.transform,"Dark window",PrimitiveType.Cube,new Vector3(x*w+side*sx*w*.3f,.82f,front-.02f),new Vector3(.3f,.27f,.035f),palette.Get(new Color(.08f,.12f,.17f)));
                if(building.Type==BuildingType.TownHall||building.Type==BuildingType.House||building.Type==BuildingType.Tavern)
                    foreach(float side in new[]{-1f,1f})
                    {
                        PrototypePalette.Shape(site.transform,"Dark window",PrimitiveType.Cube,new Vector3(x*w+side*sx*w*.3f,.82f,z*d+sz*d*.5f+.02f),new Vector3(.3f,.27f,.035f),palette.Get(new Color(.08f,.12f,.17f)));
                        foreach(float face in new[]{-1f,1f})
                            PrototypePalette.Shape(site.transform,"Dark window",PrimitiveType.Cube,new Vector3(x*w+face*(sx*w*.5f+.02f),.82f,z*d+side*sz*d*.3f),new Vector3(.035f,.27f,.3f),palette.Get(new Color(.08f,.12f,.17f)));
                    }
                // Windows on all residential faces remain visible after quarter-turn rotation.
                // The roof remains within the same normalized building volume.
            };
            if(!building.IsCompleted||building.IsUpgrading)
            {
                box("Construction foundation",.5f,.5f,.82f,.82f,.045f,Stone);
                foreach(float x in new[]{.2f,.8f})
                    foreach(float z in new[]{.2f,.8f}) box("Scaffolding",x,z,.035f,.035f,1.5f,Wood);
            }
            for(int tier=1;tier<building.Level;tier++)
            {
                float x=building.Type==BuildingType.MageTower?.3f*w:.5f*w;
                PrototypePalette.Shape(site.transform,"Level "+(tier+1)+" trim",PrimitiveType.Cube,
                    new Vector3(x,1.15f+tier*.12f,.55f*d),new Vector3(building.Type==BuildingType.MageTower?.28f*w:.35f*w,.035f,.32f*d),palette.Get(Cream));
            }
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
            if(building.IsUpgrading)
                PrototypePalette.Shape(site.transform,"Upgrade work",PrimitiveType.Cube,new Vector3(.5f*w,1.48f,.5f*d),new Vector3(.4f*w,.07f,.35f*d),palette.Get(Cream));
            UpdateConstruction(building,site.transform);
        }
        public static void UpdateConstruction(BuildingState building,Transform site)
        {
            float progress=(float)building.ConstructionProgress;
            foreach(Transform part in site)
            {
                bool scaffolding=part.name=="Scaffolding"||part.name=="Construction foundation";
                if(scaffolding) {part.gameObject.SetActive(!building.IsCompleted||building.IsUpgrading);continue;}
                if(part.name=="Upgrade work") {part.gameObject.SetActive(building.IsUpgrading&&progress>.15f);part.localScale=new Vector3(building.Definition.BaseWidth*.4f*progress,.07f,building.Definition.BaseDepth*.35f);continue;}
                if(building.IsCompleted) {part.gameObject.SetActive(true);continue;}
                bool edge=part.name.StartsWith("Entrance");
                bool roof=part.name=="Roof"||part.name.Contains("battlement");
                bool walls=part.name=="Walls"||part.name=="Door"||part.name.Contains("window");
                part.gameObject.SetActive(edge||PrototypePresentationRules.ShowConstructionPart(progress,roof,walls));
                if(part.name=="Walls")
                {
                    float height=1.15f*Mathf.Clamp01((progress-.35f)/.5f);
                    var scale=part.localScale;scale.y=height;part.localScale=scale;
                    var position=part.localPosition;position.y=height*.5f;part.localPosition=position;
                }
                if(part.name=="Roof") {var scale=part.localScale;scale.y=.22f*Mathf.Clamp01((progress-.85f)/.15f);part.localScale=scale;}
                // Completed geometry retains original scale; staged visibility never alters logical footprints.
            }
        }
    }
}
