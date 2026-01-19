using eShopLegacy.Data;
using eShopLegacy.Data.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Data.SqlClient;

namespace eShopLegacy.Data.Models.Infrastructure
{
    public class CatalogDBInitializer : MigrateDatabaseToLatestVersion<EntityModel, eShopLegacy.Data.Migrations.Configuration>
    {
        public override void InitializeDatabase(EntityModel context)
        {
            try
            {
                base.InitializeDatabase(context);
            }
            catch (Exception ex)
            {
                if (!IsObjectAlreadyExistsException(ex))
                {
                    throw;
                }
            }
        }

        private bool IsObjectAlreadyExistsException(Exception ex)
        {
            if (ex is SqlException sqlEx && (sqlEx.Number == 2714 || sqlEx.Message.Contains("There is already an object named")))
            {
                return true;
            }
            if (ex.InnerException != null)
            {
                return IsObjectAlreadyExistsException(ex.InnerException);
            }
            return false;
        }
    }
}