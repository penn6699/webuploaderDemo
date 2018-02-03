using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using api.Common;
using api.Models;
using System.Web;
using System.Web.Http.Cors;

namespace api.Controllers
{
    [EnableCors(origins:"*",headers:"*",methods:"*",SupportsCredentials =true)]
    [RoutePrefix("Upload")]
    public class UploadController : ApiController
    {
        /// <summary>
        /// 是否调试状态（若true，上传的文件不会实际保存）
        /// </summary>
        private bool IsDeBug { get { return Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsDeBug"].ToString()); } }
        private HttpServerUtility Server
        {
            get {
                return HttpContext.Current.Server;
            }
        }
        private HttpRequest _Request
        {
            get { return HttpContext.Current.Request; }
        }


        public AjaxResult ToAjaxResult(int code,object data, string errmsg="") {
            return new AjaxResult {
                code= code,
                errmsg= errmsg,
                data= data
            };
        }

        #region 公共方法
        /// <summary>
        /// 上传文件的类型
        /// </summary>
        private enum UploadCategory
        {
            images,
            files
        }
        private class Data
        {
            public string OriginalName { get; set; }
            public string SaveFileName { get; set; }
            public string SaveFilePath { get; set; }
            public string Ext { get; set; }
            public int Size { get; set; }
        }
        /**
         * 重命名文件
         * @return string
         */
        private string reName(HttpPostedFile fileData)
        {
            return DateTime.Now.ToString("yyyyMMddHHmmssfff") + new Random().Next(1000, 9999) + getFileExt(fileData);
        }

        /**
         * 文件类型检测
         * @return bool
         */
        private bool checkType(HttpPostedFile fileData, string[] filetype)
        {
            var currentType = getFileExt(fileData);
            return Array.IndexOf(filetype, currentType) == -1;
        }



        /**
         * 获取文件扩展名
         * @return string
         */
        private string getFileExt(HttpPostedFile fileData)
        {
            string[] temp = fileData.FileName.Split('.');
            return "." + temp[temp.Length - 1].ToLower();
        }


        /// <summary>
        /// 获取文件的保存目录
        /// </summary>
        /// <param name="UploadCategory"></param>
        /// <returns></returns>
        private string GetSaveDir(UploadCategory UploadCategory)
        {
            string path;
            switch (UploadCategory)
            {
                case UploadCategory.images:
                    path = "/Upload/images/";
                    break;
                case UploadCategory.files:
                    path = "/Upload/files/";
                    break;
                default:
                    path = "/Upload/temp/";
                    break;
            }
            string dir = path + DateTime.Now.ToString("yyyy-MM-dd") + "/";
            string _dir = Server.MapPath(dir);
            //目录不存在则创建
            if (!Directory.Exists(_dir))
            {
                Directory.CreateDirectory(_dir);
            }

            return dir;
        }
  
        #endregion

        /// <summary>
        /// 上传图片,根据config的参数值确定对应的配置文件
        /// </summary>
        /// <param name="fileData"></param>
        /// <returns></returns>
        [HttpPost,Route("UploadImage")]
        public AjaxResult UploadImage()
        {
            AjaxResult ar = new AjaxResult();
            try
            {
                
               HttpPostedFile  fileData = _Request.Files[0];
                string config = _Request["config"] ?? "ImageUploadBaseConfig";

                ConfigHelper ConfigHelper = new ConfigHelper();

                //获取配置
                var configInstance = ConfigHelper.GetConfig<Models.ImageUploadBaseConfig>(config);

                #region 检查文件及配置

                //格式验证
                if (checkType(fileData, configInstance.AllowExtension.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries)))
                {
                    throw new Exception("不允许的文件类型");
                }
                //大小验证
                if (fileData.ContentLength >= configInstance.MaxSize)
                {
                    throw new Exception("文件大小超出网站限制");
                }

                #endregion

                //获取文件的保存目录
                string saveDir = GetSaveDir(UploadCategory.images);

                //设置要保存的文件的名称
                string saveFileName = reName(fileData);
                string saveFilePath = saveDir + saveFileName;

                //文件保存
                fileData.SaveAs(Server.MapPath(saveFilePath));

                ar.errmsg = string.Empty;
                ar.code = 0;
                //构造返回的数据格式
                ar.data = new Data()
                {
                    OriginalName = fileData.FileName,
                    SaveFileName = saveFileName,
                    SaveFilePath = saveFilePath,
                    Ext = getFileExt(fileData),
                    Size = fileData.ContentLength
                };
            }
            catch (Exception exp) {
                ar.errmsg = exp.Message;
                ar.code = -1;
                ar.data = null;
            }
            return ar;
        }
        
        /////////////////////////////////////////////////////////////////////////////


        /// <summary>
        /// 分片参数
        /// </summary>
        private class ChunkTemp
        {
            public bool chunked { get; set; }
            public string ext { get; set; }
        }
        /// <summary>
        /// 文件分块上传
        /// </summary>
        /// <param name="fileData"></param>
        /// <returns></returns>
        [HttpPost, Route("ChunkUpload")]
        public AjaxResult ChunkUpload()
        {
            HttpPostedFile file = _Request.Files[0];
            if (IsDeBug)
            {
                return ToAjaxResult(0, new ChunkTemp
                {
                    chunked = true,
                    ext = Path.GetExtension(file.FileName)
                });
            }
            if (file == null) //当用户在上传文件时突然刷新页面，可能造成这种情况
            {
                return ToAjaxResult(-1, new ChunkTemp
                {
                    chunked = _Request.Form.AllKeys.Any(m => m == "chunk"),
                    ext = Path.GetExtension(file.FileName)
                }, "HttpPostedFileBase对象为null");
                
            }

            string root = Server.MapPath("/Upload/files/");
            //如果进行了分片
            if (_Request.Form.AllKeys.Any(m => m == "chunk"))
            {
                //取得chunk和chunks
                int chunk = Convert.ToInt32(_Request.Form["chunk"]);//当前分片在上传分片中的顺序（从0开始）
                int chunks = Convert.ToInt32(_Request.Form["chunks"]);//总分片数
                //根据GUID创建用该GUID命名的临时文件夹
                //string folder = Server.MapPath("~/Upload/" + Request["md5"] + "/");
                string folder = root + "chunk\\" + _Request["md5"] + "\\";
                string path = folder + chunk;

                //建立临时传输文件夹
                if (!Directory.Exists(Path.GetDirectoryName(folder)))
                {
                    Directory.CreateDirectory(folder);
                }

                FileStream addFile = null;
                BinaryWriter AddWriter = null;
                Stream stream = null;
                BinaryReader TempReader = null;

                try
                {
                    //addFile = new FileStream(path, FileMode.Append, FileAccess.Write);
                    addFile = new FileStream(path, FileMode.Create, FileAccess.Write);
                    AddWriter = new BinaryWriter(addFile);
                    //获得上传的分片数据流
                    stream = file.InputStream;
                    TempReader = new BinaryReader(stream);
                    //将上传的分片追加到临时文件末尾
                    AddWriter.Write(TempReader.ReadBytes((int)stream.Length));
                }
                finally
                {
                    if (addFile != null)
                    {
                        addFile.Close();
                        addFile.Dispose();
                    }
                    if (AddWriter != null)
                    {
                        AddWriter.Close();
                        AddWriter.Dispose();
                    }
                    if (stream != null)
                    {
                        stream.Close();
                        stream.Dispose();
                    }
                    if (TempReader != null)
                    {
                        TempReader.Close();
                        TempReader.Dispose();
                    }
                }

                //context.Response.Write("{\"chunked\" : true, \"hasError\" : false, \"f_ext\" : \"" + Path.GetExtension(file.FileName) + "\"}");
                //return CommonResult.ToJsonStr(0, string.Empty, "{\"chunked\" : true, \"ext\" : \"" + Path.GetExtension(file.FileName) + "\"}");
                return ToAjaxResult(0, new ChunkTemp
                {
                    chunked = true,
                    ext = Path.GetExtension(file.FileName)
                });
                
            }
            else//没有分片直接保存
            {
                string path = root + _Request["md5"] + Path.GetExtension(_Request.Files[0].FileName);
                _Request.Files[0].SaveAs(path);
                //context.Response.Write("{\"chunked\" : false, \"hasError\" : false}");
                //return CommonResult.ToJsonStr(0, string.Empty, "{\"chunked\" : false}");
                return ToAjaxResult(0, new ChunkTemp
                {
                    chunked = false,
                    ext = Path.GetExtension(file.FileName)
                });
                
            }
        }


        #region 获取指定文件的已上传的文件块
        /// <summary>
        /// 获取指定文件的已上传的文件块
        /// </summary>
        /// <returns></returns>
        [HttpGet, Route("GetMaxChunk")]
        public AjaxResult GetMaxChunk()
        {
            if (IsDeBug) { return ToAjaxResult(0, 0);}

            string root = Server.MapPath("/Upload/files/");
            try
            {
                var md5 = Convert.ToString(_Request["md5"]);
                var ext = Convert.ToString(_Request["ext"]);
                int chunk = 0;

                var fileName = md5 + "." + ext;

                FileInfo file = new FileInfo(root + fileName);
                if (file.Exists)
                {
                    chunk = Int32.MaxValue;
                }
                else
                {
                    if (Directory.Exists(root + "chunk\\" + md5))
                    {
                        DirectoryInfo dicInfo = new DirectoryInfo(root + "chunk\\" + md5);
                        var files = dicInfo.GetFiles();
                        chunk = files.Count();
                        if (chunk > 1)
                        {
                            chunk = chunk - 1; //当文件上传中时，页面刷新，上传中断，这时最后一个保存的块的大小可能会有异常，所以这里直接删除最后一个块文件
                        }
                    }
                }

                return ToAjaxResult(0, chunk);
            }
            catch
            {
                return ToAjaxResult(0, 0);
            }
        }
        #endregion

        #region 合并文件
        /// <summary>
        /// 合并文件
        /// </summary>
        /// <returns></returns>
        [HttpPost, Route("MergeFiles")]
        public AjaxResult MergeFiles()
        {
            string root = Server.MapPath("/Upload/files/");

            string guid = _Request["md5"];
            string ext = _Request["ext"];
            string sourcePath = Path.Combine(root, "chunk\\" + guid + "\\");//源数据文件夹
            string targetPath = Path.Combine(root, guid + ext);//合并后的文件

            if (IsDeBug) {
                return ToAjaxResult(0, new {
                    chunked=true,
                    hasError= false,
                    savePath= System.Web.HttpUtility.UrlEncode(targetPath)+""
                }); 
            }

            DirectoryInfo dicInfo = new DirectoryInfo(sourcePath);
            if (Directory.Exists(Path.GetDirectoryName(sourcePath)))
            {
                FileInfo[] files = dicInfo.GetFiles();
                foreach (FileInfo file in files.OrderBy(f => int.Parse(f.Name)))
                {
                    FileStream addFile = new FileStream(targetPath, FileMode.Append, FileAccess.Write);
                    BinaryWriter AddWriter = new BinaryWriter(addFile);

                    //获得上传的分片数据流 
                    Stream stream = file.Open(FileMode.Open);
                    BinaryReader TempReader = new BinaryReader(stream);
                    //将上传的分片追加到临时文件末尾
                    AddWriter.Write(TempReader.ReadBytes((int)stream.Length));
                    //关闭BinaryReader文件阅读器
                    TempReader.Close();
                    stream.Close();
                    AddWriter.Close();
                    addFile.Close();

                    TempReader.Dispose();
                    stream.Dispose();
                    AddWriter.Dispose();
                    addFile.Dispose();
                }
                DeleteFolder(sourcePath);
                //context.Response.Write("{\"chunked\" : true, \"hasError\" : false, \"savePath\" :\"" + System.Web.HttpUtility.UrlEncode(targetPath) + "\"}");

                return ToAjaxResult(0, new
                {
                    chunked = true,
                    hasError = false,
                    savePath = System.Web.HttpUtility.UrlEncode(targetPath) + ""
                });
                
            }
            else
            {
                //context.Response.Write("{\"hasError\" : true}");

                return ToAjaxResult(0, new{
                    hasError = true
                });
            }
        }

        /// <summary>
        /// 删除文件夹及其内容
        /// </summary>
        /// <param name="dir"></param>
        private void DeleteFolder(string strPath)
        {
            //删除这个目录下的所有子目录
            if (Directory.GetDirectories(strPath).Length > 0)
            {
                foreach (string fl in Directory.GetDirectories(strPath))
                {
                    Directory.Delete(fl, true);
                }
            }
            //删除这个目录下的所有文件
            if (Directory.GetFiles(strPath).Length > 0)
            {
                foreach (string f in Directory.GetFiles(strPath))
                {
                    System.IO.File.Delete(f);
                }
            }
            Directory.Delete(strPath, true);
        }

        #endregion



    }

    [Serializable]
    public class AjaxResult {
        /// <summary>
        /// 小于0表示失败
        /// </summary>
        public int code;
        public string errmsg;
        public object data;
    }

}
