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
        private readonly Dictionary<string,GameObject[]> windows=new Dictionary<string,GameObject[]>();
        private readonly Dictionary<string,GameObject> dreams=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,GameObject> dust=new Dictionary<string,GameObject>();
        private readonly FireflySwarmSystem fireflies=new FireflySwarmSystem();
        private readonly MaterialPropertyBlock candleProperties=new MaterialPropertyBlock();
        private readonly Light sun;
        private readonly Material[] ground;
        private readonly Color[] groundColors;
        private readonly float originalIntensity;
        private float nightBlend;
                private bool lastDay=true;
        private readonly System.Random random=new System.Random(1039);

        private readonly GameObject playerDust;
        private readonly List<GameObject> builderDust=new List<GameObject>();
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
            for(int i=0;i<12;i++)
            {
                var puff=PrototypePalette.Shape(root.transform,"Builder work puff",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.1f,palette.Get(new Color(.7f,.62f,.45f)));
                puff.SetActive(false);builderDust.Add(puff);
            }
            for(int phase=0;phase<moonPhases.Length;phase++) moonPhases[phase]=CreateMoon((phase+.15f)/8f);
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
                if(day) windows.Values.SelectMany(v=>v).ToList().ForEach(v=>v.SetActive(false));
            }
            var working=economy.Jobs.Where(j=>j.IsFunded&&j.Builder!=null&&j.Builder.Task==CitizenTask.Working).Take(builderDust.Count).ToArray();
            for(int i=0;i<builderDust.Count;i++)
            {
                builderDust[i].SetActive(day&&i<working.Length&&Mathf.Sin(Time.time*4+i)>0);
                if(i<working.Length) builderDust[i].transform.localPosition=new Vector3((float)working[i].Builder.Position.X,.25f,(float)working[i].Builder.Position.Z);
            }
            nightBlend=Mathf.MoveTowards(nightBlend,day?0:1,seconds*.3f);
                        palette.SetNightAmount(nightBlend);
            for(int i=0;i<ground.Length;i++) ground[i].SetColor("_BaseColor",groundColors[i]*Color.Lerp(Color.white,new Color(.48f,.55f,.72f,1),nightBlend));
            if(sun!=null) sun.intensity=Mathf.Lerp(originalIntensity,originalIntensity*.3f,nightBlend);
            foreach(var id in windows.Keys.Where(id=>!state.Buildings.Any(b=>b.Id==id)).ToArray())
            {foreach(var window in windows[id]) Object.Destroy(window);windows.Remove(id);}
            foreach(var building in state.Buildings.Where(b=>BuildingEconomyRules.ResidenceCapacity(b)>0))
            {
                if(!windows.TryGetValue(building.Id,out var panes))
                {
                    panes=new GameObject[8];
                    float w=building.Definition.BaseWidth,d=building.Definition.BaseDepth;
                    float depth=building.Type==BuildingType.TownHall?.52f:building.Type==BuildingType.House?.6f:.5f;
                    float centerZ=building.Type==BuildingType.Tavern?.65f:.5f;
                    float width=building.Type==BuildingType.TownHall?.56f:building.Type==BuildingType.House?.55f:.62f;
                    var rotation=Quaternion.Euler(0,building.OrientationDegrees,0);
                    var offset=building.OrientationDegrees==90?new Vector3(0,0,w):building.OrientationDegrees==180?new Vector3(w,0,d):building.OrientationDegrees==270?new Vector3(d,0,0):Vector3.zero;
                    for(int i=0;i<panes.Length;i++)
                    {
                        int face=i/2;float side=i%2==0?-1:1;
                        var local=face<2?new Vector3(.5f*w+side*.3f*width*w,.82f,centerZ*d+(face==0?-1:1)*(depth*d*.5f+.047f)):
                            new Vector3(.5f*w+(face==2?-1:1)*(width*w*.5f+.047f),.82f,centerZ*d+side*.3f*depth*d);
                        var position=new Vector3(building.Origin.X,0,building.Origin.Z)+offset+rotation*local;
                        var pane=Glow("Occupied residential candle window "+i,position,new Vector3(.3f,.27f,.035f),new Color(1f,.73f,.25f));
                        pane.transform.localRotation=rotation*Quaternion.Euler(0,face<2?0:90,0);
                        var light=pane.AddComponent<Light>();light.type=LightType.Point;light.range=.85f;light.intensity=2.8f;
                        light.color=new Color(1f,.46f,.08f);light.shadows=LightShadows.None;
                        panes[i]=pane;
                    }
                    windows.Add(building.Id,panes);
                }
                int occupants=economy.Bootstrap.Roster.Count(c=>c.ResidenceBuildingId==building.Id&&c.Active);
                for(int i=0;i<panes.Length;i++)
                {
                    var pane=panes[i];pane.SetActive(PrototypePresentationRules.WindowLit(day,occupants));
                    double phase=building.Id.Aggregate(0,(value,c)=>unchecked(value*31+c))*.001+i*2.399;
                    float candle=(float)PrototypePresentationRules.WindowFlicker(Time.time,phase);
                    var warm=new Color(1f,.49f,.075f);
                    candleProperties.SetColor("_BaseColor",warm*candle);
                    candleProperties.SetColor("_EmissionColor",new Color(1f,.31f,.018f)*(3f*candle));
                    pane.GetComponent<Renderer>().SetPropertyBlock(candleProperties);
                    pane.GetComponent<Light>().intensity=2.8f*candle;
                }
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

            var camera=Camera.main;
            var territory=state.Territory;
            int forestDepth=economy.Bootstrap.Settings.WildernessDepth;
            int minX=territory.ClaimedTiles.Min(t=>t.X)-forestDepth-1,maxX=territory.ClaimedTiles.Max(t=>t.X)+forestDepth+2;
            int minZ=territory.ClaimedTiles.Min(t=>t.Z)-forestDepth-1,maxZ=territory.ClaimedTiles.Max(t=>t.Z)+forestDepth+2;
            bool Valid(CitizenPosition point)
            {
                if(camera==null) return false;
                int x=(int)System.Math.Floor(point.X),z=(int)System.Math.Floor(point.Z);
                if(x<minX||x>=maxX||z<minZ||z>=maxZ||territory.IsClaimed(new TileCoordinate(x,z))||!PrototypePresentationRules.ForestCell(x,z)) return false;
                var screen=camera.WorldToViewportPoint(root.transform.TransformPoint(new Vector3((float)point.X,.35f,(float)point.Z)));
                return screen.z>0&&screen.x>.02f&&screen.x<.98f&&screen.y>.02f&&screen.y<.98f;
            }
            CitizenPosition? Candidate()
            {
                if(camera==null) return null;
                var ray=camera.ViewportPointToRay(new Vector3((float)random.NextDouble(),(float)random.NextDouble(),0));
                var plane=new Plane(root.transform.up,root.transform.TransformPoint(new Vector3(0,.35f,0)));
                if(!plane.Raycast(ray,out var distance)) return null;
                var local=root.transform.InverseTransformPoint(ray.GetPoint(distance));
                return new CitizenPosition(local.x,local.z);
            }
            fireflies.Tick(seconds,day,Candidate,Valid);
        }
        // Unlit, alpha-blended screen patches anchored in world space. This avoids a tiny
        // Lit sphere being darkened/occluded by the canopy and shrunk below a screen pixel.
        public void DrawFireflies(Camera camera)
        {
            if(camera==null||economy.Clock.IsDay) return;
            var previous=GUI.color;
            foreach(var swarm in fireflies.Swarms)
            {
                if(!swarm.Active) continue;
                for(int i=0;i<swarm.Points.Length;i++)
                {
                    if(swarm.Alpha[i]<=.001) continue;
                    var point=swarm.Points[i];
                    var screen=camera.WorldToScreenPoint(root.transform.TransformPoint(new Vector3((float)point.X,.35f,(float)point.Z)));
                    if(screen.z<=0||!camera.pixelRect.Contains(new Vector2(screen.x,screen.y))) continue;
                    GUI.color=new Color(1f,.85f,.3f,(float)swarm.Alpha[i]);
                    float size=fireflies.Settings.PixelSize;
                    GUI.DrawTexture(new Rect(Mathf.Round(screen.x-size*.5f),Mathf.Round(Screen.height-screen.y-size*.5f),size,size),Texture2D.whiteTexture);
                }
            }
            GUI.color=previous;
        }
        public void Dispose()
        {
            if(sun!=null) sun.intensity=originalIntensity;
            Object.Destroy(root);
            foreach(var mesh in moonPhases) Object.Destroy(mesh);
        }
    }
}
