using AzureStorageProvider.Collections;
using AzureStorageProvider.Handlers;
using AzureStorageProvider.Helpers;
using CMS.Base;
using CMS.Base.Internal;
using CMS.Base.Routing;
using CMS.Core;
using CMS.Helpers;
using CMS.IO;
using CMS.Routing.Web;

[assembly: RegisterHttpHandler("CMSPages/GetAzureProviderFile.aspx", typeof(FileHandler), Order = 1)]

namespace AzureStorageProvider.Handlers
{
    internal class FileHandler : AdvancedGetFileService
    {
        private bool? mAllowCache = null;
        private readonly IWebPathMapper mapper;

        /// <summary>
        /// Gets or sets whether cache is allowed. By default cache is allowed on live site.
        /// </summary>
        protected override bool AllowCache
        {
            get
            {
                if (mAllowCache == null)
                {
                    mAllowCache = IsLiveSite;
                }

                return mAllowCache.Value;
            }
            set => mAllowCache = value;
        }

        public FileHandler(IWebPathMapper mapper) => this.mapper = mapper;

        protected override CMSActionResult GetFileServiceResult()
        {
            string hash = QueryHelper.GetString("hash", string.Empty);
            string path = QueryHelper.GetString("path", string.Empty);

            if (!ValidationHelper.ValidateHash("?path=" + URLHelper.EscapeSpecialCharacters(path), hash, string.Empty))
            {
                return Forbidden();
            }

            if (path.StartsWithCSafe("~"))
            {
                path = mapper.MapPath(path);
            }

            string blobPath = AzurePathHelper.GetBlobPath(path);
            var blob = BlobCollection.Instance.GetOrCreate(blobPath);

            if (!blob.Exists())
            {
                return FileNotFound();
            }

            //CookieHelper.RemoveAllCookies(0);

            var cache = Service.Resolve<ICache>();
            cache.SetRevalidation(HttpCacheRevalidation.AllCaches);

            string eTag = blob.GetAttribute(a => a.Etag);
            var lastModified = ValidationHelper.GetDateTime(blob.GetAttribute(a => a.LastModified), DateTimeHelper.ZERO_TIME);

            string extension = Path.GetExtension(path);
            string contentType = MimeTypeHelper.GetMimetype(extension);

            // Client caching - only on the live site
            if (AllowCache && AllowClientCache && ETagsMatch(eTag, lastModified))
            {
                return NotModified(contentType, eTag, lastModified);
            }

            return PreparePhysicalFileResult(path, Path.GetFileName(path), extension, contentType, eTag, lastModified, true);
        }
    }
}
