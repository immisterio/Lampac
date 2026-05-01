using Newtonsoft.Json.Linq;
using System.Web;

namespace Shared.Models.Module;

public class OnlineEventsModel
{
    public OnlineEventsModel(string id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, int year, string source, string rchtype, int serial, bool isanime, bool life, bool islite, string account_email, string uid, string token, string nws_id, JObject kitconf)
    {
        this.id = id;
        this.imdb_id = imdb_id;
        this.kinopoisk_id = kinopoisk_id;
        this.title = title;
        this.original_title = original_title;
        this.original_language = original_language;
        this.year = year;
        this.source = source;
        this.rchtype = rchtype;
        this.serial = serial;
        this.isanime = isanime;
        this.life = life;
        this.islite = islite;
        this.account_email = account_email;
        this.uid = uid;
        this.token = token;
        this.nws_id = nws_id;
        this.kitconf = kitconf;
    }

    public string id { get; set; }
    public string imdb_id { get; set; }
    public long kinopoisk_id { get; set; }
    public string title { get; set; }
    public string original_title { get; set; }
    public string original_language { get; set; }
    public int year { get; set; }
    public string source { get; set; }
    public string rchtype { get; set; }
    public int serial { get; set; }
    public bool isanime { get; set; }
    public bool life { get; set; }
    public bool islite { get; set; }
    public string account_email { get; set; }
    public string uid { get; set; }
    public string token { get; set; }
    public string nws_id { get; set; }
    public JObject kitconf { get; set; }

    public string ToArgs()
        => $"imdb_id={HttpUtility.UrlEncode(imdb_id)}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&serial={serial}&original_language={HttpUtility.UrlEncode(original_language)}&year={year}&source={HttpUtility.UrlEncode(source)}&rchtype={HttpUtility.UrlEncode(rchtype)}&isanime={(isanime ? "1" : "0")}&life={(life ? "1" : "0")}&islite={(islite ? "1" : "0")}&account_email={HttpUtility.UrlEncode(account_email)}&uid={HttpUtility.UrlEncode(uid)}&token={HttpUtility.UrlEncode(token)}&nws_id={HttpUtility.UrlEncode(nws_id)}";
}
