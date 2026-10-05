using System.Collections.Generic;
using ColonyGame.World;
using UnityEngine;

namespace ColonyGame.Presentation
{
    // Replaceable schematic presentation. No citizen simulation runs in GameObjects.
    public sealed class CitizenView
    {
        private readonly CitizenSimulation simulation;
        private readonly Transform parent;
        private readonly GameObject root;
        private readonly Dictionary<string,Transform> models=new Dictionary<string,Transform>();
        private readonly Dictionary<string,GameObject> indicators=new Dictionary<string,GameObject>();
        private readonly Mesh ringMesh;
        public CitizenView(Transform parent,CitizenSimulation simulation,PrototypePalette palette)
        {
            this.parent=parent; this.simulation=simulation;
            simulation.DevelopmentWarning+=ReportWarning;
            root=new GameObject("Settlement Citizens"); root.transform.SetParent(parent,false);
            ringMesh=CreateRing();
            foreach(var citizen in simulation.Citizens)
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
                var ring=new GameObject("Citizen selection ring");
                ring.transform.SetParent(model.transform,false); ring.transform.localPosition=Vector3.up*.025f;
                ring.AddComponent<MeshFilter>().sharedMesh=ringMesh;
                var renderer=ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=palette.Get(new Color(.3f,.9f,1f));
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                indicators.Add(citizen.Id,ring); ring.SetActive(false);
            }
            Sync(null);
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
                if(!citizen.Active) continue;
                var bounds=new Bounds(new Vector3((float)citizen.Position.X,.65f,(float)citizen.Position.Z),new Vector3(.56f,1.3f,.56f));
                if(bounds.IntersectRay(localRay,out float distance)&&distance<nearest) { nearest=distance;closest=citizen; }
            }
            return closest;
        }
        public void Sync(Citizen selected)
        {
            foreach(var citizen in simulation.Citizens)
            {
                var model=models[citizen.Id];
                model.gameObject.SetActive(citizen.Active);
                var next=new Vector3((float)citizen.Position.X,0,(float)citizen.Position.Z);
                var direction=next-model.localPosition;
                if(direction.sqrMagnitude>.000001f) model.localRotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
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