using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    [StaticConstructorOnStartup]
    public static class Resources
    {
        public static readonly Texture2D dig = ContentFinder<Texture2D>.Get("UI/Dig");
        public static readonly Texture2D digIn = ContentFinder<Texture2D>.Get("UI/DigIn");
        public static readonly Texture2D digOut = ContentFinder<Texture2D>.Get("UI/DigOut");
        public static readonly Texture2D commit = ContentFinder<Texture2D>.Get("UI/Commit");
        public static readonly Texture2D inner = ContentFinder<Texture2D>.Get("UI/Inner");
        public static readonly Texture2D getIn = ContentFinder<Texture2D>.Get("UI/GetIn");
        public static readonly Texture2D info = ContentFinder<Texture2D>.Get("UI/Info");
    }
}
