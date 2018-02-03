using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace api.Models
{
    /// <summary>
    /// 图片上传配置（基类）
    /// </summary>
    public class UploadBaseConfig
    {
        /// <summary>
        /// 可上传文件的扩展名
        /// </summary>
        public string AllowExtension;
        /// <summary>
        /// 可上传文件的大小（b）
        /// </summary>
        public int MaxSize;
        /// <summary>
        /// 文件保存目录
        /// </summary>
        public string SavePath;
    }
}