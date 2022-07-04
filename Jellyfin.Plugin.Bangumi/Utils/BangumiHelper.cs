using System;
using Microsoft.Extensions.Logging;

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