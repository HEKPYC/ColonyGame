using System.Collections.Generic;
using System.Linq;
using ColonyGame.World;
using UnityEngine;

namespace ColonyGame.Presentation
{
    // Lightweight pooled schematic effects; no simulation state is derived from these visuals.
    public sealed class SettlementAmbience
    {
        private readonly SettlementEconomy economy;
        private readonly PrototypePalette palette;
        private readonly GameObject root;
        private readonly Dictionary<string,GameObject> windows=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,GameObject> dreams=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,GameObject> dust=new Dictionary<string,GameObject>();
        private readonly List<GameObject> fireflies=new List<GameObject>();
        private readonly Light sun;
        private readonly Material[] ground;
        private readonly Color[] groundColors;
        private readonly float originalIntensity;
        private float nightBlend;
                private bool lastDay=true;
        private readonly System.Random random=new System.Random(1039);
        private readonly double[] fireflyRefresh=new double[12];
        private readonly Vector3[] fireflyPosition=new Vector3[12];
        private readonly GameObject playerDust;
        private readonly Mesh[] moonPhases=new Mesh[8];
        public SettlementAmbience(Transform parent,SettlementEconomy economy,PrototypePalette palette,Material[] ground)
        {
            this.economy=economy;this.palette=palette;this.ground=ground;
            groundColors=ground.Select(m=>m.GetColor("_BaseColor")).ToArray();
            root=new GameObject("Settlement ambience");root.transform.SetParent(parent,false);
            sun=Object.FindObjectsByType<Light>().FirstOrDefault(l=>l.type==LightType.Directional);
                                    originalIntensity=sun!=null?sun.intensity:1;
            foreach(var color in new[]{new Color(.9f,.78f,.35f),new Color(.65f,.75f,.95f),new Color(1f,.73f,.25f)})
            {
                var glow=palette.Get(color);
                glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",color*.2f);
            }
            playerDust=PrototypePalette.Shape(root.transform,"Player construction puff",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.1f,palette.Get(new Color(.7f,.62f,.45f)));
            playerDust.SetActive(false);
            for(int phase=0;phase<moonPhases.Length;phase++) moonPhases[phase]=CreateMoon((phase+.15f)/8f);
            for(int i=0;i<12;i++)
            {
                var fly=PrototypePalette.Shape(root.transform,"Wilderness firefly",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.045f,palette.Get(new Color(.9f,.78f,.35f)));
                fly.SetActive(false);fireflies.Add(fly);
            }
        }
        private static Mesh CreateMoon(float phase)
        {
            const int rows=24;
            var vertices=new Vector3[(rows+1)*2];
            var triangles=new int[rows*6];
            float angle=phase*Mathf.PI*2;
            for(int row=0;row<=rows;row++)
            {
                float y=-.5f+(float)row/rows,radius=Mathf.Sqrt(Mathf.Max(0,.25f-y*y));
                float terminator=Mathf.Cos(angle)*radius;
                float left=angle<=Mathf.PI?terminator:-radius,right=angle<=Mathf.PI?radius:-terminator;
                vertices[row*2]=new Vector3(left,y,0);vertices[row*2+1]=new Vector3(right,y,0);
                if(row<rows)
                {
                    int k=row*6,a=row*2;
                    triangles[k]=a;triangles[k+1]=a+2;triangles[k+2]=a+1;
                    triangles[k+3]=a+1;triangles[k+4]=a+2;triangles[k+5]=a+3;
                }
            }
            var mesh=new Mesh{name="Schematic moon phase"};mesh.vertices=vertices;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private GameObject Glow(string name,Vector3 position,Vector3 size,Color color)=>
            PrototypePalette.Shape(root.transform,name,PrimitiveType.Cube,position,size,palette.Get(color));
                public void ShowPlayerWork(BuildingState site,bool working)
        {
            playerDust.SetActive(working&&site!=null&&Mathf.Sin(Time.time*4)>0);
            if(site!=null) playerDust.transform.localPosition=new Vector3(site.Entrance.ExteriorTile.X+.5f,.3f,site.Entrance.ExteriorTile.Z+.5f);
        }
        public void Update(float seconds)
        {
            var state=economy.Bootstrap.Settlement;
            bool day=economy.Clock.IsDay;
            if(day!=lastDay)
            {
                lastDay=day;
                if(day) windows.Values.ToList().ForEach(v=>v.SetActive(false));
            }
            nightBlend=Mathf.MoveTowards(nightBlend,day?0:1,seconds*.3f);
                        palette.SetNightAmount(nightBlend);
            for(int i=0;i<ground.Length;i++) ground[i].SetColor("_BaseColor",groundColors[i]*Color.Lerp(Color.white,new Color(.48f,.55f,.72f,1),nightBlend));
            if(sun!=null) sun.intensity=Mathf.Lerp(originalIntensity,originalIntensity*.3f,nightBlend);
            foreach(var id in windows.Keys.Where(id=>!state.Buildings.Any(b=>b.Id==id)).ToArray())
            {Object.Destroy(windows[id]);windows.Remove(id);}
            foreach(var building in state.Buildings.Where(b=>BuildingEconomyRules.ResidenceCapacity(b)>0))
            {
                if(!windows.TryGetValue(building.Id,out var window))
                {
                    var entrance=building.Entrance;
                    var outward=new Vector3(entrance.Facing.X,0,entrance.Facing.Z);
                    var position=new Vector3(entrance.FootprintTile.X+.5f,.85f,entrance.FootprintTile.Z+.5f)+outward*.51f;
                    window=Glow("Occupied residential window",position,new Vector3(.35f,.25f,.025f),new Color(1f,.73f,.25f));
                    window.transform.localRotation=Quaternion.Euler(0,building.OrientationDegrees,0);
                    var light=window.AddComponent<Light>();light.type=LightType.Point;light.range=2;light.intensity=.5f;light.color=new Color(1f,.68f,.3f);light.shadows=LightShadows.None;
                    windows.Add(building.Id,window);
                }
                window.SetActive(!day&&economy.Bootstrap.Roster.Any(c=>c.InsideBuildingId==building.Id));
            }
            foreach(var id in dreams.Keys.Where(id=>!economy.Bootstrap.Roster.Any(c=>c.Id==id)).ToArray())
            {Object.Destroy(dreams[id]);dreams.Remove(id);}
            foreach(var citizen in economy.Bootstrap.Roster)
            {
                if(!dreams.TryGetValue(citizen.Id,out var dream))
                {
                    dream=PrototypePalette.Shape(root.transform,"Sleeping moon",PrimitiveType.Sphere,Vector3.zero,new Vector3(.25f,.25f,.25f),palette.Get(new Color(.65f,.75f,.95f)));
                    dreams.Add(citizen.Id,dream);
                }
                dream.SetActive(citizen.InsideBuildingId!=null&&!day);
                if(citizen.InsideBuildingId!=null)
                {
                    var home=state.Buildings.FirstOrDefault(b=>b.Id==citizen.InsideBuildingId);
                    if(home!=null) dream.transform.localPosition=new Vector3(home.Origin.X+home.Width*.5f+.3f*economy.Bootstrap.Roster.ToList().IndexOf(citizen),2.6f+.08f*Mathf.Sin(Time.time),home.Origin.Z+home.Depth*.5f);
                    int phase=Mathf.FloorToInt(Mathf.Repeat(Time.time*.15f+citizen.Attributes.Charisma,8));
                    dream.GetComponent<MeshFilter>().sharedMesh=moonPhases[phase];
                    dream.transform.localScale=Vector3.one*(.22f+.025f*Mathf.Sin(Time.time*.7f+citizen.Attributes.Dexterity));
                    if(Camera.main!=null) dream.transform.rotation=Camera.main.transform.rotation;
                }
            }
            foreach(var id in dust.Keys.Where(id=>!economy.Bootstrap.Roster.Any(c=>c.Id==id)).ToArray())
            {Object.Destroy(dust[id]);dust.Remove(id);}
            foreach(var citizen in economy.Bootstrap.Roster)
            {
                if(!dust.TryGetValue(citizen.Id,out var puff))
                {
                    puff=PrototypePalette.Shape(root.transform,"Schematic work puff",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.1f,palette.Get(new Color(.7f,.62f,.45f)));
                    dust.Add(citizen.Id,puff);
                }
                bool physical=citizen.Profession==CitizenProfession.Builder||citizen.Profession==CitizenProfession.Miner||citizen.Profession==CitizenProfession.Lumberjack||citizen.Profession==CitizenProfession.Farmer||citizen.Profession==CitizenProfession.Butcher;
                puff.SetActive(day&&physical&&citizen.Task==CitizenTask.Working&&Mathf.Sin(Time.time*4)>0);
                puff.transform.localPosition=new Vector3((float)citizen.Position.X+.22f,.35f+.06f*Mathf.Sin(Time.time*4),(float)citizen.Position.Z);
            }

            for(int i=0;i<fireflies.Count;i++)
            {
                if(Time.time>=fireflyRefresh[i])
                {
                    fireflyRefresh[i]=Time.time+2+random.NextDouble()*6;
                    int minX=state.Territory.ClaimedTiles.Min(t=>t.X)-4,maxX=state.Territory.ClaimedTiles.Max(t=>t.X)+5;
                    int minZ=state.Territory.ClaimedTiles.Min(t=>t.Z)-4,maxZ=state.Territory.ClaimedTiles.Max(t=>t.Z)+5;
                    for(int attempt=0;attempt<20;attempt++)
                    {
                        int x=random.Next(minX,maxX),z=random.Next(minZ,maxZ);
                        if(!state.Territory.IsClaimed(new TileCoordinate(x,z))) {fireflyPosition[i]=new Vector3(x+.5f,.6f,z+.5f);break;}
                    }
                }
                var position=fireflyPosition[i];
                bool outside=!state.Territory.IsClaimed(new TileCoordinate(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.z)));
                float glimmer=Mathf.Max(0,Mathf.Sin(Time.time*(.65f+i*.031f)+i*2.3f));
                fireflies[i].SetActive(!day&&outside&&glimmer>.3f);
                fireflies[i].transform.localScale=Vector3.one*(.02f+.025f*glimmer);
                fireflies[i].transform.localPosition=position+new Vector3(.1f*Mathf.Sin(Time.time+i),.06f*Mathf.Sin(Time.time*1.3f+i),0);
            }
        }
        public void Dispose()
        {
            if(sun!=null) sun.intensity=originalIntensity;
            Object.Destroy(root);
            foreach(var mesh in moonPhases) Object.Destroy(mesh);
        }
    }
}