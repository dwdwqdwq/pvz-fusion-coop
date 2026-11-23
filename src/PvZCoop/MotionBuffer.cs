using System;
using System.Collections.Generic;
namespace PvZCoop;

// Host timestamps drive playback. Packet arrival only supplies samples; it never
// moves the playback clock forward or teleports the displayed position.
public sealed class MotionBuffer
{
 readonly List<(float time,float x,float y)> samples=new();
 const float Delay=.22f,PredictionLimit=.75f;
 float playTime,lastRead,shownX,shownY;bool started;
 public void RebaseLatest(){if(samples.Count==0)return;var latest=samples[^1];samples.Clear();samples.Add(latest);started=false;}
 public void Add(float time,float x,float y){
  if(samples.Count>0){var last=samples[^1];
   if(time<last.time)return;
   if(Math.Abs(x-last.x)>4||Math.Abs(y-last.y)>4){samples.Clear();started=false;}
   else if(time==last.time){samples[^1]=(time,x,y);return;}
  }
  samples.Add((time,x,y));if(samples.Count>32)samples.RemoveAt(0);
 }
 public bool Read(float now,out float x,out float y){
  x=y=0;if(samples.Count==0)return false;
  if(!started){started=true;playTime=samples[0].time-Delay;lastRead=now;shownX=samples[0].x;shownY=samples[0].y;x=shownX;y=shownY;return true;}
  float dt=Math.Clamp(now-lastRead,0,.1f);lastRead=now;
  var last=samples[^1];float error=last.time-Delay-playTime;
  float rate=Math.Clamp(1+error*.35f,.92f,1.08f);
  playTime=Math.Min(playTime+dt*rate,last.time+PredictionLimit);
  Sample(playTime,out float targetX,out float targetY);
  float speed=.25f;
  for(int i=Math.Max(1,samples.Count-12);i<samples.Count;i++){
   var a=samples[i-1];var b=samples[i];float interval=b.time-a.time;
   if(interval>.001f){float dx=b.x-a.x,dy=b.y-a.y;speed=Math.Max(speed,MathF.Sqrt(dx*dx+dy*dy)/interval*1.15f);}
  }
  float vx=(targetX-shownX)*(1-MathF.Exp(-dt*20));float vy=(targetY-shownY)*(1-MathF.Exp(-dt*20));
  float distance=MathF.Sqrt(vx*vx+vy*vy),limit=speed*dt;
  if(distance>limit&&distance>0){vx*=limit/distance;vy*=limit/distance;}
  shownX+=vx;shownY+=vy;x=shownX;y=shownY;return true;
 }
 void Sample(float time,out float x,out float y){
  var first=samples[0];x=first.x;y=first.y;if(time<=first.time)return;
  for(int i=1;i<samples.Count;i++){
   var a=samples[i-1];var b=samples[i];if(time>b.time)continue;
   float t=(time-a.time)/(b.time-a.time);x=a.x+(b.x-a.x)*t;y=a.y+(b.y-a.y)*t;return;
  }
  var last=samples[^1];x=last.x;y=last.y;
  if(samples.Count>1){var previous=samples[Math.Max(0,samples.Count-4)];float dt=last.time-previous.time;
   if(dt>.001f){float ahead=Math.Clamp(time-last.time,0,PredictionLimit);x+=(last.x-previous.x)/dt*ahead;y+=(last.y-previous.y)/dt*ahead;}
  }
 }
}
