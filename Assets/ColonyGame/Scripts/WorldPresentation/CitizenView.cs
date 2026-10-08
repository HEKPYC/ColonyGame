using System.Collections.Generic;
using System.Linq;
using ColonyGame.World;
using UnityEngine;

namespace ColonyGame.Presentation
{
    // Replaceable schematic presentation. No citizen simulation runs in GameObjects.
    public sealed class CitizenView
    {
        private readonly CitizenSimulation simulation;
        private readonly PrototypePalette palette;
        private readonly Dictionary<string,CitizenProfession> shownProfessions=new Dictionary<string,CitizenProfession>();
        private readonly Transform parent;
        private readonly GameObject root;
        private readonly Dictionary<string,Transform> models=new Dictionary<string,Transform>();
        private readonly Dictionary<string,GameObject> indicators=new Dictionary<string,GameObject>();
        private readonly Mesh ringMesh;
        private readonly Dictionary<string,GameObject> professionLayers=new Dictionary<string,GameObject>();
        public CitizenView(Transform parent,CitizenSimulation simulation,PrototypePalette palette)
        {
            this.parent=parent; this.simulation=simulation; this.palette=palette;
            simulation.DevelopmentWarning+=ReportWarning;
            root=new GameObject("Settlement Citizens"); root.transform.SetParent(parent,false);
            ringMesh=CreateRing();
            foreach(var citizen in simulation.Citizens) CreateCitizen(citizen);
            Sync(null);
        }
        private void CreateCitizen(Citizen citizen)
        {
                var model=new GameObject(citizen.FirstName+" ("+citizen.Id+")");
                model.transform.SetParent(root.transform,false);
                models.Add(citizen.Id,model.transform);
                bool female=citizen.Sex==CitizenSex.Female;
                var shirt=female?new Color(.7f,.34f,.68f):new Color(.12f,.65f,.7f);
                var skin=new Color(.86f,.65f,.45f);
                var trousers=new Color(.2f,.24f,.32f);
                System.Action<string,Vector3,Vector3,Color> box=(name,pos,size,color)=>
                    PrototypePalette.Shape(model.transform,name,PrimitiveType.Cube,pos,size,palette.Get(color));
                foreach(float x in new[]{-.1f,.1f})
                {
                    box("Leg",new Vector3(x,.22f,0),new Vector3(.12f,.44f,.18f),trousers);
                    box("Arm",new Vector3(x*2.05f,.67f,0),new Vector3(.09f,.4f,.16f),shirt);
                }
                box("Ordinary tunic",new Vector3(0,.66f,0),new Vector3(.31f,.43f,.22f),shirt);
                if(female) box("Tunic hem",new Vector3(0,.43f,0),new Vector3(.4f,.2f,.26f),shirt);
                PrototypePalette.Shape(model.transform,"Head",PrimitiveType.Sphere,new Vector3(0,1.03f,0),new Vector3(.26f,.28f,.26f),palette.Get(skin));
                box("Hair",new Vector3(0,1.15f,-.025f),new Vector3(.27f,.08f,.26f),new Color(.23f,.15f,.1f));
                if(female) box("Long hair",new Vector3(0,.98f,-.13f),new Vector3(.26f,.28f,.05f),new Color(.23f,.15f,.1f));
                box("Face direction",new Vector3(0,1.03f,.135f),new Vector3(.12f,.045f,.025f),skin);
                var apron=PrototypePalette.Shape(model.transform,"Builder work apron",PrimitiveType.Cube,new Vector3(0,.56f,.145f),new Vector3(.28f,.48f,.06f),palette.Get(new Color(.67f,.46f,.18f)));
                PrototypePalette.Shape(apron.transform,"Tool belt",PrimitiveType.Cube,new Vector3(0,.1f,.6f),new Vector3(1.08f,.16f,1.1f),palette.Get(new Color(.25f,.18f,.12f)));
                professionLayers.Add(citizen.Id,apron); apron.SetActive(false);
                var ring=new GameObject("Citizen selection ring");
                ring.transform.SetParent(model.transform,false); ring.transform.localPosition=Vector3.up*.025f;
                ring.AddComponent<MeshFilter>().sharedMesh=ringMesh;
                var renderer=ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=palette.Get(new Color(.3f,.9f,1f));
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                indicators.Add(citizen.Id,ring); ring.SetActive(false);
        }
        private static Mesh CreateRing()
        {
            const int segments=32;
            var vertices=new Vector3[segments*2];
            var triangles=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                var unit=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[i*2]=unit*.34f; vertices[i*2+1]=unit*.4f;
                int a=i*2,b=((i+1)%segments)*2,k=i*6;
                triangles[k]=a;triangles[k+1]=b;triangles[k+2]=a+1;
                triangles[k+3]=a+1;triangles[k+4]=b;triangles[k+5]=b+1;
            }
            var mesh=new Mesh{name="Runtime Citizen Selection Ring"};
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
        public Citizen Pick(Ray worldRay)
        {
            var localRay=new Ray(parent.InverseTransformPoint(worldRay.origin),parent.InverseTransformDirection(worldRay.direction));
            Citizen closest=null;float nearest=float.PositiveInfinity;
            foreach(var citizen in simulation.Citizens)
            {
                if(!citizen.Active||citizen.InsideBuildingId!=null) continue;
                var bounds=new Bounds(new Vector3((float)citizen.Position.X,.65f,(float)citizen.Position.Z),new Vector3(.56f,1.3f,.56f));
                if(bounds.IntersectRay(localRay,out float distance)&&distance<nearest) { nearest=distance;closest=citizen; }
            }
            return closest;
        }
        public void Sync(Citizen selected)
        {
            foreach(var id in models.Keys.Where(id=>!simulation.Citizens.Any(c=>c.Id==id)).ToArray())
            {
                Object.Destroy(models[id].gameObject);
                models.Remove(id);indicators.Remove(id);professionLayers.Remove(id);shownProfessions.Remove(id);
            }
            foreach(var citizen in simulation.Citizens) if(!models.ContainsKey(citizen.Id)) CreateCitizen(citizen);
            foreach(var citizen in simulation.Citizens)
            {
                var model=models[citizen.Id];
                model.localScale=Vector3.one*(citizen.IsAdult?1:.65f);
                model.gameObject.SetActive(citizen.Active&&citizen.InsideBuildingId==null);
                                if(!shownProfessions.TryGetValue(citizen.Id,out var shown)||shown!=citizen.Profession)
                {
                    professionLayers[citizen.Id].SetActive(false);
                    Object.Destroy(professionLayers[citizen.Id]);
                    var layer=new GameObject(citizen.Profession+" equipment");layer.transform.SetParent(model,false);
                    professionLayers[citizen.Id]=layer;shownProfessions[citizen.Id]=citizen.Profession;
                    var ordinaryApron=model.Find("Builder work apron");
                    if(ordinaryApron!=null) ordinaryApron.gameObject.SetActive(false);
                    if(citizen.Profession!=CitizenProfession.Unemployed)
                    {
                        Color color=citizen.Profession==CitizenProfession.Warrior?new Color(.48f,.5f,.55f):citizen.Profession==CitizenProfession.Fisher?new Color(.2f,.4f,.8f):
                            citizen.Profession==CitizenProfession.Mage?new Color(.48f,.25f,.7f):
                            citizen.Profession==CitizenProfession.Butcher||citizen.Profession==CitizenProfession.Scientist?new Color(.85f,.85f,.8f):
                            citizen.Profession==CitizenProfession.Builder?new Color(.95f,.64f,.12f):citizen.Profession==CitizenProfession.Farmer?new Color(.3f,.65f,.24f):citizen.Profession==CitizenProfession.Miner?new Color(.4f,.45f,.5f):new Color(.65f,.28f,.18f);
                        PrototypePalette.Shape(layer.transform,"Work vest",PrimitiveType.Cube,new Vector3(0,.66f,0),new Vector3(.34f,citizen.Profession==CitizenProfession.Mage?.75f:.45f,.25f),palette.Get(color));
                        if(PrototypePresentationRules.HasHat(citizen.Profession))
                            PrototypePalette.Shape(layer.transform,"Profession hat",PrimitiveType.Cylinder,new Vector3(0,1.18f,0),new Vector3(.4f,.04f,.4f),palette.Get(color));
                        if(PrototypePresentationRules.HasTool(citizen.Profession))
                            PrototypePalette.Shape(layer.transform,"Work tool",PrimitiveType.Cube,new Vector3(.3f,.55f,.15f),new Vector3(.045f,.65f,.045f),palette.Get(new Color(.4f,.3f,.2f)));
                                                if(citizen.Profession==CitizenProfession.Lumberjack||citizen.Profession==CitizenProfession.Builder)
                            PrototypePalette.Shape(layer.transform,"Tool head",PrimitiveType.Cube,new Vector3(.3f,.84f,.15f),new Vector3(.18f,.08f,.06f),palette.Get(new Color(.5f,.52f,.55f)));
                        if(citizen.Profession==CitizenProfession.Scientist)
                            PrototypePalette.Shape(layer.transform,"Goggles",PrimitiveType.Cube,new Vector3(0,1.06f,.15f),new Vector3(.23f,.07f,.03f),palette.Get(new Color(.3f,.55f,.7f)));
                    }
                }
                var next=new Vector3((float)citizen.Position.X,0,(float)citizen.Position.Z);
                var direction=next-model.localPosition;
                direction.y=0;
                if(direction.sqrMagnitude>.000001f) model.localRotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                if(citizen.Task==CitizenTask.Working) next.y=.015f*Mathf.Sin(Time.time*6f);
                model.localPosition=next;
                indicators[citizen.Id].SetActive(citizen.Active&&selected==citizen);
            }
        }
        private static void ReportWarning(string message)=>Debug.LogWarning(message);
        public void Dispose()
        {
            simulation.DevelopmentWarning-=ReportWarning;
            Object.Destroy(root);Object.Destroy(ringMesh);
        }
    }
}