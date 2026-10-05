using System.Collections.Generic;
using ColonyGame.World;
using UnityEngine;

namespace ColonyGame.Presentation
{
    public sealed class PeacefulWildlifeView
    {
        private readonly PeacefulWildlifeSystem system;
        private readonly GameObject root;
        private readonly Dictionary<int,Transform> models = new Dictionary<int,Transform>();
        public PeacefulWildlifeView(Transform parent, PeacefulWildlifeSystem system, PrototypePalette palette)
        {
            this.system=system;
            system.DevelopmentWarning += ReportWarning;
            root=new GameObject("Peaceful Wilderness Wildlife"); root.transform.SetParent(parent,false);
            foreach(var animal in system.Animals)
            {
                var model=new GameObject(animal.Species.ToString()); model.transform.SetParent(root.transform,false);
                models.Add(animal.Id,model.transform);
                float scale=animal.Species==PeacefulSpecies.Squirrel?.35f:animal.Species==PeacefulSpecies.Deer?.85f:.6f;
                Color coat=animal.Species==PeacefulSpecies.Badger?new Color(.43f,.46f,.49f):animal.Species==PeacefulSpecies.Deer?new Color(.48f,.3f,.15f):new Color(.85f,.39f,.12f);
                var t=model.transform;
                System.Action<string,Vector3,Vector3,Color> shape=(name,pos,size,color)=>PrototypePalette.Shape(t,name,PrimitiveType.Cube,pos*scale,size*scale,palette.Get(color));
                bool deer=animal.Species==PeacefulSpecies.Deer, squirrel=animal.Species==PeacefulSpecies.Squirrel;
                float height=deer?.55f:.23f;
                shape("Body",new Vector3(0,height,0),new Vector3(.35f,.25f,.6f),coat);
                shape("Head",new Vector3(0,height+.15f,.34f),new Vector3(.22f,.24f,.22f),coat);
                if(deer)
                {
                    foreach(float x in new[]{-.12f,.12f}) foreach(float z in new[]{-.2f,.2f}) shape("Leg",new Vector3(x,.22f,z),new Vector3(.06f,.44f,.06f),coat);
                    shape("Neck",new Vector3(0,.64f,.25f),new Vector3(.16f,.4f,.16f),coat);
                    foreach(float x in new[]{-.13f,.13f}) shape("Antler",new Vector3(x,.92f,.33f),new Vector3(.04f,.24f,.04f),new Color(.72f,.6f,.4f));
                }
                else if(squirrel) shape("Upright tail",new Vector3(0,.38f,-.3f),new Vector3(.16f,.55f,.18f),coat);
                else
                {
                    shape("Tail",new Vector3(0,.22f,-.38f),new Vector3(.16f,.14f,.25f),coat);
                    shape("White muzzle",new Vector3(0,height+.13f,.45f),new Vector3(.14f,.1f,.06f),Color.white);
                    if(animal.Species==PeacefulSpecies.Fox) shape("White tail tip",new Vector3(0,.22f,-.48f),new Vector3(.17f,.15f,.06f),Color.white);
                    else foreach(float x in new[]{-.07f,.07f}) shape("White head stripe",new Vector3(x,height+.27f,.34f),new Vector3(.035f,.02f,.23f),Color.white);
                }
            }
            Sync();
        }
        public void Tick(float seconds) { system.Tick(seconds); Sync(); }
        public void Sync()
        {
            foreach(var animal in system.Animals)
            {
                var model=models[animal.Id]; model.gameObject.SetActive(animal.Active);
                var next=new Vector3((float)animal.Position.X,0,(float)animal.Position.Z);
                var direction=next-model.localPosition;
                if(direction.sqrMagnitude>.000001f) model.localRotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                model.localPosition=next;
            }
        }
        private static void ReportWarning(string message) { Debug.LogWarning(message); }
        public void Dispose() { system.DevelopmentWarning -= ReportWarning; Object.Destroy(root); }
    }
}
