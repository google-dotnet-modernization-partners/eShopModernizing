using eShopWCFService;
using eShopWCFService.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace eShopWCFService.Models.Infrastructure
{
    public class CatalogDBInitializer : CreateDatabaseIfNotExists<EntityModel>
    {
        public override void InitializeDatabase(EntityModel context)
        {
            // This will create the database and tables if they don't exist.
            // If the database exists, it does nothing (and won't throw "object exists" errors).
            base.InitializeDatabase(context);

            // Now explicitly add our custom SQL features. 
            // These scripts are written to be idempotent (IF NOT EXISTS...), so safe to run every time.
            AddDatabaseFeatures(context);
        }

        protected override void Seed(EntityModel context)
        {
            AddCatalogTypes(context);
            AddCatalogBrands(context);
            AddCatalogItems(context);
            AddCatalogItemsStock(context);
            AddDiscountItems(context);
        }

        private void AddDatabaseFeatures(EntityModel context)
        {
            var sqlCommands = new List<string>
            {
                // 1. Create CatalogItemPriceHistory table
                @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CatalogItemPriceHistory]') AND type in (N'U'))
                  BEGIN
                      CREATE TABLE [dbo].[CatalogItemPriceHistory](
                          [LogId] [int] IDENTITY(1,1) NOT NULL,
                          [ItemId] [int] NOT NULL,
                          [OldPrice] [money] NOT NULL,
                          [NewPrice] [money] NOT NULL,
                          [ChangeDate] [datetime] NOT NULL DEFAULT (GETDATE()),
                          CONSTRAINT [PK_CatalogItemPriceHistory] PRIMARY KEY CLUSTERED ([LogId] ASC)
                      );
                  END",

                // 2. Create Stored Procedure sp_UpdateStock
                @"IF OBJECT_ID('dbo.sp_UpdateStock', 'P') IS NOT NULL
                      DROP PROCEDURE dbo.sp_UpdateStock",

                @"EXEC('CREATE PROCEDURE [dbo].[sp_UpdateStock]
                      @CatalogItemId int,
                      @Delta int
                  AS
                  BEGIN
                      SET NOCOUNT ON;
                      BEGIN TRANSACTION;

                      IF EXISTS (SELECT 1 FROM [dbo].[CatalogItemsStock] WHERE [CatalogItemId] = @CatalogItemId)
                      BEGIN
                          DECLARE @CurrentStock int;
                          SELECT @CurrentStock = [AvailableStock] FROM [dbo].[CatalogItemsStock] WHERE [CatalogItemId] = @CatalogItemId;

                          DECLARE @NewStock int;
                          SET @NewStock = @CurrentStock + @Delta;

                          IF (@NewStock < 0)
                          BEGIN
                              ROLLBACK TRANSACTION;
                              THROW 51000, ''Insufficient Stock'', 1;
                              RETURN;
                          END

                          UPDATE [dbo].[CatalogItemsStock]
                          SET [AvailableStock] = @NewStock
                          WHERE [CatalogItemId] = @CatalogItemId;
                      END
                      ELSE
                      BEGIN
                          IF (@Delta < 0)
                          BEGIN
                              ROLLBACK TRANSACTION;
                              THROW 51000, ''Cannot decrease stock for non-existent item stock record'', 1;
                              RETURN;
                          END

                          DECLARE @MaxStockId int;
                          SELECT @MaxStockId = ISNULL(MAX([StockId]), 0) FROM [dbo].[CatalogItemsStock];
                          
                          INSERT INTO [dbo].[CatalogItemsStock] ([StockId], [CatalogItemId], [AvailableStock], [Date])
                          VALUES (@MaxStockId + 1, @CatalogItemId, @Delta, GETDATE());
                      END

                      COMMIT TRANSACTION;
                  END')",

                // 3. Create Scalar Function fn_GetDiscountedPrice
                @"IF OBJECT_ID('dbo.fn_GetDiscountedPrice', 'FN') IS NOT NULL
                      DROP FUNCTION dbo.fn_GetDiscountedPrice",

                @"EXEC('CREATE FUNCTION [dbo].[fn_GetDiscountedPrice] 
                  (
                      @CatalogItemId int
                  )
                  RETURNS money
                  AS
                  BEGIN
                      DECLARE @BasePrice money;
                      DECLARE @FinalPrice money;
                      DECLARE @DiscountSize float;

                      SELECT @BasePrice = [Price] FROM [dbo].[CatalogItems] WHERE [Id] = @CatalogItemId;

                      SELECT TOP 1 @DiscountSize = [Size]
                      FROM [dbo].[DiscountItems]
                      WHERE GETDATE() BETWEEN [Start] AND [End]
                      ORDER BY [Size] DESC;

                      IF (@DiscountSize IS NOT NULL)
                      BEGIN
                          SET @FinalPrice = @BasePrice - (@BasePrice * CAST(@DiscountSize AS money));
                      END
                      ELSE
                      BEGIN
                          SET @FinalPrice = @BasePrice;
                      END

                      RETURN @FinalPrice;
                  END')",

                // 4. Create Trigger trg_AuditPriceChange
                @"IF OBJECT_ID('dbo.trg_AuditPriceChange', 'TR') IS NOT NULL
                      DROP TRIGGER dbo.trg_AuditPriceChange",

                @"EXEC('CREATE TRIGGER [dbo].[trg_AuditPriceChange]
                  ON [dbo].[CatalogItems]
                  AFTER UPDATE
                  AS
                  BEGIN
                      SET NOCOUNT ON;

                      INSERT INTO [dbo].[CatalogItemPriceHistory] (ItemId, OldPrice, NewPrice, ChangeDate)
                      SELECT 
                          d.Id, 
                          d.Price, 
                          i.Price, 
                          GETDATE()
                      FROM deleted d
                      INNER JOIN inserted i ON d.Id = i.Id
                      WHERE d.Price <> i.Price;
                  END')"
            };

            foreach (var command in sqlCommands)
            {
                context.Database.ExecuteSqlCommand(command);
            }
        }

        private void AddCatalogTypes(EntityModel context)
        {
            var preconfiguredTypes = PreconfiguredData.GetPreconfiguredCatalogTypes();

            foreach (var type in preconfiguredTypes)
            {
                context.CatalogTypes.Add(type);
            }

            context.SaveChanges();
        }

        private void AddCatalogBrands(EntityModel context)
        {
            var preconfiguredBrands = PreconfiguredData.GetPreconfiguredCatalogBrands();

            foreach (var brand in preconfiguredBrands)
            {
                context.CatalogBrands.Add(brand);
            }

            context.SaveChanges();
        }

        private void AddDiscountItems(EntityModel context)
        {
            var preconfiguredDiscounts = PreconfiguredData.GetPreconfiguredDiscountItems();

            foreach (var discount in preconfiguredDiscounts)
            {
                context.DiscountItems.Add(discount);
            }

            context.SaveChanges();
        }

        private void AddCatalogItems(EntityModel context)
        {
            var preconfiguredItems = PreconfiguredData.GetPreconfiguredCatalogItems();

            foreach (var item in preconfiguredItems)
            {
                context.CatalogItems.Add(item);
            }

            context.SaveChanges();
        }

        private void AddCatalogItemsStock(EntityModel context)
        {
            var preconfiguredStock = PreconfiguredData.GetPreconfiguredCatalogItemsStock();

            foreach (var s in preconfiguredStock)
            {
                context.CatalogItemsStocks.Add(s);
            }

            context.SaveChanges();
        }
    }
}