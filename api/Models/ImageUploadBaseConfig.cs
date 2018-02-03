using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace api.Models
{
    // <summary>
    /// 图片上传配置（基类）
    /// </summary>
    public class ImageUploadBaseConfig : UploadBaseConfig
    {
        ///// <summary>
        ///// 可上传图片的扩展名
        ///// </summary>
        //public string AllowExtension;
        ///// <summary>
        ///// 可上传图片的大小（b）
        ///// </summary>
        //public int MaxSize;
        ///// <summary>
        ///// 图片保存目录
        ///// </summary>
        //public string SavePath;
        /// <summary>
        /// 图片处理模式: None（不处理）、Cut（裁剪图片）、Zoom（等比例缩放）
        /// </summary>
        public string Mode;
        /// <summary>
        /// 图片处理后是否删除原图
        /// </summary>
        public bool DelOriginalPhoto;
        /// <summary>
        /// 图片质量1-100
        /// </summary>
        public int Quality;
        /// <summary>
        /// 图片处理后的宽（默认值）
        /// </summary>
        public int Width;
        /// <summary>
        /// 图片处理后的高（默认值）
        /// </summary>
        public int Height;
    }
}