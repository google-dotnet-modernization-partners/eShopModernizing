
-- 1. Create CatalogItemPriceHistory table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CatalogItemPriceHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CatalogItemPriceHistory](
        [LogId] [int] IDENTITY(1,1) NOT NULL,
        [ItemId] [int] NOT NULL,
        [OldPrice] [money] NOT NULL,
        [NewPrice] [money] NOT NULL,
        [ChangeDate] [datetime] NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_CatalogItemPriceHistory] PRIMARY KEY CLUSTERED ([LogId] ASC)
    );
END
GO

-- 2. Create Stored Procedure sp_UpdateStock
IF OBJECT_ID('dbo.sp_UpdateStock', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_UpdateStock
GO

CREATE PROCEDURE [dbo].[sp_UpdateStock]
    @CatalogItemId int,
    @Delta int
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- Check if item exists in stock table
    IF EXISTS (SELECT 1 FROM [dbo].[CatalogItemsStock] WHERE [CatalogItemId] = @CatalogItemId)
    BEGIN
        DECLARE @CurrentStock int;
        SELECT @CurrentStock = [AvailableStock] FROM [dbo].[CatalogItemsStock] WHERE [CatalogItemId] = @CatalogItemId;

        DECLARE @NewStock int;
        SET @NewStock = @CurrentStock + @Delta;

        IF (@NewStock < 0)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51000, 'Insufficient Stock', 1;
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
            THROW 51000, 'Cannot decrease stock for non-existent item stock record', 1;
            RETURN;
        END

        DECLARE @MaxStockId int;
        SELECT @MaxStockId = ISNULL(MAX([StockId]), 0) FROM [dbo].[CatalogItemsStock];
        
        INSERT INTO [dbo].[CatalogItemsStock] ([StockId], [CatalogItemId], [AvailableStock], [Date])
        VALUES (@MaxStockId + 1, @CatalogItemId, @Delta, GETDATE());
    END

    COMMIT TRANSACTION;
END
GO

-- 3. Create Scalar Function fn_GetDiscountedPrice
IF OBJECT_ID('dbo.fn_GetDiscountedPrice', 'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetDiscountedPrice
GO

CREATE FUNCTION [dbo].[fn_GetDiscountedPrice] 
(
    @CatalogItemId int
)
RETURNS money
AS
BEGIN
    DECLARE @BasePrice money;
    DECLARE @FinalPrice money;
    DECLARE @DiscountSize float;

    -- Get Base Price
    SELECT @BasePrice = [Price] FROM [dbo].[CatalogItems] WHERE [Id] = @CatalogItemId;

    -- Check for active discount (Global discount model based on DiscountItems table)
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
END
GO

-- 4. Create Trigger trg_AuditPriceChange
IF OBJECT_ID('dbo.trg_AuditPriceChange', 'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_AuditPriceChange
GO

CREATE TRIGGER [dbo].[trg_AuditPriceChange]
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
END
GO
