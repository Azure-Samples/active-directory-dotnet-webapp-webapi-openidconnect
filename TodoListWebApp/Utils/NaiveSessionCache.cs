using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace TodoListWebApp.Utils
{


            //System.Web.HttpContext.Current.Session[CachePrefix + "RefreshToken"] = value;

            //return System.Web.HttpContext.Current.Session[CachePrefix + "RefreshToken"];

            //System.Web.HttpContext.Current.Session.Remove(CachePrefix + "RefreshToken");
      


    public class NaiveSessionCache: TokenCache
    {
        private static readonly object FileLock = new object();
        string UserObjectId = string.Empty;
        string CacheId = string.Empty;
        public NaiveSessionCache(string userId)
        {
            UserObjectId = userId;
            CacheId = UserObjectId + "_TokenCache";

            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            lock (FileLock)
            {
                var cache = HttpContext.Current.Session[CacheId];
                this.Deserialize(cache != null ? ProtectedData.Unprotect((byte[])cache, null, DataProtectionScope.LocalMachine) : null);
            }
        }

        // Empties the persistent store.
        public override void Clear()
        {
            base.Clear();
            System.Web.HttpContext.Current.Session.Remove(CacheId);
        }

        // Triggered right before ADAL needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
         void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                var cache = HttpContext.Current.Session[CacheId];
                this.Deserialize(cache != null ? ProtectedData.Unprotect((byte[])cache, null, DataProtectionScope.LocalMachine) : null);
            }
        }

        // Triggered right after ADAL accessed the cache.
        void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (this.HasStateChanged)
            {
                lock (FileLock)
                {                    
                    // reflect changes in the persistent store
                    HttpContext.Current.Session[CacheId] = ProtectedData.Protect(this.Serialize(),null,DataProtectionScope.LocalMachine);
                    // once the write operation took place, restore the HasStateChanged bit to false
                    this.HasStateChanged = false;
                }                
            }
        }
    }
}