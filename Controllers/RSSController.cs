using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ServiceModel.Syndication;
using System.Xml;
using TGbot_RssFeed.Models;
using Formatting = Newtonsoft.Json.Formatting;


namespace TGbot_RssFeed.Controllers
{
    [ApiController]
    [Route("[controller]/[Action]")]
    public class RSSController : ControllerBase
    {
        private readonly UserSubList subList;
        private readonly string SavePath = Path.Combine(Environment.CurrentDirectory, "Sub");
        public RSSController(UserSubList subList)
        {
            this.subList = subList;
        }

        [HttpPost]
        public async Task<ActionResult> Insert(long id, string url )
        {
            try
            {
                #region 讀取id資料
                var UserPath = Path.Combine(SavePath, "UserList.txt");
                Console.WriteLine(SavePath);
                Console.WriteLine(UserPath);

                if (!System.IO.File.Exists(UserPath))
                    await System.IO.File.Create(UserPath).DisposeAsync();
                List<UserList> UserList = new List<UserList>();
                //讀取現有RSS清單
                var list = await ReadUserList(UserPath);
                //判斷是否為舊有id
                if (list != null) UserList = list;
                else UserList.Add(new UserList { id = id });
                // 寫入資料到檔案
                using (StreamWriter writer = new StreamWriter(UserPath))
                {
                    await writer.WriteLineAsync(JsonConvert.SerializeObject(UserList, Formatting.Indented));
                }
                #endregion

                #region 讀取id訂閱資料
                var subpath = Path.Combine(SavePath, $"{id}.txt");
                Console.WriteLine(subpath);

                if (!System.IO.File.Exists(subpath))
                    await System.IO.File.Create(subpath).DisposeAsync();

                List<UserSubList> subRssList = new List<UserSubList>();
                //讀取現有RSS清單
                var record = await ReadSubRssList(subpath);
                //判斷是否為舊有id
                if (record != null) subRssList = record;
                //判斷是否已訂閱
                if (subRssList.Any(x => x.url == url)) return NotFound("當前網站已訂閱");
                #endregion

                //取ID最大值
                int max = 0;
                if (subRssList.Any()) max = subRssList.Max(o => o.id);
                //取網站名稱
                try
                {
                    XmlReader reader = XmlReader.Create(url);
                    SyndicationFeed feed = SyndicationFeed.Load(reader);
                    //寫入當前資料
                    subList.id = max + 1;
                    subList.name = feed.Title.Text;
                    subList.url = url;
                    subList.updateTime = DateTime.Now;

                    subRssList.Add(subList);
                    reader.Close();
                }
                catch (Exception ex)
                {
                    return NotFound(ex.Message);
                }

                //轉換成json格式
                var JsubList = JsonConvert.SerializeObject(subRssList, Formatting.Indented);

                // 寫入資料到檔案
                using (StreamWriter writer = new StreamWriter(subpath))
                {
                    await writer.WriteLineAsync(JsubList);
                }

                return Ok(JsubList);
            }
            catch(Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
        [HttpPost]
        public async Task<ActionResult> Delete(long id,int delID)
        {
            try
            {
                #region 讀取id訂閱資料
                var subpath = $"{SavePath}\\{id}.txt";
                if (!System.IO.File.Exists(subpath))
                    return NotFound("找不到訂閱資料");

                //讀取現有RSS清單
                var record = await ReadSubRssList(subpath);
                #endregion

                //搜尋訂閱網站
                var serash = record.Where(o => o.id == delID).ToList();
                if (!serash.Any()) return NotFound("找不到訂閱資料");
                //移除
                record.Remove(serash[0]);
                //轉換成json格式
                var JsubList = JsonConvert.SerializeObject(record, Formatting.Indented);

                // 寫入資料到檔案
                using (StreamWriter writer = new StreamWriter(subpath))
                {
                    await writer.WriteLineAsync(JsubList);
                }

                return Ok(JsubList);

            }
            catch(Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
        [HttpGet]
        public async Task<ActionResult> Search(long id)
        {
            try
            {
                var subpath = Path.Combine(SavePath, $"{id}.txt");
                if (!System.IO.File.Exists(subpath))
                    return NotFound("找不到訂閱資料");

                //讀取現有RSS清單
                var record = await ReadSubRssList(subpath);
                if(!record.Any()) return NotFound("訂閱清單為空");

                return Ok(record);
            }
            catch(Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
        public static async Task<List<UserSubList>> ReadSubRssList(string SavePath)
        {
            List<UserSubList> subRssList = new List<UserSubList>();
            if (System.IO.File.Exists(SavePath))
            {
                using var file = System.IO.File.ReadAllTextAsync(SavePath);
                subRssList = JsonConvert.DeserializeObject<List<UserSubList>>(await file);
            }
            return subRssList;
        }
        public static async Task<List<UserList>> ReadUserList(string UserPath)
        {
            List<UserList> userlist = new List<UserList>();
            if (System.IO.File.Exists(UserPath))
            {
                using var file = System.IO.File.ReadAllTextAsync(UserPath);
                userlist = JsonConvert.DeserializeObject<List<UserList>>(await file);
            }
            return userlist;
        }
    }
}
