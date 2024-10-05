﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Bangumi.Providers;
using Jellyfin.Plugin.Bangumi.Test.Util;
using MediaBrowser.Controller.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Jellyfin.Plugin.Bangumi.Test;

[TestClass]
public class Episode
{
    private readonly Bangumi.Plugin _plugin = ServiceLocator.GetService<Bangumi.Plugin>();
    private readonly EpisodeProvider _provider = ServiceLocator.GetService<EpisodeProvider>();

    private readonly CancellationToken _token = new();

    [TestMethod]
    public void ProviderInfo()
    {
        Assert.AreEqual(_provider.Name, Constants.ProviderName);
        Assert.IsTrue(_provider.Order < 0);
    }

    [TestMethod]
    public async Task EpisodeInfo()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("White Album 2/White Album 2[01][Hi10p_1080p][BDRip][x264_2flac].mkv"),
            ProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "259013"
                }
            },
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "69496"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual("WHITE ALBUM", episodeData.Item.Name, "should return the right episode title");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("ONE PIECE/海贼王--S21--E1023.MP4"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "975"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(1023, episodeData.Item.IndexNumber, "should return the right episode number");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("ONE PIECE/海贼王--S21--E1026.MP4"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "975"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(1026, episodeData.Item.IndexNumber, "should return the right episode number");
    }

    [TestMethod]
    public async Task EpisodeInfoWithoutId()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("White Album 2/White Album 2[01][Hi10p_1080p][BDRip][x264_2flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "69496"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual("WHITE ALBUM", episodeData.Item.Name, "should return the right episode title");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[202204]辉夜大小姐想让我告白-超级浪漫-/[202204]辉夜大小姐想让我告白-超级浪漫- S3/ep01.mp4"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "317613"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(1, episodeData.Item.IndexNumber, "should fix episode index automatically");
    }

    [TestMethod]
    public async Task LargeEpisodeIndex()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("[CONAN][999][1080P][AVC_AAC][CHS_JP](B07242C7).mp4"),
            IndexNumber = 999,
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "899"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual("迷惑な親切心", episodeData.Item.Name, "should return the right episode title");
    }

    [TestMethod]
    public async Task SpecialEpisodeSupport()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("SPY x FAMILY - 10 [WebRip 1080p HEVC-10bit AAC ASSx2].mkv"),
            IndexNumber = 0,
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "329906"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreNotEqual(episodeData.Item.ParentIndexNumber, 0, "episode 10 is not special episode");
        Assert.AreEqual("ドッジボール大作戦", episodeData.Item.Name, "should return the right episode title");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("[Sword Art Online - Alicization -War of Underworld-][00][BDRIP 1920x1080 HEVC-YUV420P10 FLAC].mkv"),
            IndexNumber = 0,
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "279457"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(episodeData.Item.ParentIndexNumber, 0, "episode 0 is special episode");
        Assert.AreEqual("リフレクション", episodeData.Item.Name, "should return the right episode title");
    }

    [TestMethod]
    public async Task SpecialEpisodeFromSubFolder()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("とある科学の超電磁砲S/Specials/01.mkv"),
            IndexNumber = 0,
            SeriesProviderIds = new Dictionary<string, string> { { Constants.ProviderName, "51928" } }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(episodeData.Item.ParentIndexNumber, 0, "episode 1 is special episode");
        Assert.AreEqual("MMR Ⅲ 〜もっとまるっと超電磁砲Ⅲ〜", episodeData.Item.Name, "should return the right episode title");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("Season 1 OVA/Seitokai Yakuindomo [16][Ma10p_1080p][x265_flac].mkv"),
            IndexNumber = 0,
            SeriesProviderIds = new Dictionary<string, string> { { Constants.ProviderName, "39118" } }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(episodeData.Item.ParentIndexNumber, 0, "episode 16 is special episode");
        Assert.AreEqual("気分は青空 会長はブルー/ハイリスクが気持ちいい/ハイリスクが気持ちいい/桜才・英稜 学園交流会!", episodeData.Item.Name, "should return the right episode title");
    }

    [TestMethod]
    public async Task SpecialEpisodeMetadataFromSubject()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("OVA\\Tonikaku Kawaii: Seifuku [WebRip 1080p HEVC-10bit AAC ASSx2].mkv"),
            ParentIndexNumber = 0,
            ProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "1143188"
                }
            },
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "301541"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(episodeData.Item.ParentIndexNumber, 0, "this is special episode");
        Assert.AreEqual("トニカクカワイイ ～制服～", episodeData.Item.Name, "should use subject title as episode title");
    }

    [TestMethod]
    public async Task NonIntegerEpisodeIndexSupport()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile("Ore no Imouto ga Konna ni Kawaii Wake ga Nai - 12.5 [BD 1920x1080 x264 FLAC Sub(GB,Big5,Jap)].mkv"),
            IndexNumber = 0,
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "5436"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual("俺の妹の人生相談がこれで終わるわけがない TRUE ROUTE", episodeData.Item.Name, "should return the right episode title");
    }

    [TestMethod]
    public async Task FixEpisodeIndex()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 1080,
            Path = FakePath.CreateFile("White Album 2/White Album 2[01][Hi10p_1080p][BDRip][x264_2flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "69496"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(1, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("WHITE ALBUM", episodeData.Item.Name, "should return the right episode title");
    }

    [TestMethod]
    public async Task FixEpisodeIndexWithoutCount()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 1080,
            Path = FakePath.CreateFile("Asobi Asobase/Asobi Asobase [12][Ma10p_1080p][x265_flac_aac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "236020"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(12, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("「ダニエル」「ブラ会議」「メルヘン・バトルロワイヤル」 「紙のみぞ戦争」", episodeData.Item.Name, "should return the right episode title");
    }

    [TestMethod]
    public async Task FixEpisodeIndexWithNumberInName()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Steins;Gate 0/Steins;Gate 0 [23][Ma10p_1080p][x265_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "129807"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(23, episodeData.Item.IndexNumber, "should fix episode index automatically");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Log Horizon 2/Log Horizon 2 [08][Ma10p_1080p][x265_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "100517"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(8, episodeData.Item.IndexNumber, "should fix episode index automatically");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Kanojo, Okarishimasu/Kanojo, Okarishimasu [07][Ma444-10p_1080p][x265_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "296076"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(7, episodeData.Item.IndexNumber, "should fix episode index automatically");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Kakegurui/Kakegurui 賭ケグルイ [Live Action S01] 第02話 (BDRip 1920x1080p x264 10bit AVC FLAC).mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "230953"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(2, episodeData.Item.IndexNumber, "should fix episode index automatically");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Eighty-Six/[AI-Raws] 86 #02 スピアヘッド (BD HEVC 1920x1080 yuv444p10le FLAC)[E56E5DFE].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "302189"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(2, episodeData.Item.IndexNumber, "should fix episode index automatically");

        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Eighty-Six/[AI-Raws] 86 #22 シン (BD HEVC 1920x1080 yuv444p10le FLAC)[65CA4ED3].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "331887"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(22, episodeData.Item.IndexNumber, "should fix episode index automatically");
    }

    [TestMethod]
    public async Task FixEpisodeIndexWithBracketsInName()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Date A Live/Date A Live [05(BDBOX Ver.)][Hi10p_1080p][x264_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "49131"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(5, episodeData.Item.IndexNumber, "should fix episode index automatically");
    }

    [TestMethod]
    public async Task FixIncorrectEpisodeId()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 1080,
            Path = FakePath.CreateFile("Saki/Saki [01] [Hi10p_720p][BDRip][x264_flac].mkv"),
            ProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "162427"
                }
            },
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "1444"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual("5168", episodeData.Item.ProviderIds[Constants.ProviderName], "should return the correct episode id");
        Assert.AreEqual("出会い", episodeData.Item.Name, "should return the correct episode title");
        Assert.AreEqual(1, episodeData.Item.IndexNumber, "should fix episode index automatically");
    }

    [TestMethod]
    public async Task SpecialEpisodeInDifferentSubject()
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 1080,
            Path = FakePath.CreateFile("Yahari Ore no Seishun Lovecome wa Machigatte Iru. Zoku/Yahari Ore no Seishun Lovecome wa Machigatte Iru. Zoku [OVA][Ma10p_1080p][x265_flac].mkv"),
            ProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "555794"
                }
            },
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "102134"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual("きっと、女の子はお砂糖とスパイスと素敵な何かでできている。", episodeData.Item.Name, "should return the correct episode title");
        Assert.AreEqual(1, episodeData.Item.IndexNumber, "should fix episode index automatically");
    }

    [TestMethod]
    public async Task CorrectEpisodeIndex()
    {
        Assert.AreEqual(10,
            await TestEpisodeIndex("White Album 2[01][Hi10p_1080p][BDRip][x264_2flac].mkv", 10, 259022),
            "should use episode index 10 from episode info");
        Assert.AreEqual(10,
            await TestEpisodeIndex("White Album 2[01][Hi10p_1080p][BDRip][x264_2flac].mkv", 10, null),
            "should use episode index 10 from previous");

        _plugin.Configuration.AlwaysReplaceEpisodeNumber = true;
        Assert.AreEqual(1,
            await TestEpisodeIndex("White Album 2[01][Hi10p_1080p][BDRip][x264_2flac].mkv", 10, 259022),
            "forced episode index 1 when AlwaysReplaceEpisodeNumber is true");
        _plugin.Configuration.AlwaysReplaceEpisodeNumber = false;
    }

    private async Task<int?> TestEpisodeIndex(string fileName, int previous, int? episodeId)
    {
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            Path = FakePath.CreateFile($"White Album 2/{fileName}"),
            IndexNumber = previous,
            ProviderIds = episodeId == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>
                {
                    {
                        Constants.ProviderName, $"{episodeId}"
                    }
                },
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "69496"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        return episodeData.Item.IndexNumber;
    }

    [TestMethod]
    public async Task GetEpisodeByAnitomySharp()
    {
        _plugin.Configuration.AlwaysGetEpisodeByAnitomySharp = true;
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[VCB-Studio] BEATLESS [Ma10p_1080p]/[VCB-Studio] BEATLESS [05][Ma10p_1080p][x265_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "227102"
                }
            }
        }, _token);
        _plugin.Configuration.AlwaysGetEpisodeByAnitomySharp = false;
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(5, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("Tools for outsoucers", episodeData.Item.Name, "should return the right episode title");
    }

    [TestMethod]
    public async Task GetSpecialEpisodeByAnitomySharp()
    {
        _plugin.Configuration.AlwaysParseEpisodeByAnitomySharp = true;
        _plugin.Configuration.AlwaysGetEpisodeByAnitomySharp = true;
        _plugin.Configuration.RequestTimeout = 50000;

        //  Special(OAD)
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[Moozzi2] Nagato Yuki-chan no Shoushitsu - 17 OAD (BD 1920x1080 x.264 Flac).mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "129960"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual("終われない夏休み", episodeData.Item.OriginalTitle, "should return the right episode title");

        // 多季度且文件序号连续
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[Gintama S4 Part1][323][BDRIP][1080P][H264_FLAC].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "247"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(323, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("道", episodeData.Item.OriginalTitle, "should return the right episode title");
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[Gintama S4 Part3][347][BDRIP][1080P][H264_FLAC].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "247"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(347, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("無駄を覚えた機械を人間という", episodeData.Item.OriginalTitle, "should return the right episode title");
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[Gintama S2][235][JPN][BDRIP][1080P][H264_FLAC].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "247"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(235, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("空の星", episodeData.Item.OriginalTitle, "should return the right episode title");
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("らんま½ 第150話 できた！八宝大カビン (1080p x265 Ma10p FLAC).mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "2789"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(150, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("できた!八宝大カビン", episodeData.Item.OriginalTitle, "should return the right episode title");
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[VCB-Studio] Mobile Suit Gundam 00 [26][Ma10p_1080p][x265_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "286"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(26, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("天使再臨", episodeData.Item.OriginalTitle, "should return the right episode title");
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[VCB-Studio] Mobile Suit Gundam 00 [32][Ma10p_1080p][x265_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "286"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(32, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("再会と離別と", episodeData.Item.OriginalTitle, "should return the right episode title");

        // Special(OVA)
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Toradora! OVA [BD 1080p 23.976fps AVC-yuv420p10 FLAC] - VCB-Studio & mawen1250.mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "909"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(1, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("亜美のモノマネ150連発!!", episodeData.Item.OriginalTitle, "should return the right episode title");

        // Opening
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("[VCB-Studio] Puella Magi Madoka Magica/[VCB-Studio] Puella Magi Madoka Magica [Ma10p_1080p]/SPs/[VCB-Studio] Puella Magi Madoka Magica [NCOP][Ma10p_1080p][x265_flac].mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "9717"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(1, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("コネクト", episodeData.Item.OriginalTitle, "should return the right episode title");

        // Opening
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Toradora! 2008 [BD 1920x1080 AVC FLAC] - mawen1250&VCB-Studio/SPs/Toradora! Uncredited OP 1 [BD 1080p 23.976fps AVC-yuv420p10 FLAC] - VCB-Studio & mawen1250.mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "909"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(1, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("Toradora! Uncredited OP & OP E1", episodeData.Item.OriginalTitle, "should return the right episode title");

        // Preview
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Toradora! CM09 [BD 720p 23.976fps AVC-yuv420p10 FLAC] - VCB-Studio & mawen1250.mkv"),
            SeriesProviderIds = new Dictionary<string, string>
            {
                {
                    Constants.ProviderName, "909"
                }
            }
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(9, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("Toradora! CM & CM E09", episodeData.Item.OriginalTitle, "should return the right episode title");

        _plugin.Configuration.AlwaysParseEpisodeByAnitomySharp = false;
        _plugin.Configuration.AlwaysGetEpisodeByAnitomySharp = false;
    }

    [TestMethod]
    public async Task EpisodeOffsetSupport()
    {
        FakePath.CreateFile("Kimetsu no Yaiba/Season 2/bangumi.ini", "[Bangumi]\nID=350764\nOffset=26\n");
        var episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Kimetsu no Yaiba/Season 2/[BeanSub&FZSD&LoliHouse] Kimetsu no Yaiba - 27 [WebRip 1080p HEVC-10bit AAC ASSx2].mkv")
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(27, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("炎柱・煉獄杏寿郎", episodeData.Item.Name, "should return the right episode title");

        FakePath.CreateFile("Jujutsu Kaisen/Season 2/bangumi.ini", "[Bangumi]\nID=369304\nOffset=-24\n");
        episodeData = await _provider.GetMetadata(new EpisodeInfo
        {
            IndexNumber = 0,
            Path = FakePath.CreateFile("Jujutsu Kaisen/Season 2/Jujutsu Kaisen (2020) - S02E03 - Hidden Inventory 3 [WEBRip-1080p][10bit][x265][AAC 2.0][JA]-LoliHouse.mkv")
        }, _token);
        Assert.IsNotNull(episodeData, "episode data should not be null");
        Assert.IsNotNull(episodeData.Item, "episode data should not be null");
        Assert.AreEqual(3, episodeData.Item.IndexNumber, "should fix episode index automatically");
        Assert.AreEqual("懐玉-参-", episodeData.Item.Name, "should return the right episode title");
    }
}