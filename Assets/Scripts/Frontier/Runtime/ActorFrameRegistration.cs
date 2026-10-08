using System.Collections.Generic;
using UnityEngine;
namespace Starfall {
    // Generated from accepted authored pixels by register_actor_frames.py.
    internal static class ActorFrameRegistration {
        internal static readonly Dictionary<string, Rect[]> Heads = new Dictionary<string, Rect[]> {
            { "PlayerVanguard", new [] { new Rect(105,148,45,48), new Rect(107,148,43,47), new Rect(107,145,47,48), new Rect(106,145,49,48), new Rect(118,153,47,48), new Rect(109,154,52,49), new Rect(106,166,45,48), new Rect(123,158,48,48), new Rect(124,168,48,48), new Rect(105,157,47,48), new Rect(79,153,50,49), new Rect(79,192,47,44) } },
            { "PlayerRanger", new [] { new Rect(105,145,49,48), new Rect(105,144,46,48), new Rect(105,142,49,48), new Rect(102,142,49,48), new Rect(102,144,51,49), new Rect(108,149,49,48), new Rect(111,157,49,48), new Rect(97,152,56,49), new Rect(127,171,50,48), new Rect(99,151,49,48), new Rect(86,155,48,48), new Rect(81,195,44,46) } },
            { "PlayerArcanist", new [] { new Rect(106,145,46,48), new Rect(108,148,46,48), new Rect(104,142,46,48), new Rect(104,144,45,48), new Rect(118,150,46,49), new Rect(112,156,47,48), new Rect(107,164,45,48), new Rect(117,149,41,49), new Rect(113,168,48,48), new Rect(100,154,46,48), new Rect(77,156,46,48), new Rect(77,189,47,46) } },
            { "PlayerRear", new [] { new Rect(105,145,50,48), new Rect(108,147,46,48), new Rect(100,143,48,49), new Rect(103,146,47,48), new Rect(101,142,51,48), new Rect(103,142,49,48), new Rect(107,149,48,48), new Rect(105,141,51,49), new Rect(106,137,49,49), new Rect(108,138,49,49), new Rect(103,145,48,49), new Rect(96,141,59,48) } },
            { "PlayerSouth", new [] { new Rect(110,150,45,47), new Rect(105,155,47,47), new Rect(106,156,47,47), new Rect(110,161,47,47), new Rect(104,157,48,47), new Rect(106,159,46,46), new Rect(106,162,48,46), new Rect(107,160,49,46), new Rect(103,152,47,47), new Rect(103,151,48,47), new Rect(99,148,48,48), new Rect(103,152,46,47) } },
            { "CompanionMira", new [] { new Rect(100,143,57,44), new Rect(93,143,62,44), new Rect(98,143,64,44), new Rect(96,147,61,44), new Rect(96,149,63,44), new Rect(96,148,57,44), new Rect(105,143,47,44), new Rect(107,151,41,44), new Rect(98,152,44,44), new Rect(78,144,73,44), new Rect(94,145,68,44), new Rect(97,143,43,44) } },
            { "CompanionIvo", new [] { new Rect(111,143,42,44), new Rect(115,144,41,44), new Rect(112,143,40,44), new Rect(106,151,41,44), new Rect(108,150,39,44), new Rect(108,151,39,44), new Rect(108,153,54,44), new Rect(107,153,39,44), new Rect(109,139,46,44), new Rect(98,140,50,44), new Rect(102,139,41,44), new Rect(101,156,54,44) } },
            { "CompanionSen", new [] { new Rect(111,145,42,52), new Rect(116,146,37,52), new Rect(100,148,35,52), new Rect(115,160,38,52), new Rect(114,164,36,52), new Rect(104,153,39,52), new Rect(98,149,51,52), new Rect(97,157,54,52), new Rect(103,147,48,52), new Rect(109,142,52,52), new Rect(113,150,51,52), new Rect(95,150,55,52) } },
            { "PlayerReactionsRear", new [] { new Rect(116,150,55,49), new Rect(104,152,45,47), new Rect(93,160,45,46), new Rect(82,178,51,48), new Rect(92,152,54,49), new Rect(106,148,49,48), new Rect(120,154,47,47), new Rect(131,182,51,47), new Rect(92,151,53,49), new Rect(106,150,49,47), new Rect(121,161,45,46), new Rect(139,182,49,47) } },
            { "PlayerReactionsSouth", new [] { new Rect(112,169,49,47), new Rect(104,157,46,47), new Rect(82,157,49,46), new Rect(71,184,50,54), new Rect(111,159,49,48), new Rect(101,148,50,48), new Rect(84,152,48,47), new Rect(76,195,46,48), new Rect(103,156,48,48), new Rect(102,148,50,48), new Rect(80,156,45,47), new Rect(70,184,47,51) } },
            { "WalkVanguard", new [] { new Rect(103,146,49,48), new Rect(103,145,49,48), new Rect(104,145,49,48), new Rect(104,146,48,48), new Rect(105,145,47,48), new Rect(105,146,47,49), new Rect(103,145,49,48), new Rect(104,146,48,48), new Rect(104,146,49,48), new Rect(104,145,48,48), new Rect(104,145,48,49), new Rect(103,145,50,48) } },
            { "WalkRanger", new [] { new Rect(104,145,49,48), new Rect(104,145,48,47), new Rect(103,145,50,48), new Rect(104,145,48,47), new Rect(105,145,47,46), new Rect(105,145,47,46), new Rect(104,145,48,47), new Rect(105,145,47,47), new Rect(104,145,48,47), new Rect(104,145,49,47), new Rect(104,145,48,47), new Rect(104,145,48,47) } },
            { "WalkArcanist", new [] { new Rect(106,145,45,48), new Rect(106,145,44,48), new Rect(105,145,45,49), new Rect(105,145,46,48), new Rect(106,145,43,45), new Rect(107,145,43,45), new Rect(106,145,43,45), new Rect(107,145,43,44), new Rect(104,145,48,49), new Rect(104,145,48,49), new Rect(104,145,48,49), new Rect(104,145,49,49) } },
            { "WalkMira", new [] { new Rect(97,143,62,44), new Rect(95,143,65,44), new Rect(95,143,66,44), new Rect(96,143,63,44), new Rect(107,143,42,44), new Rect(106,142,44,44), new Rect(107,144,42,44), new Rect(106,143,44,44), new Rect(99,144,58,44), new Rect(104,143,49,44), new Rect(103,143,50,44), new Rect(102,142,51,44) } },
            { "WalkIvo", new [] { new Rect(108,142,40,44), new Rect(109,143,39,44), new Rect(106,152,43,44), new Rect(107,142,42,44), new Rect(102,157,52,44), new Rect(110,149,36,44), new Rect(102,143,52,44), new Rect(102,142,52,44), new Rect(109,142,38,44), new Rect(109,143,38,44), new Rect(108,141,39,44), new Rect(101,144,55,44) } },
            { "WalkSen", new [] { new Rect(110,147,37,52), new Rect(110,146,36,52), new Rect(109,146,38,52), new Rect(110,146,36,52), new Rect(103,152,50,52), new Rect(107,151,41,52), new Rect(102,148,51,52), new Rect(102,152,52,52), new Rect(110,139,36,52), new Rect(108,144,41,52), new Rect(107,138,42,52), new Rect(107,145,41,52) } },
        };
        internal static bool TryStableHead(Texture2D sheet, int row, out Rect rect) {
            string name=sheet.name; int slash=name.LastIndexOf('/'); if(slash>=0) name=name.Substring(slash+1);
            if(name=="WalkVanguard" || name=="WalkRanger" || name=="WalkArcanist") { rect=new Rect(100,140,56,56); return true; }
            Rect[] all; if(Heads.TryGetValue(name,out all) && name.StartsWith("Walk") && row>=0 && row*4+3<all.Length) {
                float x=256,y=256,right=0,bottom=0; for(int i=row*4;i<row*4+4;i++) { Rect h=all[i]; x=System.Math.Min(x,h.x); y=System.Math.Min(y,h.y); right=System.Math.Max(right,h.x+h.width); bottom=System.Math.Max(bottom,h.y+h.height); }
                rect=new Rect(x,y,right-x,bottom-y); return true;
            } rect=new Rect(); return false;
        }
        internal static bool TryHead(Texture2D sheet, int frame, out Rect rect) {
            string name=sheet.name; int slash=name.LastIndexOf('/'); if(slash>=0) name=name.Substring(slash+1);
            Rect[] all; if(Heads.TryGetValue(name,out all) && frame>=0 && frame<all.Length) { rect=all[frame]; return true; }
            rect=new Rect(); return false;
        }
    }
}
