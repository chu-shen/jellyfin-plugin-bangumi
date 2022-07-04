using System;
using System.Collections.Generic;
using System.IO;

namespace Jellyfin.Plugin.Bangumi.Utils
{
    public class BangumiHelper
    {
        public static String NameHelper(String searchName, Plugin plugin){

            if (plugin.Configuration.AlwaysUseAnitomySharp){
                searchName = Anitomy.AnitomyHelper.ExtractAnimeTitle(searchName);
            }

            return searchName;
        }

    }
}